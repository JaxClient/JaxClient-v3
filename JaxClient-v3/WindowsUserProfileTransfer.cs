using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace JaxClient_v3;

internal sealed record WindowsUserProfile(
    string AccountName,
    string ProfilePath)
{
    public string DisplayName => AccountName;
}

internal static class WindowsUserProfileTransfer
{
    private const int Logon32LogonInteractive = 2;
    private const int Logon32ProviderDefault = 0;

    [DllImport(
        "advapi32.dll",
        EntryPoint = "LogonUserW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(
        string userName,
        string? domain,
        IntPtr password,
        int logonType,
        int logonProvider,
        out SafeAccessTokenHandle token);

    public static IReadOnlyList<WindowsUserProfile> GetOtherProfiles()
    {
        RegistryView view = Environment.Is64BitOperatingSystem
            ? RegistryView.Registry64
            : RegistryView.Registry32;
        using RegistryKey machineKey =
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using RegistryKey profileList = machineKey.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList")
            ?? throw new InvalidOperationException(
                "The Windows user profile registry key could not be opened.");

        string currentProfilePath = NormalizePath(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        string? currentSid = WindowsIdentity.GetCurrent().User?.Value;
        List<WindowsUserProfile> profiles = new();

        foreach (string sidValue in profileList.GetSubKeyNames())
        {
            SecurityIdentifier sid;
            try
            {
                sid = new SecurityIdentifier(sidValue);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (sidValue.Equals(currentSid, StringComparison.OrdinalIgnoreCase) ||
                sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
                sid.IsWellKnown(WellKnownSidType.LocalServiceSid) ||
                sid.IsWellKnown(WellKnownSidType.NetworkServiceSid))
            {
                continue;
            }

            using RegistryKey? profileKey = profileList.OpenSubKey(sidValue);
            string? profileValue =
                profileKey?.GetValue("ProfileImagePath") as string;
            if (string.IsNullOrWhiteSpace(profileValue))
                continue;

            string profilePath = NormalizePath(
                Environment.ExpandEnvironmentVariables(profileValue));
            if (!Directory.Exists(profilePath) ||
                profilePath.Equals(
                    currentProfilePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string accountName;
            try
            {
                accountName = sid.Translate(typeof(NTAccount)).Value;
            }
            catch (IdentityNotMappedException)
            {
                continue;
            }

            if (accountName.StartsWith(
                    "NT AUTHORITY\\",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            profiles.Add(new WindowsUserProfile(accountName, profilePath));
        }

        return profiles
            .OrderBy(profile => profile.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static SafeAccessTokenHandle Authenticate(
        WindowsUserProfile profile,
        SecureString password)
    {
        int separatorIndex = profile.AccountName.LastIndexOf('\\');
        string userName = separatorIndex >= 0
            ? profile.AccountName[(separatorIndex + 1)..]
            : profile.AccountName;
        string? domain = separatorIndex >= 0
            ? profile.AccountName[..separatorIndex]
            : null;

        IntPtr passwordPointer =
            Marshal.SecureStringToGlobalAllocUnicode(password);
        try
        {
            if (!LogonUser(
                    userName,
                    domain,
                    passwordPointer,
                    Logon32LogonInteractive,
                    Logon32ProviderDefault,
                    out SafeAccessTokenHandle token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return token;
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(passwordPointer);
        }
    }

    public static void CopyJaxStore(
        WindowsUserProfile targetProfile)
    {
        string sourceRoot = Path.GetFullPath(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE"));
        string targetRoot = Path.GetFullPath(
            Path.Combine(targetProfile.ProfilePath, ".JAXSTORE"));

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException(
                $"The source folder does not exist: {sourceRoot}");
        }

        if (NormalizePath(sourceRoot).Equals(
                NormalizePath(targetRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The source and destination .JAXSTORE folders are the same.");
        }

        Debug.WriteLine("Starting elevated .JAXSTORE replacement copy.");
        RunElevatedCopyCommand(sourceRoot, targetRoot);
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void RunElevatedCopyCommand(
        string sourceRoot,
        string targetRoot)
    {
        StringBuilder script = new();
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.AppendLine(
            $"$sourceRoot = '{EscapePowerShellLiteral(sourceRoot)}'");
        script.AppendLine(
            $"$targetRoot = '{EscapePowerShellLiteral(targetRoot)}'");
        script.AppendLine(
            "function Assert-NoReparsePoints($path) { " +
            "$item = Get-Item -LiteralPath $path -Force -ErrorAction Stop; " +
            "if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { " +
            "throw \"Copying reparse points is not supported: $path\" }; " +
            "if ($item.PSIsContainer) { " +
            "Get-ChildItem -LiteralPath $path -Force -Recurse -ErrorAction Stop | " +
            "ForEach-Object { if (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { " +
            "throw \"Copying reparse points is not supported: $($_.FullName)\" } } } }");
        script.AppendLine("Assert-NoReparsePoints $sourceRoot");
        script.AppendLine(
            "if (Test-Path -LiteralPath $targetRoot) { " +
            "$targetItem = Get-Item -LiteralPath $targetRoot -Force -ErrorAction Stop; " +
            "if (-not $targetItem.PSIsContainer) { " +
            "throw 'The destination .JAXSTORE path exists but is not a directory.' }; " +
            "Assert-NoReparsePoints $targetRoot; " +
            "Remove-Item -LiteralPath $targetRoot -Recurse -Force -ErrorAction Stop }");
        script.AppendLine(
            "New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null");
        script.AppendLine(
            "$robocopy = Join-Path $env:windir 'System32\\robocopy.exe'");
        script.AppendLine(
            "& $robocopy $sourceRoot $targetRoot '/E' '/COPY:DAT' " +
            "'/DCOPY:DAT' '/R:0' '/W:0' '/XJ' '/NFL' '/NDL' '/NJH' '/NJS' '/NP'");
        script.AppendLine("if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }");
        script.AppendLine("exit 0");

        string encodedCommand = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(script.ToString()));
        string powershellPath = Path.Combine(
            Environment.SystemDirectory,
            @"WindowsPowerShell\v1.0\powershell.exe");
        ProcessStartInfo startInfo = new(powershellPath)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = sourceRoot
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedCommand);

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "The elevated .JAXSTORE copy could not be started.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException(
                "Administrator approval for the .JAXSTORE copy was cancelled.",
                ex);
        }

        using (process)
        {
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"The elevated .JAXSTORE copy failed with exit code {process.ExitCode}.");
            }
        }
    }

    private static string EscapePowerShellLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
