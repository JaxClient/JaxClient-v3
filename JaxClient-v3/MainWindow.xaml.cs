using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using JaxClient_v3.BodyRendering;
using JaxClient_v3.Rendering;
using Microsoft.Win32;
using OpenTK.Windowing.Desktop;
using Path = System.IO.Path;

using SkiaSharp;
using WpfAnimatedGif;

namespace JaxClient_v3;


public partial class MainWindow : Window
{
    private const long MaxJxcPackUploadBytes = 249_000_000;
    private readonly List<RunningInstance> runningInstances = new();
    private readonly List<Process> runningMinecraftInstances = new();
    private string currentTab = "mod"; // Default tab: Mods.
    private bool suppressContentInstallUi;
    private Profile currentProfile;
    private Profile profileToDelete;
    private bool isDeletingProfile;
    private string filePath;
    private Dictionary<string, string> iconCache = new();
    private string installedModsSearch = "";
    private string installedRPacksSearch = "";
    private string installedShadersSearch = "";
    private bool openedFromInstalledContent = false;
    private bool IsOfflineMode = false;
    private bool ForceOfflineMode = false;
    private DispatcherTimer internetTimer;
    private readonly DispatcherTimer tokenRefreshTimer = new();
    private readonly SemaphoreSlim tokenRefreshSemaphore = new(1, 1);
    private string lastExportPath = "";
    private readonly Random random = new();
    private readonly DispatcherTimer matrixTimer = new();
    private bool isLaunching = false;
    private Process minecraftProcess;
    private Process? currentMinecraftProcess;
    private LogWindow? logWindow;
    private readonly string matrixCharsSet =
        "01ABCDEFGHIJKLMNOPQRSTUVWXYZ{}[]";
    private bool cancelLaunch = false;
    private CancellationTokenSource? launchCts;
    private readonly List<MatrixDrop> matrixDrops = new();
    private const string CurseForgeApiKey = "$2a$10$bL4bIL5pUWqfcO7KQtnMReakwtfHbNKh6v1uTpKlzhwoueEJQnPnm";
    private string currentSCTab = "content";
    private string currentSCType = "mod";
    private string currentSCSearch = "";
    private DiscordRPC.DiscordRpcClient rpc;
    private string currentBedrockSearch = "";
    private bool taskWidgetHiddenForSession = false;
    private List<StandardContentEntry> standardBrowseContent = new();
    private string currentModpackSearch = "";
    private bool openedFromModpacks = false;
    [DllImport("shell32.dll")]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        string AppID);
    private ModrinthProject selectedModpack;
    private string selectedLoader;
    private string selectedVersion;
    private readonly Dictionary<string, HashSet<string>> modpackLoaderVersions =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedSkinFile;
    private string _pendingSkinFile = "";
    private List<ContentVersionOption> contentVersionOptions = new();
    private ContentVersionRequest contentVersionRequest;
    private bool isInitializingLanguage = true;
    private WebsiteAccount? websiteAccount;
    private bool optionalPackSettingsUnselected;

    private sealed class WebsiteAccount
    {
        public WebsiteAccount()
        {
        }

        public string Email { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
    }

    private sealed class WebsitePackSummary
    {
        public WebsitePackSummary()
        {
        }

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public long Created { get; set; }
    }

    private sealed class WebsitePackInventory
    {
        public List<WebsitePackSummary> Packs { get; init; } = new();
        public bool FreeSlotAvailable { get; init; }
    }

    private sealed class PackUploadChoice
    {
        public string Mode { get; init; } = string.Empty;
        public string PackId { get; init; } = string.Empty;
    }

    public class StandardContentEntry
    {
        public string Name { get; set; }

        public string Platform { get; set; }

        public string Type { get; set; }

        public string IconUrl { get; set; }
    }
   
    public static string GetVersion()
    {
        try
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "version.json");

            if (!File.Exists(path))
                return "Unknown";

            var json = JsonDocument.Parse(File.ReadAllText(path));

            return json.RootElement
                .GetProperty("version")
                .GetString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
   

   



public static class AppRegistration
{
    public static Action<string>? StartTaskAction1;
    public static Action<string>? StopTaskAction1;
    private const string RegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\JaxClient";

    private const string VersionJsonPath = "version.json";

    private const string UninstallerUrl =
        "https://github.com/Jaxroxnyt/JaxClient-v3/releases/download/0.0.3/JaxClient-v3-Uninstaller.exe";

    private const string UninstallerFileName =
        "JaxClientUninstaller.exe";

    public static async Task CheckAndRegister()
    {
        string jsonVersion = GetVersionFromJson();

        bool needsRegistration = true;

        using RegistryKey? key =
            Registry.CurrentUser.OpenSubKey(RegistryPath);
      
        if (key != null)
        {
            string? displayName =
                key.GetValue("DisplayName")?.ToString();

            string? registeredVersion =
                key.GetValue("DisplayVersion")?.ToString();

            if (registeredVersion == jsonVersion)
            {
                needsRegistration = false;
            }
        }

        if (needsRegistration)
        {
            await Register(jsonVersion);
        }
    }

    private static string GetVersionFromJson()
    {
        try
        {
            string path =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    VersionJsonPath);

            if (!File.Exists(path))
                return "0.0.0";

            string json = File.ReadAllText(path);

            using JsonDocument doc =
                JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("version")
                .GetString() ?? "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }

    private static async Task Register(string version)
    {
        string exePath =
            Process.GetCurrentProcess()
                .MainModule!
                .FileName!;
        
        string installDir =
            Path.GetDirectoryName(exePath)!;

        string resourceDir =
            Path.Combine(installDir, "resource");
        Directory.CreateDirectory(resourceDir);

        string uninstallerPath =
            Path.Combine(resourceDir, UninstallerFileName);
        string temporaryUninstallerPath =
            uninstallerPath + ".download";

        if (!File.Exists(uninstallerPath))
        {
            try
            {
                StartTaskAction1?.Invoke("Downloading uninstaller...");
                Console.WriteLine("Downloading uninstaller...");
                using HttpClient client = new()
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };

                using HttpResponseMessage response =
                    await client.GetAsync(
                        UninstallerUrl,
                        HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using (Stream downloadStream =
                    await response.Content.ReadAsStreamAsync())
                await using (FileStream fileStream =
                    new(
                        temporaryUninstallerPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync: true))
                {
                    await downloadStream.CopyToAsync(fileStream);
                }

                File.Move(
                    temporaryUninstallerPath,
                    uninstallerPath,
                    overwrite: true);
            }
            catch (Exception ex) when (
                ex is HttpRequestException
                    or IOException
                    or UnauthorizedAccessException
                    or TaskCanceledException)
            {
                Console.Error.WriteLine(
                    $"Could not download uninstaller: {ex}");
                MessageBox.Show(
                    $"Der Uninstaller konnte nicht heruntergeladen werden:\n{ex.Message}",
                    "JaxClient",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                StopTaskAction1?.Invoke("Downloading uninstaller...");
                if (File.Exists(temporaryUninstallerPath))
                {
                    File.Delete(temporaryUninstallerPath);
                }
            }
        }

        using RegistryKey key =
            Registry.CurrentUser.CreateSubKey(
                RegistryPath)!;

        key.SetValue("DisplayName", "JaxClient");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "Jaxroxn");
        key.SetValue("DisplayIcon", exePath);

        key.SetValue(
            "InstallLocation",
            installDir);

        if (File.Exists(uninstallerPath))
        {
            key.SetValue(
                "UninstallString",
                $"\"{uninstallerPath}\"");
        }
        else
        {
            key.SetValue(
                "UninstallString",
                $"\"{exePath}\" --uninstall");
        }

        key.SetValue(
            "NoModify",
            1,
            RegistryValueKind.DWord);

        key.SetValue(
            "NoRepair",
            1,
            RegistryValueKind.DWord);

        long estimatedSizeKb =
            new FileInfo(exePath).Length / 1024;

        key.SetValue(
            "EstimatedSize",
            (int)estimatedSizeKb,
            RegistryValueKind.DWord);
    }
}


    
    
    private readonly string _resourceFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource");

    private readonly string _skinsJson =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "skins.json");
    private async void ModpackSearch_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        currentModpackSearch =
            ModpackSearchBox.Text;
    
        await LoadModpacks();
    }
    private void LoadCurrentSkin()
    {
        if (currentAccountData == null)
            return;

        string file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource",
            $"{currentAccountData.username.ToLower()}_skin.png"
        );

        if (!File.Exists(file))
            return;

        
    }
    public class SavedSkin
    {
        public string Name { get; set; } = "";
        public string File { get; set; } = "";

        public bool Selected { get; set; }
    }
    private void SaveSkinEntry(
        string name,
        string file)
    {
        List<SavedSkin> skins =
            new();

        if (File.Exists(_skinsJson))
        {
            skins =
                JsonSerializer.Deserialize<
                    List<SavedSkin>>(
                    File.ReadAllText(_skinsJson))
                ?? new();
        }

        skins.Add(
            new SavedSkin
            {
                Name = name,
                File = file,
                Selected = skins.Count == 0
            });

        File.WriteAllText(
            _skinsJson,
            JsonSerializer.Serialize(
                skins,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        
        if (skins.Count == 1)
        {
            File.Copy(
                Path.Combine(_resourceFolder, file),
                Path.Combine(_resourceFolder, "currentskin.png"),
                true);

            _selectedSkinFile = file;
        }

    }
    private async void WebsiteLogin_Click(object sender, RoutedEventArgs e)
    {
        string email = WebsiteEmailBox.Text.Trim();
        string password = WebsitePasswordBox.Password;
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            ShowToast(
                LocalizationManager.Instance.Translate(
                    "Enter email and password."));
            return;
        }

        WebsiteLoginButton.IsEnabled = false;
        object originalContent = WebsiteLoginButton.Content;
        WebsiteLoginButton.Content =
            LocalizationManager.Instance.Translate("Logging in...");
        try
        {
            using HttpClient client = new();
            var response = await client.PostAsync(
                "https://jaxroxn.lan64.de/login",
                new StringContent(
                    JsonSerializer.Serialize(new { email, password }),
                    Encoding.UTF8,
                    "application/json"));

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            bool loginSucceeded =
                response.IsSuccessStatusCode &&
                doc.RootElement.TryGetProperty(
                    "success",
                    out JsonElement successElement) &&
                successElement.ValueKind == JsonValueKind.True;
            if (!loginSucceeded)
            {
                string? error = doc.RootElement.TryGetProperty(
                        "error",
                        out JsonElement errorElement)
                    ? errorElement.GetString()
                    : null;
                ShowToast(
                    LocalizationManager.Instance.Format(
                        "Login failed: {0}",
                        string.IsNullOrWhiteSpace(error)
                            ? response.ReasonPhrase ?? "Unknown error"
                            : error));
                return;
            }

            if (!doc.RootElement.TryGetProperty(
                    "sessionToken",
                    out JsonElement tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(tokenElement.GetString()))
            {
                throw new JsonException(
                    "Login succeeded without an account session token.");
            }

            WebsiteAccount signedInAccount = new()
            {
                Email = email,
                SessionToken = tokenElement.GetString()
                    ?? throw new JsonException(
                        "Login returned an empty account session token.")
            };
            websiteAccount = signedInAccount;
            SaveWebsiteAccount(signedInAccount);
            SetWebsiteLoggedIn(email);
            WebsitePasswordBox.Clear();
            bool creditsLoaded =
                await TryRefreshWebsiteCreditsAsync(email);
            await TryRefreshWebsitePacksAsync(signedInAccount);
            ShowToast(
                LocalizationManager.Instance.Translate(
                    creditsLoaded
                        ? "Logged in"
                        : "Logged in, but credits could not be refreshed."));
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is IOException ||
            ex is UnauthorizedAccessException)
        {
            Console.WriteLine($"JaxClient website login failed: {ex}");
            ShowToast(
                LocalizationManager.Instance.Format(
                    "Login failed: {0}",
                    ex.Message));
        }
        finally
        {
            WebsiteLoginButton.Content = originalContent;
            WebsiteLoginButton.IsEnabled = true;
        }
    }

    private void OpenWebsiteRegistration_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://jaxroxn.lan64.de",
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (
            ex is System.ComponentModel.Win32Exception ||
            ex is InvalidOperationException)
        {
            Console.WriteLine(
                $"Could not open JaxClient registration page: {ex}");
            ShowToast(
                LocalizationManager.Instance.Format(
                    "Could not open registration page: {0}",
                    ex.Message));
        }
    }

    private void SaveWebsiteAccount(WebsiteAccount account)
    {
        string folder = Path.GetDirectoryName(WebsiteAccountFilePath)
            ?? throw new InvalidOperationException(
                "The website account path has no parent directory.");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            WebsiteAccountFilePath,
            JsonSerializer.Serialize(account));
    }

    private string WebsiteAccountFilePath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "websiteaccount.json");

    private WebsiteAccount? LoadWebsiteAccount()
    {
        if (!File.Exists(WebsiteAccountFilePath))
            return null;

        try
        {
            return JsonSerializer.Deserialize<WebsiteAccount>(
                File.ReadAllText(WebsiteAccountFilePath));
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"Could not read saved JaxClient account: {ex}");
            return null;
        }
    }

    private void SetWebsiteLoggedIn(string email)
    {
        WebsiteLoggedInAsText.Text =
            LocalizationManager.Instance.Format(
                "Logged in as \"{0}\"",
                email);
        WebsiteLoginPanel.Visibility = Visibility.Collapsed;
        WebsiteLoggedInPanel.Visibility = Visibility.Visible;
        WebsiteCreditsText.Text =
            LocalizationManager.Instance.Translate("Loading credits...");
    }

    private void SetWebsiteLoggedOut()
    {
        websiteAccount = null;
        WebsiteLoggedInPanel.Visibility = Visibility.Collapsed;
        WebsiteLoginPanel.Visibility = Visibility.Visible;
        WebsitePasswordBox.Clear();
        WebsiteCreditsText.Text =
            LocalizationManager.Instance.Translate("Loading credits...");
        WebsitePacksList.Children.Clear();
        WebsitePacksStatusText.Text =
            LocalizationManager.Instance.Translate("No uploaded packs.");
    }

    private async Task<int> RefreshWebsiteCreditsAsync(string email)
    {
        using HttpClient client = new();
        using HttpResponseMessage response = await client.PostAsync(
            "https://jaxroxn.lan64.de/get-credits",
            new StringContent(
                JsonSerializer.Serialize(new { email }),
                Encoding.UTF8,
                "application/json"));
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(
                "success",
                out JsonElement successElement) ||
            successElement.ValueKind != JsonValueKind.True ||
            !doc.RootElement.TryGetProperty(
                "jpoints",
                out JsonElement creditsElement) ||
            !creditsElement.TryGetInt32(out int credits))
        {
            throw new InvalidDataException(
                "The credits service returned an invalid response.");
        }

        WebsiteCreditsText.Text =
            LocalizationManager.Instance.Format(
                "Credits: {0}",
                credits);
        return credits;
    }

    private async Task InitializeWebsiteAccountAsync()
    {
        try
        {
            WebsiteAccount? savedAccount = LoadWebsiteAccount();
            if (savedAccount is null ||
                string.IsNullOrWhiteSpace(savedAccount.Email))
                return;

            websiteAccount = savedAccount;
            WebsiteEmailBox.Text = savedAccount.Email;
            SetWebsiteLoggedIn(savedAccount.Email);
            await TryRefreshWebsiteCreditsAsync(savedAccount.Email);
            await TryRefreshWebsitePacksAsync(savedAccount);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException)
        {
            Console.WriteLine(
                $"Could not restore JaxClient account display: {ex}");
        }
    }

    private HttpClient CreateWebsitePackClient(string sessionToken)
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sessionToken);
        return client;
    }

    private async Task<WebsitePackInventory> GetWebsitePacksAsync(
        WebsiteAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.SessionToken))
        {
            throw new HttpRequestException(
                LocalizationManager.Instance.Translate(
                    "Sign in again to manage packs."),
                null,
                System.Net.HttpStatusCode.Unauthorized);
        }

        using HttpClient client =
            CreateWebsitePackClient(account.SessionToken);
        using HttpResponseMessage response =
            await client.GetAsync("https://jaxroxn.lan64.de/api/packs");
        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        if (!response.IsSuccessStatusCode)
        {
            string error = doc.RootElement.TryGetProperty(
                    "error",
                    out JsonElement errorElement)
                ? errorElement.GetString() ?? response.ReasonPhrase ?? "Unknown error"
                : response.ReasonPhrase ?? "Unknown error";
            throw new HttpRequestException(
                error,
                null,
                response.StatusCode);
        }

        if (!doc.RootElement.TryGetProperty(
                "success",
                out JsonElement successElement) ||
            successElement.ValueKind != JsonValueKind.True ||
            !doc.RootElement.TryGetProperty(
                "packs",
                out JsonElement packsElement) ||
            packsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "The pack service returned an invalid response.");
        }

        List<WebsitePackSummary> packs =
            JsonSerializer.Deserialize<List<WebsitePackSummary>>(
                packsElement.GetRawText()) ?? new();
        if (packs.Any(pack =>
                string.IsNullOrWhiteSpace(pack.Id) ||
                string.IsNullOrWhiteSpace(pack.Link) ||
                !Uri.TryCreate(
                    pack.Link,
                    UriKind.Absolute,
                    out Uri? packUri) ||
                packUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(
                    packUri.Host,
                    "jaxroxn.lan64.de",
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "The pack service returned a pack without an ID or link.");
        }

        return new WebsitePackInventory
        {
            Packs = packs,
            FreeSlotAvailable =
                doc.RootElement.TryGetProperty(
                    "freeSlotAvailable",
                    out JsonElement freeSlotElement) &&
                freeSlotElement.ValueKind == JsonValueKind.True
        };
    }

    private async Task RefreshWebsitePacksAsync(
        WebsiteAccount account)
    {
        WebsitePackInventory inventory =
            await GetWebsitePacksAsync(account);
        List<WebsitePackSummary> packs = inventory.Packs;
        WebsitePacksList.Children.Clear();
        if (packs.Count == 0)
        {
            WebsitePacksStatusText.Text =
                LocalizationManager.Instance.Translate("No uploaded packs.");
            return;
        }

        WebsitePacksStatusText.Text =
            LocalizationManager.Instance.Format(
                "Uploaded packs: {0}",
                packs.Count);
        foreach (WebsitePackSummary pack in packs)
        {
            WebsitePacksList.Children.Add(CreateWebsitePackRow(pack));
        }
    }

    private async Task<bool> TryRefreshWebsitePacksAsync(
        WebsiteAccount account)
    {
        try
        {
            await RefreshWebsitePacksAsync(account);
            return true;
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is InvalidDataException ||
            ex is IOException)
        {
            Console.WriteLine(
                $"Could not refresh uploaded JaxClient packs: {ex}");
            WebsitePacksList.Children.Clear();
            WebsitePacksStatusText.Text =
                ex is HttpRequestException
                {
                    StatusCode: System.Net.HttpStatusCode.Unauthorized
                }
                    ? LocalizationManager.Instance.Translate(
                        "Sign in again to manage packs.")
                    : LocalizationManager.Instance.Format(
                        "Could not load packs: {0}",
                        ex.Message);
            return false;
        }
    }

    private UIElement CreateWebsitePackRow(WebsitePackSummary pack)
    {
        Border card = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8)
        };

        Grid row = new();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });
        row.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        TextBlock name = new()
        {
            Text = pack.Name,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = pack.Name,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(name, 0);
        row.Children.Add(name);

        Button copyButton = CreatePackActionButton(
            "Copy link",
            "#2563EB");
        copyButton.Tag = pack;
        copyButton.Click += CopyWebsitePackLink_Click;
        Grid.SetColumn(copyButton, 1);
        row.Children.Add(copyButton);

        Button deleteButton = CreatePackActionButton(
            "Delete",
            "#7F1D1D");
        deleteButton.Tag = pack;
        deleteButton.Margin = new Thickness(6, 0, 0, 0);
        deleteButton.Click += DeleteWebsitePack_Click;
        Grid.SetColumn(deleteButton, 2);
        row.Children.Add(deleteButton);

        card.Child = row;
        LocalizationManager.Instance.ApplyToVisualTree(card);
        return card;
    }

    private Button CreatePackActionButton(
        string content,
        string background)
    {
        if (new BrushConverter().ConvertFromString(background)
            is not Brush buttonBackground)
        {
            throw new InvalidOperationException(
                $"Invalid pack action button color: {background}");
        }

        return new Button
        {
            Content = LocalizationManager.Instance.Translate(content),
            Background = buttonBackground,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 0, 12, 0),
            MinWidth = 90,
            Height = 40,
            Cursor = Cursors.Hand,
            Style = (Style)FindResource("RoundedButton")
        };
    }

    private Button CreatePackPopupActionButton(
        string content,
        string background)
    {
        Button button = CreatePackActionButton(content, background);
        button.HorizontalContentAlignment =
            HorizontalAlignment.Center;
        button.VerticalContentAlignment =
            VerticalAlignment.Center;
        button.Template = CreatePackPopupButtonTemplate(
            new CornerRadius(12));
        return button;
    }

    private static ControlTemplate CreatePackPopupButtonTemplate(
        CornerRadius cornerRadius)
    {
        FrameworkElementFactory border =
            new(typeof(Border));
        border.SetBinding(
            Border.BackgroundProperty,
            new Binding(nameof(Control.Background))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        border.SetBinding(
            Border.BorderBrushProperty,
            new Binding(nameof(Control.BorderBrush))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        border.SetBinding(
            Border.BorderThicknessProperty,
            new Binding(nameof(Control.BorderThickness))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        border.SetBinding(
            Border.PaddingProperty,
            new Binding(nameof(Control.Padding))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        border.SetValue(Border.CornerRadiusProperty, cornerRadius);

        FrameworkElementFactory presenter =
            new(typeof(ContentPresenter));
        presenter.SetBinding(
            ContentPresenter.ContentProperty,
            new Binding(nameof(ContentControl.Content))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        presenter.SetBinding(
            ContentPresenter.ContentTemplateProperty,
            new Binding(nameof(ContentControl.ContentTemplate))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        presenter.SetBinding(
            ContentPresenter.HorizontalAlignmentProperty,
            new Binding(nameof(Control.HorizontalContentAlignment))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        presenter.SetBinding(
            ContentPresenter.VerticalAlignmentProperty,
            new Binding(nameof(Control.VerticalContentAlignment))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.TemplatedParent)
            });
        border.AppendChild(presenter);

        return new ControlTemplate(typeof(Button))
        {
            VisualTree = border
        };
    }

    private void CopyWebsitePackLink_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WebsitePackSummary pack })
            return;

        try
        {
            Clipboard.SetText(pack.Link);
            ShowToast(
                LocalizationManager.Instance.Translate(
                    "Pack link copied to clipboard."));
        }
        catch (Exception ex) when (
            ex is System.Runtime.InteropServices.ExternalException ||
            ex is System.Threading.ThreadStateException)
        {
            Console.WriteLine($"Could not copy JaxClient pack link: {ex}");
            ShowToast(
                LocalizationManager.Instance.Format(
                    "Could not copy link: {0}",
                    ex.Message));
        }
    }

    private async void DeleteWebsitePack_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WebsitePackSummary pack } ||
            websiteAccount is not WebsiteAccount account)
        {
            return;
        }

        if (!ShowDeleteWebsitePackDialog(pack))
            return;

        const string taskName = "Deleting JaxClient pack...";
        StartTask(taskName);
        try
        {
            string deleteUrl =
                $"https://jaxroxn.lan64.de/deletejxc/{Uri.EscapeDataString(pack.Id)}";
            bool deleted = false;
            Exception? transportFailure = null;

            for (int attempt = 0; attempt < 2 && !deleted; attempt++)
            {
                try
                {
                    using HttpClient client =
                        CreateWebsitePackClient(account.SessionToken);
                    using HttpRequestMessage request =
                        new(HttpMethod.Delete, deleteUrl)
                        {
                            Version = System.Net.HttpVersion.Version11,
                            VersionPolicy =
                                HttpVersionPolicy.RequestVersionOrLower
                        };
                    using HttpResponseMessage response =
                        await client.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead);
                    string json =
                        await response.Content.ReadAsStringAsync();

                    bool success = response.IsSuccessStatusCode &&
                        (string.IsNullOrWhiteSpace(json) ||
                         (
                             JsonDocument.Parse(json)
                             .RootElement.TryGetProperty(
                                 "success",
                                 out JsonElement successElement) &&
                             successElement.ValueKind == JsonValueKind.True
                         ));

                    if (!success)
                    {
                        string error = "Unknown error";
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            try
                            {
                                using JsonDocument doc = JsonDocument.Parse(json);
                                error = doc.RootElement.TryGetProperty(
                                        "error",
                                        out JsonElement errorElement)
                                    ? errorElement.GetString() ??
                                      response.ReasonPhrase ??
                                      "Unknown error"
                                    : response.ReasonPhrase ?? "Unknown error";
                            }
                            catch (JsonException)
                            {
                                error = json.Trim();
                                if (string.IsNullOrWhiteSpace(error))
                                    error = response.ReasonPhrase ?? "Unknown error";
                            }
                        }
                        else
                        {
                            error = response.ReasonPhrase ?? "Unknown error";
                        }

                        if (response.StatusCode ==
                            System.Net.HttpStatusCode.NotFound ||
                            response.StatusCode ==
                                System.Net.HttpStatusCode.NoContent ||
                            response.StatusCode ==
                                System.Net.HttpStatusCode.Accepted)
                        {
                            if (await IsWebsitePackDeletedAsync(account, pack.Id) ||
                                response.StatusCode ==
                                    System.Net.HttpStatusCode.NotFound)
                            {
                                deleted = true;
                                break;
                            }
                        }

                        throw new HttpRequestException(
                            error,
                            null,
                            response.StatusCode);
                    }

                    deleted = true;
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode is null)
                {
                    transportFailure = ex;
                    Console.WriteLine(
                        $"[JaxClient Packs] Delete attempt {attempt + 1} " +
                        $"lost its HTTP response: {ex}");

                    if (await IsWebsitePackDeletedAsync(account, pack.Id))
                    {
                        deleted = true;
                        break;
                    }

                    if (attempt == 0)
                    {
                        await Task.Delay(500);
                    }
                }
            }

            if (!deleted)
            {
                throw new HttpRequestException(
                    LocalizationManager.Instance.Translate(
                        "The server did not confirm pack deletion. Please try again."),
                    transportFailure);
            }

            await TryRefreshWebsitePacksAsync(account);
            ShowToast(
                LocalizationManager.Instance.Translate("Pack deleted."));
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is IOException)
        {
            Console.WriteLine($"Could not delete JaxClient pack: {ex}");
            ShowToast(
                LocalizationManager.Instance.Format(
                    "Pack action failed: {0}",
                    ex.Message));
        }
        finally
        {
            StopTask(taskName);
        }
    }

    private bool ShowDeleteWebsitePackDialog(WebsitePackSummary pack)
    {
        (Window dialog, StackPanel content) =
            CreatePackPopupWindow(
                "Delete pack",
                "Delete this uploaded pack?",
                500,
                300);

        Border packCard = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14),
            Child = new TextBlock
            {
                Text = pack.Name,
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = pack.Name
            }
        };
        content.Children.Add(packCard);

        Border warning = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(69, 26, 26)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 18),
            Child = new TextBlock
            {
                Text = LocalizationManager.Instance.Translate(
                    "This action cannot be undone."),
                Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        };
        content.Children.Add(warning);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button cancelButton = CreatePackPopupActionButton(
            "Cancel",
            "#334155");
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => dialog.Close();
        actions.Children.Add(cancelButton);

        Button deleteButton = CreatePackPopupActionButton(
            "Delete pack",
            "#B91C1C");
        deleteButton.Click += (_, _) =>
        {
            dialog.Tag = true;
            dialog.Close();
        };
        actions.Children.Add(deleteButton);
        content.Children.Add(actions);

        dialog.ShowDialog();
        return dialog.Tag is true;
    }

    private bool ShowPackUploadRetryDialog(string error)
    {
        (Window dialog, StackPanel content) =
            CreatePackPopupWindow(
                "Pack upload failed",
                "The pack could not be uploaded.",
                500,
                280);

        Border errorCard = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(69, 26, 26)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 18),
            Child = new TextBlock
            {
                Text = error,
                Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        };
        content.Children.Add(errorCard);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button cancelButton = CreatePackPopupActionButton(
            "Cancel",
            "#334155");
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => dialog.Close();
        actions.Children.Add(cancelButton);

        Button retryButton = CreatePackPopupActionButton(
            "Retry",
            "#2563EB");
        retryButton.Click += (_, _) =>
        {
            dialog.Tag = true;
            dialog.Close();
        };
        actions.Children.Add(retryButton);
        content.Children.Add(actions);

        dialog.ShowDialog();
        return dialog.Tag is true;
    }

    private async Task<(System.Net.HttpStatusCode StatusCode, string? ReasonPhrase, string Json)>
        SendJxcPackUploadAttemptAsync(
            string sessionToken,
            string filePath,
            PackUploadChoice choice,
            string requestId)
    {
        const string taskName = "Uploading JXCpack...";
        StartTask(taskName);
        try
        {
            using HttpClient client =
                CreateWebsitePackClient(sessionToken);
            client.Timeout = TimeSpan.FromMinutes(15);

            using MultipartFormDataContent form = new();
            using FileStream packStream = File.OpenRead(filePath);
            form.Add(
                new StreamContent(packStream),
                "file",
                Path.GetFileName(filePath));
            form.Add(new StringContent(choice.Mode), "mode");
            form.Add(new StringContent(requestId), "requestId");
            if (choice.Mode == "replace")
            {
                form.Add(
                    new StringContent(choice.PackId),
                    "packId");
            }

            Console.WriteLine(
                $"[JXCpack Upload] Sending POST to " +
                $"https://jaxroxn.lan64.de/uploadpack (request {requestId}).");
            using HttpResponseMessage response =
                await client.PostAsync(
                    "https://jaxroxn.lan64.de/uploadpack",
                    form);
            string json =
                await response.Content.ReadAsStringAsync();
            return (
                response.StatusCode,
                response.ReasonPhrase,
                json);
        }
        finally
        {
            StopTask(taskName);
        }
    }

    private async Task<bool> IsWebsitePackDeletedAsync(
        WebsiteAccount account,
        string packId)
    {
        try
        {
            WebsitePackInventory inventory =
                await GetWebsitePacksAsync(account);
            return inventory.Packs.All(pack =>
                !string.Equals(
                    pack.Id,
                    packId,
                    StringComparison.Ordinal));
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is InvalidDataException ||
            ex is IOException)
        {
            Console.WriteLine(
                $"[JaxClient Packs] Could not verify delete result: {ex}");
            return false;
        }
    }

    private async Task<bool> TryRefreshWebsiteCreditsAsync(string email)
    {
        try
        {
            await RefreshWebsiteCreditsAsync(email);
            return true;
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is InvalidDataException ||
            ex is IOException)
        {
            Console.WriteLine(
                $"Could not refresh JaxClient credits for {email}: {ex}");
            WebsiteCreditsText.Text =
                LocalizationManager.Instance.Translate(
                    "Credits unavailable");
            return false;
        }
    }

    private async void WebsiteLogout_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(websiteAccount?.SessionToken))
            {
                try
                {
                    using HttpClient client =
                        CreateWebsitePackClient(
                            websiteAccount.SessionToken);
                    using HttpResponseMessage response =
                        await client.PostAsync(
                            "https://jaxroxn.lan64.de/api/packs/logout",
                            content: null);
                    if (!response.IsSuccessStatusCode &&
                        response.StatusCode !=
                            System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine(
                            $"Server-side JaxClient logout returned {(int)response.StatusCode}.");
                    }
                }
                catch (Exception ex) when (
                    ex is HttpRequestException ||
                    ex is TaskCanceledException)
                {
                    Console.WriteLine(
                        $"Could not revoke JaxClient session on server: {ex}");
                }
            }

            if (File.Exists(WebsiteAccountFilePath))
                File.Delete(WebsiteAccountFilePath);

            SetWebsiteLoggedOut();
            ShowToast(
                LocalizationManager.Instance.Translate("Logged out"));
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException)
        {
            Console.WriteLine($"Could not log out JaxClient account: {ex}");
            ShowToast(
                LocalizationManager.Instance.Format(
                    "Logout failed: {0}",
                    ex.Message));
        }
    }
    private void LoadSavedSkins()
    {
        SavedSkinsPanel.Children.Clear();

        if (!File.Exists(_skinsJson))
            return;

        
        var skins =
            JsonSerializer.Deserialize<
                List<SavedSkin>>(
                File.ReadAllText(_skinsJson));
        if (skins == null || skins.Count == 0)
            return;

        if (currentAccountData != null &&
            string.IsNullOrEmpty(currentAccountData.selectedSkin))
        {
            currentAccountData.selectedSkin = skins[0].File;

            SaveAccountsToFile();
        }
        var selectedSkin = skins.FirstOrDefault(x => x.Selected);

        if (selectedSkin == null && skins.Count > 0)
        {
            skins[0].Selected = true;
            selectedSkin = skins[0];

            File.WriteAllText(
                _skinsJson,
                JsonSerializer.Serialize(
                    skins,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
        if (skins == null)
            return;

        foreach (var skin in skins)
        {
            AddSkinCard(skin);
        }
    }
    private async Task SetMinecraftSkin(string skinPath)
    {
        using var client = new HttpClient();
        var img = new BitmapImage();

        img.BeginInit();
        img.UriSource = new Uri(skinPath);
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        
        
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                currentAccountData.accessToken);

        
        using var form = new MultipartFormDataContent();

        form.Add(
            new StringContent("classic"),
            "variant");

        byte[] data =
            await File.ReadAllBytesAsync(skinPath);

        form.Add(
            new ByteArrayContent(data),
            "file",
            "skin.png");

        var response =
            await client.PostAsync(
                "https://api.minecraftservices.com/minecraft/profile/skins",
                form);

        string result =
            await response.Content.ReadAsStringAsync();

        LoadAccountHead(currentAccountData.username);
       

        if (!response.IsSuccessStatusCode)
        {

        }
    }
    private void AddSkinCard(
        SavedSkin skin)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            Height = 270,
            Margin = new Thickness(0, 10, 0, 0)
        };

        string path =
            Path.Combine(
                _resourceFolder,
                skin.File);

        var bmp = new BitmapImage();

        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();

        bmp.Freeze();

        image.Source = bmp;

        var footerGrid = new Grid
        {
            Margin = new Thickness(12, 8, 12, 12)
        };

        footerGrid.ColumnDefinitions.Add(
            new ColumnDefinition());

        footerGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        var nameText = new TextBlock
        {
            Text = skin.Name,
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var deleteButton = new TextBlock
        {
            Text = "🗑",
            FontSize = 16,
            Foreground = Brushes.White,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };

        deleteButton.Resources[typeof(Button)] = null;


        deleteButton.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;

            try
            {
                string accountsPath = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".JAXSTORE",
                    "JaxClient",
                    "accounts.json");

                if (File.Exists(accountsPath))
                {
                    var accountFile =
                        JsonSerializer.Deserialize<AccountFile>(
                            File.ReadAllText(accountsPath));

                    bool inUse =
                        accountFile?.accounts?.Any(x =>
                            string.Equals(
                                x.selectedSkin,
                                skin.File,
                                StringComparison.OrdinalIgnoreCase))
                        == true;

                    if (inUse)
                    {
                       ShowToast(
                            "This skin is currently selected by an account and cannot be deleted.");

                        return;
                    }
                }
                if (File.Exists(path))
                    File.Delete(path);

                var skins =
                    JsonSerializer.Deserialize<List<SavedSkin>>(
                        File.ReadAllText(_skinsJson))
                    ?? new();

                skins.RemoveAll(x => x.File == skin.File);
                if (!skins.Any(x => x.Selected) &&
                    skins.Count > 0)
                {
                    skins[0].Selected = true;
                
                    File.Copy(
                        Path.Combine(
                            _resourceFolder,
                            skins[0].File),
                        Path.Combine(
                            _resourceFolder,
                            "currentskin.png"),
                        true);
                }
                if (_selectedSkinFile == skin.File)
                {
                    _selectedSkinFile = null;

                    if (skins.Count > 0)
                    {
                        _selectedSkinFile = skins[0].File;

                        File.Copy(
                            Path.Combine(_resourceFolder, skins[0].File),
                            Path.Combine(_resourceFolder, "currentskin.png"),
                            true);
                    }
                }
                File.WriteAllText(
                    _skinsJson,
                    JsonSerializer.Serialize(
                        skins,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }));

                LoadSavedSkins();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        };

        Grid.SetColumn(nameText, 0);
        Grid.SetColumn(deleteButton, 1);

        footerGrid.Children.Add(nameText);
        footerGrid.Children.Add(deleteButton);

        var stack = new StackPanel();

        stack.Children.Add(image);
        stack.Children.Add(footerGrid);

        bool isSelected =
            currentAccountData != null &&
            !string.IsNullOrEmpty(currentAccountData.selectedSkin) &&
            string.Equals(
                currentAccountData.selectedSkin,
                skin.File,
                StringComparison.OrdinalIgnoreCase);

        var border = new Border
            {
                Width = 220,
                Height = 330,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(
                    Color.FromRgb(18, 28, 51)),

                BorderBrush = isSelected
                    ? Brushes.DodgerBlue
                    : new SolidColorBrush(
                        Color.FromRgb(37, 53, 84)),

                BorderThickness = isSelected
                    ? new Thickness(3)
                    : new Thickness(1),

                Child = stack,
                Margin = new Thickness(10)
            };
            

        border.MouseLeftButtonDown += async (_, _) =>
        {
            // Select the account.
            
            if (currentAccountData == null)
                {
                ShowToast("No account logged in.");
                return;
                }
            currentAccountData.selectedSkin = skin.File;
            // Save accounts.json.
            SaveAccountsToFile();

            string skinPath = Path.Combine(
                _resourceFolder,
                "mp",
                currentAccountData.selectedSkin);


            await SetMinecraftSkin(skinPath);

            // Re-render the profile head.
            LoadAccountHead(
                currentAccountData.username);

            // Update the border immediately.
            LoadSavedSkins();
            LoadSkinLocal();
            // Create the GIF only if it does not already exist.
            string gifPath = Path.Combine(
                _resourceFolder,
                $"{Path.GetFileNameWithoutExtension(
                    currentAccountData.selectedSkin)}.gif");

            if (!File.Exists(gifPath))
            {
                _ = Task.Run(async () =>
                {
                    await SkinPreviewWindow2.ShowPreview(
                        skinPath,
                        Path.GetFileNameWithoutExtension(
                            currentAccountData.selectedSkin));

                    Dispatcher.Invoke(() =>
                    {
                        LoadSkinLocal();
                    });
                });
            }

        };
        SavedSkinsPanel.Children.Add(border);
    }
    private void CancelSkinName_Click(object sender, RoutedEventArgs e)
    {
        SkinNameOverlay.Visibility = Visibility.Collapsed;
    }
    private async void ConfirmSkinName_Click(
        object sender,
        RoutedEventArgs e)
    {
        string skinName = SkinNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(skinName))
            return;

        SkinNameOverlay.Visibility =
            Visibility.Collapsed;

        string renderFile =
            await SkinPreviewWindow.ShowPreview(
                _pendingSkinFile,
                skinName);

        string mpFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource",
            "mp");

        Directory.CreateDirectory(mpFolder);

        string fileName =
            Path.GetFileName(_pendingSkinFile);


        File.Copy(
            _pendingSkinFile,
            Path.Combine(mpFolder, fileName),
            true);


        File.Copy(
            renderFile,
            Path.Combine(
                _resourceFolder,
                fileName),
            true);

        SaveSkinEntry(
            skinName,
            fileName);

        LoadSavedSkins();
    }
    
    private async void AddSkin_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PNG Files|*.png"
        };

        if (dialog.ShowDialog() != true)
            return;

        _pendingSkinFile = dialog.FileName;

        string previewRender =
            await SkinPreviewWindow.ShowPreview(
                _pendingSkinFile,
                "preview");


        await Task.Run(async () =>
        {
            await SkinPreviewWindow2.ShowPreview(
                _pendingSkinFile,
                "preview");
        });

        var preview = new BitmapImage();

        preview.BeginInit();
        preview.CacheOption = BitmapCacheOption.OnLoad;
        preview.UriSource = new Uri(previewRender);
        preview.EndInit();
        
        SkinPreviewImage.Source = preview;

        SkinNameBox.Text = "";

        SkinNameOverlay.Visibility = Visibility.Visible;
    }
    private async void Tab_Click(object sender, RoutedEventArgs e)
    {
        Button clicked = (Button)sender;
        if (clicked == AccountBtn &&
            OfflineModeCheckBox.IsChecked == true)
        {
            ShowToast("Offline Mode is enabled");
            return;
        }
        if (clicked == AccountBtn && IsOfflineMode)
        {
           
            StartTask("Checking internet...");
            await CheckInternetStatus();
            StopTask("Checking internet...");
            if (IsOfflineMode)
            {
                ShowToast("No internet connection");
            }
            else
            {
                ShowToast("Connection restored");
            }

            return;
        }



           

        HomeBtn.Tag = null;
        ProfilesBtn.Tag = null;
        SettingsBtn.Tag = null;
        AccountBtn.Tag = null;
        ModpacksBtn.Tag = null;
        SkinBtn.Tag = null;

        HomePage.Visibility = Visibility.Collapsed;
        ModpacksPage.Visibility = Visibility.Collapsed;
        ProfilesPage.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        AccountPage.Visibility = Visibility.Collapsed;
        ContentBrowserPage.Visibility = Visibility.Collapsed;
        SkinPage.Visibility = Visibility.Collapsed;


        
        clicked.Tag = "Active";


        if (clicked == HomeBtn)
            HomePage.Visibility = Visibility.Visible;

        else if (clicked == ProfilesBtn)
            ProfilesPage.Visibility = Visibility.Visible;
       
       
        else if (clicked == ModpacksBtn)
        {
            ModpacksPage.Visibility = Visibility.Visible;

            await LoadModpacks();
        }
        else if (clicked == SkinBtn)
        {
            SkinPage.Visibility = Visibility.Visible;

            LoadCurrentSkin();
        }

        else if (clicked == SettingsBtn)
            SettingsPage.Visibility = Visibility.Visible;

        else if (clicked == AccountBtn)
        {
            if (IsOfflineMode)
            {
                

                _ = Task.Run(async () =>
                {
                    StartTask("Checking internet...");
                    await CheckInternetStatus();
                    StopTask("Checking internet...");
                    Dispatcher.Invoke(() =>
                    {
                        if (!IsOfflineMode)
                        {
                            AccountPage.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            ShowToast("Still offline");
                        }
                    });
                });

                return;
            }

            AccountPage.Visibility = Visibility.Visible;
        }

    }

    private async void AddStandardContent_Click(
        object sender,
        RoutedEventArgs e)
    {
        AddStandardContentOverlay.Visibility =
            Visibility.Visible;

        // Always open the Mods tab.
        currentSCType = "mod";


        ModsSCBtn.Foreground = Brushes.White;
        RPackSCBtn.Foreground =
            (Brush)new BrushConverter()
                .ConvertFromString("#64748B");

        ShaderSCBtn.Foreground =
            (Brush)new BrushConverter()
                .ConvertFromString("#64748B");

        ModsSCLine.Visibility =
            Visibility.Visible;

        RPackSCLine.Visibility =
            Visibility.Collapsed;

        ShaderSCLine.Visibility =
            Visibility.Collapsed;


        SCSearchBox.Text = "";

        await LoadSCContent();
    }    private async void SCContentTab_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender == ModsSCBtn)
            currentSCType = "mod";
        UpdateSCTabUI();

        if (sender == RPackSCBtn)
            currentSCType = "resourcepack";

        if (sender == ShaderSCBtn)
            currentSCType = "shader";

        UpdateSCTabUI();
        await LoadSCContent();
    }
    
    
    
private async Task LoadModpacks()
{
    ModpacksList.Children.Clear();

    using HttpClient client = new();

    string url =
        "https://api.modrinth.com/v2/search" +
        "?limit=50" +
        "&query=" +
        Uri.EscapeDataString(currentModpackSearch) +
        "&facets=[[\"project_type:modpack\"]]";

    string json =
        await client.GetStringAsync(url);

    var result =
        JsonSerializer.Deserialize<ModrinthResponse>(
            json);

    if (result?.hits == null)
        return;

    List<Profile> profiles = new();

    if (File.Exists(filePath))
    {
        profiles =
            JsonSerializer.Deserialize<List<Profile>>(
                File.ReadAllText(filePath))
            ?? new();
    }

    foreach (var pack in result.hits)
    {
        try
        {
            string projectJson =
                await client.GetStringAsync(
                    $"https://api.modrinth.com/v2/project/{pack.project_id}");

            JsonDocument doc =
                JsonDocument.Parse(projectJson);

            bool validLoader = false;
            bool validVersion = false;

            if (doc.RootElement.TryGetProperty(
                    "loaders",
                    out var loaders))
            {
                validLoader =
                    loaders.EnumerateArray()
                        .Any(x =>
                        {
                            string loader =
                                x.GetString()?.ToLower();

                            return loader == "fabric"
                                || loader == "forge"
                                || loader == "neoforge"
                                || loader == "vanilla"
                                || loader == "minecraft";
                        });
            }

            if (doc.RootElement.TryGetProperty(
                    "game_versions",
                    out var versions))
            {
                validVersion =
                    versions.EnumerateArray()
                        .Any(x =>
                        {
                            string version =
                                x.GetString();

                            return
                                version == "26." ||
                                version == "26.2" ||
                                version == "26.1.2" ||
                                version == "26.1.1" ||
                                version == "26.1" ||

                                version == "1.21.11" ||
                                version == "1.21.10" ||
                                version == "1.21.9" ||
                                version == "1.21.8" ||
                                version == "1.21.7" ||
                                version == "1.21.6" ||
                                version == "1.21.5" ||
                                version == "1.21.4" ||
                                version == "1.21.3" ||
                                version == "1.21.2" ||
                                version == "1.21.1" ||
                                version == "1.21" ||

                                version == "1.20.6" ||
                                version == "1.20.5" ||
                                version == "1.20.4" ||
                                version == "1.20.3" ||
                                version == "1.20.2" ||
                                version == "1.20.1";
                        });
            }

            if (!validLoader)
                continue;

            if (!validVersion)
                continue;

            bool installed =
                profiles.Any(x =>
                    NormalizeCompareName(x.Name) ==
                    NormalizeCompareName(pack.title));

            await AddModpackCard(
                pack,
                installed);
        }
        catch
        {

        }
    }
}
private async Task AddModpackCard(
        ModrinthProject pack,
        bool installed)
{
    Border card = new Border
    {
        Background =
            (Brush)new BrushConverter()
                .ConvertFromString("#1E293B"),

        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(15),
        Margin = new Thickness(0,0,0,12),
        Cursor = Cursors.Hand
    };

    Grid grid = new Grid();

    grid.ColumnDefinitions.Add(
        new ColumnDefinition
        {
            Width = new GridLength(80)
        });

    grid.ColumnDefinitions.Add(
        new ColumnDefinition());

    grid.ColumnDefinitions.Add(
        new ColumnDefinition
        {
            Width = new GridLength(150)
        });



    Image icon = new Image
    {
        Width = 64,
        Height = 64,
        Stretch = Stretch.UniformToFill
    };

    try
    {
        if (!string.IsNullOrWhiteSpace(pack.icon_url))
        {
            icon.Source =
                new BitmapImage(
                    new Uri(pack.icon_url));
        }
    }
    catch {}

    Grid.SetColumn(icon, 0);

    grid.Children.Add(icon);



    StackPanel info =
        new StackPanel
        {
            Margin = new Thickness(10,0,0,0)
        };

    TextBlock title =
        new TextBlock
        {
            Text = pack.title,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };

    TextBlock desc =
        new TextBlock
        {
            Text = pack.description,
            Foreground = Brushes.LightGray,
            FontSize = 13,
            MaxWidth = 550,
            TextWrapping = TextWrapping.Wrap
        };

    info.Children.Add(title);
    info.Children.Add(desc);



    try
    {
        using HttpClient client = new();

        string projectJson =
            await client.GetStringAsync(
                $"https://api.modrinth.com/v2/project/{pack.project_id}");

        JsonDocument doc =
            JsonDocument.Parse(projectJson);

        string loader = "Unknown";
        string version = "Unknown";

        if (doc.RootElement.TryGetProperty("loaders", out var loaders))
        {
            loader =
                string.Join(
                    ", ",
                    loaders.EnumerateArray()
                        .Select(x => x.GetString()));
        }

        if (doc.RootElement.TryGetProperty("game_versions", out var versions))
        {
            version =
                versions.EnumerateArray()
                    .FirstOrDefault()
                    .GetString()
                    ?? "Unknown";
        }

       
    }
    catch {}

    Grid.SetColumn(info, 1);

    grid.Children.Add(info);



    StackPanel right =
        new StackPanel
        {
            VerticalAlignment =
                VerticalAlignment.Center
        };

    Button installBtn =
        new Button
        {
            Content = installed
                ? "Installed"
                : "Install",

            Width = 110,
            Height = 40,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
    if (installed)
    {
        installBtn.Background =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    "#374151"));

        installBtn.IsEnabled = false;
    }
    else
    {
        installBtn.Background =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    "#2563EB"));
    }

    MakeRounded(installBtn);

    installBtn.Click += async (s,e) =>
    {
        await OpenInstallModpackPopup(pack);

        
    };

    right.Children.Add(installBtn);

    Grid.SetColumn(right, 2);

    grid.Children.Add(right);

    card.Child = grid;



    card.MouseLeftButtonUp += async (s,e) =>
    {
        openedFromModpacks = true;

        await OpenModpackDetails(pack);
    };

    ModpacksList.Children.Add(card);
}
    private async void ConfirmModpackInstall_Click(
        object sender,
        RoutedEventArgs e)
    {
        string loader =
            ((ComboBoxItem)ModpackLoaderBox.SelectedItem)
            .Content
            .ToString();

        string version =
            ((ComboBoxItem)ModpackVersionBox.SelectedItem)
            .Content
            .ToString();

        ModpackInstallOverlay.Visibility =
            Visibility.Collapsed;

        await InstallModpack(
            selectedModpack.project_id,
            loader,
            version);
    }
    
    private void CloseModpackInstall_Click(
        object sender,
        RoutedEventArgs e)
    {
        ModpackInstallOverlay.Visibility =
            Visibility.Collapsed;
    }
    private async Task OpenInstallModpackPopup(
        ModrinthProject pack)
    {
        try
        {
            selectedModpack = pack;

            ModpackLoaderBox.Items.Clear();
            ModpackVersionBox.Items.Clear();
            modpackLoaderVersions.Clear();

            using HttpClient client = new();

            string json =
                await client.GetStringAsync(
                    $"https://api.modrinth.com/v2/project/{pack.project_id}/version");

            var versions =
                JsonSerializer.Deserialize<List<ModrinthVersion>>(
                    json);

            if (versions == null || versions.Count == 0)
            {
                ShowToast("No versions found");
                return;
            }

            foreach (var version in versions)
            {
                foreach (string loader in version.loaders ?? [])
                {
                    string? displayLoader = loader.ToLowerInvariant() switch
                    {
                        "fabric" => "Fabric",
                        "forge" => "Forge",
                        "neoforge" => "NeoForge",
                        "vanilla" or "minecraft" => "Vanilla",
                        _ => null
                    };
                    if (displayLoader == null)
                        continue;

                    if (!modpackLoaderVersions.TryGetValue(
                            displayLoader,
                            out HashSet<string> supportedVersions))
                    {
                        supportedVersions = new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);
                        modpackLoaderVersions.Add(
                            displayLoader,
                            supportedVersions);
                    }

                    foreach (string mcVersion in version.game_versions ?? [])
                        supportedVersions.Add(mcVersion);
                }
            }



            if (modpackLoaderVersions.ContainsKey("Fabric"))
            {
                ModpackLoaderBox.Items.Add(
                    new ComboBoxItem
                    {
                        Content = "Fabric"
                    });
            }

            if (modpackLoaderVersions.ContainsKey("Forge"))
            {
                ModpackLoaderBox.Items.Add(
                    new ComboBoxItem
                    {
                        Content = "Forge"
                    });
            }

            if (modpackLoaderVersions.ContainsKey("NeoForge"))
            {
                ModpackLoaderBox.Items.Add(
                    new ComboBoxItem
                    {
                        Content = "NeoForge"
                    });
            }

            if (modpackLoaderVersions.ContainsKey("Vanilla"))
            {
                ModpackLoaderBox.Items.Add(
                    new ComboBoxItem
                    {
                        Content = "Vanilla"
                    });
            }

            if (ModpackLoaderBox.Items.Count == 0)
            {
                ShowToast(
                    "No supported loader versions available");

                return;
            }

            if (ModpackVersionBox.Items.Count == 0)
            {
                ShowToast(
                    "No supported Minecraft versions available");

                return;
            }

            ModpackLoaderBox.SelectedIndex = 0;
            UpdateModpackVersions();

            ModpackInstallOverlay.Visibility =
                Visibility.Visible;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private void ModpackLoader_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateModpackVersions();
    }

    private void UpdateModpackVersions()
    {
        if (ModpackVersionBox == null)
            return;

        ModpackVersionBox.Items.Clear();
        string? loader =
            (ModpackLoaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(loader) ||
            !modpackLoaderVersions.TryGetValue(
                loader,
                out HashSet<string> supportedVersions))
        {
            return;
        }

        foreach (ComboBoxItem item in EditVersionBox.Items)
        {
            string? version = item.Content?.ToString();
            if (!string.IsNullOrWhiteSpace(version) &&
                supportedVersions.Contains(version))
            {
                ModpackVersionBox.Items.Add(new ComboBoxItem
                {
                    Content = version
                });
            }
        }

        if (ModpackVersionBox.Items.Count > 0)
            ModpackVersionBox.SelectedIndex = 0;
    }

    private async Task OpenModpackDetails(
        ModrinthProject pack)
    {
        ContentBrowserPage.Visibility =
            Visibility.Collapsed;

        ModpacksPage.Visibility =
            Visibility.Collapsed;

        ContentDetailPage.Visibility =
            Visibility.Visible;

        DetailTitle.Text = pack.title;

        DetailIcon.Source =
            new BitmapImage(
                new Uri(pack.icon_url));

        using HttpClient client = new();

        string json =
            await client.GetStringAsync(
                $"https://api.modrinth.com/v2/project/{pack.project_id}");

        JsonDocument doc =
            JsonDocument.Parse(json);

        string body =
            doc.RootElement
                .GetProperty("body")
                .GetString() ?? "";

// Remove HTML images.
        body = Regex.Replace(
            body,
            @"<img[^>]*>",
            "",
            RegexOptions.IgnoreCase);

// Remove iframes.
        body = Regex.Replace(
            body,
            @"<iframe.*?</iframe>",
            "",
            RegexOptions.Singleline |
            RegexOptions.IgnoreCase);

// Remove all HTML tags.
        body = Regex.Replace(
            body,
            @"<[^>]+>",
            "",
            RegexOptions.IgnoreCase);

// Remove Markdown images.
        body = Regex.Replace(
            body,
            @"!\[.*?\]\(.*?\)",
            "");


        body = Regex.Replace(
            body,
            @"\[(.*?)\]\((.*?)\)",
            "$1");

// Remove reference markers.
        body = Regex.Replace(
            body,
            @"\[\d+\]",
            "");


        body = body.Replace("###", "");
        body = body.Replace("##", "");
        body = body.Replace("#", "");

// Remove remaining brackets.
        body = body.Replace("[", "");
        body = body.Replace("]", "");
        body = body.Replace(">", "");
// Remove bold and italic markers.
        body = body.Replace("***", "");
        body = body.Replace("**", "");
        body = body.Replace("*", "");

// Decode HTML entities.
        body = System.Net.WebUtility.HtmlDecode(body);


        body = Regex.Replace(
            body,
            @"(\r?\n){3,}",
            "\n\n");

        DetailDescription.Text =
            body.Trim();

        await LoadGallery(pack.project_id);
    }
    
    private async Task InstallModpack(
        string projectId,
        string selectedLoader,
        string selectedVersion)
    {
        StartTask("Downloading Modpack...");

        try
        {
            string mpFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource",
                "mp"
            );

            Directory.CreateDirectory(mpFolder);

            using HttpClient client = new();

            string json =
                await client.GetStringAsync(
                    $"https://api.modrinth.com/v2/project/{projectId}/version");

            var versions =
                JsonSerializer.Deserialize<List<ModrinthVersion>>(json);

            string modrinthLoader = selectedLoader.Equals(
                    "Vanilla",
                    StringComparison.OrdinalIgnoreCase)
                ? "minecraft"
                : selectedLoader.ToLowerInvariant();
            var latest =
                versions.FirstOrDefault(x =>
                    (x.loaders?.Any(loader =>
                        loader.Equals(
                            modrinthLoader,
                            StringComparison.OrdinalIgnoreCase) ||
                        (selectedLoader.Equals(
                             "Vanilla",
                             StringComparison.OrdinalIgnoreCase) &&
                         loader.Equals(
                             "vanilla",
                             StringComparison.OrdinalIgnoreCase))) == true)
                    &&
                    x.game_versions?.Contains(
                        selectedVersion,
                        StringComparer.OrdinalIgnoreCase) == true);
            if (latest == null)
            {
                ShowToast(
                    "No matching version found");

                return;
            }
            if (latest == null)
                return;

            var file =
                latest.files.FirstOrDefault();

            if (file == null)
                return;

            string savePath =
                Path.Combine(
                    mpFolder,
                    file.filename);

            byte[] data =
                await client.GetByteArrayAsync(
                    file.url);

            await File.WriteAllBytesAsync(
                savePath,
                data);

            await ImportMrPack(savePath);
        }
        catch (Exception ex)
        {
            ShowToast(
                "Modpack download failed");

            Console.WriteLine(ex);
        }
        finally
        {
            StopTask("Downloading Modpack...");
        }
    }    
    private async Task ImportMrPack(string mrpackFile)
{
    string taskName = "Importing Modpack...";

    StartTask(taskName);

    try
    {
        string tempFolder = Path.Combine(
            Path.GetTempPath(),
            "jax_mrpack_import");

        if (Directory.Exists(tempFolder))
            Directory.Delete(tempFolder, true);

        Directory.CreateDirectory(tempFolder);

        string tempZip =
            Path.Combine(
                tempFolder,
                "pack.zip");

        File.Copy(
            mrpackFile,
            tempZip,
            true);

        ZipFile.ExtractToDirectory(
            tempZip,
            tempFolder,
            true);

        string indexFile =
            Path.Combine(
                tempFolder,
                "modrinth.index.json");

        if (!File.Exists(indexFile))
        {
            ShowToast(
                "modrinth.index.json missing");

            return;
        }

        string json =
            File.ReadAllText(indexFile);

        JsonDocument doc =
            JsonDocument.Parse(json);

        string profileName =
            doc.RootElement
                .GetProperty("name")
                .GetString() ?? "Imported Modpack";

        string mcVersion =
            doc.RootElement
                .GetProperty("dependencies")
                .GetProperty("minecraft")
                .GetString() ?? "1.21.1";

        string loader = "";
        string loaderVersion = "";

        var deps =
            doc.RootElement
                .GetProperty("dependencies");

        if (deps.TryGetProperty("quilt-loader", out _))
        {
            throw new NotSupportedException(
                "Quilt modpacks are no longer supported.");
        }

        if (deps.TryGetProperty(
                "fabric-loader",
                out var fabric))
        {
            loader = "Fabric";

            loaderVersion =
                fabric.GetString() ?? "";
        }
        else if (deps.TryGetProperty(
                     "neoforge",
                     out var neoForge))
        {
            loader = "NeoForge";
            loaderVersion = neoForge.GetString() ?? "";
        }
        else if (deps.TryGetProperty(
                     "forge",
                     out var forge))
        {
            loader = "Forge";

            loaderVersion =
                forge.GetString() ?? "";
        }
        else
        {
            loader = "Vanilla";
        }

        string profileFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "Profiles",
                profileName);

        Directory.CreateDirectory(profileFolder);

        using HttpClient client = new();
        Dictionary<string, string> modDownloadUrls =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (var file in doc.RootElement
                     .GetProperty("files")
                     .EnumerateArray())
        {
            try
            {
                string relativePath =
                    file.GetProperty("path")
                        .GetString();

                string url =
                    file.GetProperty("downloads")[0]
                        .GetString();

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                string savePath =
                    Path.Combine(
                        profileFolder,
                        relativePath);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(savePath));

                byte[] data =
                    await client.GetByteArrayAsync(url);

                await File.WriteAllBytesAsync(
                    savePath,
                    data);

                if (relativePath.Replace('\\', '/')
                        .StartsWith("mods/", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetExtension(relativePath).Equals(
                        ".jar",
                        StringComparison.OrdinalIgnoreCase))
                {
                    modDownloadUrls[Path.GetFullPath(savePath)] = url;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        List<ContentItem> mods = new();
        List<ContentItem> resourcepacks = new();
        List<ContentItem> shaders = new();

        string modsFolder =
            Path.Combine(
                profileFolder,
                "mods");

        if (Directory.Exists(modsFolder))
        {
            foreach (var file in Directory.GetFiles(modsFolder))
            {
                string fileName =
                    Path.GetFileNameWithoutExtension(file);

                modDownloadUrls.TryGetValue(
                    Path.GetFullPath(file),
                    out string downloadUrl);

                ModrinthProject metadata =
                    await FindImportedModMetadataAsync(
                        client,
                        fileName,
                        downloadUrl);

                mods.Add(
                    new ContentItem
                    {
                        Name = fileName,
                        Description = metadata?.description ?? "",
                        IconUrl = metadata?.icon_url
                    });
            }
        }

        string rpFolder =
            Path.Combine(
                profileFolder,
                "resourcepacks");

        if (Directory.Exists(rpFolder))
        {
            foreach (var file in Directory.GetFiles(rpFolder))
            {
                resourcepacks.Add(
                    new ContentItem
                    {
                        Name = Path.GetFileNameWithoutExtension(file)
                    });
            }
        }

        string shaderFolder =
            Path.Combine(
                profileFolder,
                "shaderpacks");

        if (Directory.Exists(shaderFolder))
        {
            foreach (var file in Directory.GetFiles(shaderFolder))
            {
                
                    shaders.Add(
                        new ContentItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file)
                        });
            }
        }

        List<Profile> profiles;

        if (File.Exists(filePath))
        {
            profiles =
                JsonSerializer.Deserialize<List<Profile>>(
                    File.ReadAllText(filePath))
                ?? new List<Profile>();
        }
        else
        {
            profiles =
                new List<Profile>();
        }

        bool exists =
            profiles.Any(x =>
                NormalizeCompareName(x.Name) ==
                NormalizeCompareName(profileName));

        if (exists)
        {
            ShowToast(
                "Profile already exists");

            return;
        }

        profiles.Add(
            new Profile
            {
                Name = profileName,
                Loader = loader,
                LoaderVersion = loaderVersion,
                Version = mcVersion,
                ProfilePicture =
                    GetRandomProfilePicture(),
                Mods = mods,
                RP = resourcepacks,
                Shaders = shaders
            });

        File.WriteAllText(
            filePath,
            JsonSerializer.Serialize(
                profiles,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

        try
        {
            string mpFolder =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".JAXSTORE",
                    "JaxClient",
                    "resource",
                    "mp");

            if (Directory.Exists(mpFolder))
            {
                foreach (string file in Directory.GetFiles(mpFolder))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        LoadProfiles();
        LoadLastProfile();

        ShowToast(
            $"Imported {profileName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);

        ShowToast(
            "Modpack import failed");
    }
    finally
    {
        StopTask(taskName);
    }
}

private async Task<ModrinthProject> FindImportedModMetadataAsync(
    HttpClient client,
    string fileName,
    string downloadUrl)
{
    string projectId = null;

    if (Uri.TryCreate(downloadUrl, UriKind.Absolute, out Uri downloadUri) &&
        downloadUri.Host.Equals(
            "cdn.modrinth.com",
            StringComparison.OrdinalIgnoreCase))
    {
        string[] pathSegments =
            downloadUri.AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length >= 2 &&
            pathSegments[0].Equals(
                "data",
                StringComparison.OrdinalIgnoreCase))
        {
            projectId = pathSegments[1];
        }
    }

    if (!string.IsNullOrWhiteSpace(projectId))
    {
        try
        {
            string projectJson =
                await client.GetStringAsync(
                    $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(projectId)}");

            ModrinthProject project =
                JsonSerializer.Deserialize<ModrinthProject>(projectJson);

            if (!string.IsNullOrWhiteSpace(project?.title))
            {
                project.project_id ??= project.id;
                return project;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(
                $"Could not load Modrinth metadata for {fileName}: {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(
                $"Modrinth metadata request timed out for {fileName}: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"Could not parse Modrinth metadata for {fileName}: {ex.Message}");
        }
    }

    foreach (string query in GetImportedModSearchNames(fileName))
    {
        try
        {
            string searchUrl =
                $"https://api.modrinth.com/v2/search?limit=20" +
                $"&query={Uri.EscapeDataString(query)}" +
                $"&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

            string searchJson =
                await client.GetStringAsync(searchUrl);

            ModrinthResponse result =
                JsonSerializer.Deserialize<ModrinthResponse>(searchJson);

            string normalizedQuery =
                NormalizeCompareName(query);

            ModrinthProject match =
                result?.hits?.FirstOrDefault(project =>
                    NormalizeCompareName(project.title) == normalizedQuery ||
                    NormalizeCompareName(project.slug) == normalizedQuery);

            if (match != null)
                return match;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(
                $"Could not search Modrinth metadata for {fileName} using '{query}': {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(
                $"Modrinth metadata search timed out for {fileName} using '{query}': {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"Could not parse Modrinth search results for {fileName} using '{query}': {ex.Message}");
        }
    }

    return null;
}

private List<string> GetImportedModSearchNames(string fileName)
{
    string searchName =
        Path.GetFileNameWithoutExtension(fileName);

    searchName = Regex.Replace(
        searchName,
        @"\s*[\(\[]\s*\d+\s*[\)\]]\s*$",
        "");

    searchName = Regex.Replace(
        searchName,
        @"[\s._-]+$",
        "");

    List<string> searchNames = new();

    string originalSearchName =
        NormalizeName(searchName);

    if (!string.IsNullOrWhiteSpace(originalSearchName))
        searchNames.Add(originalSearchName);

    string withoutMinecraftVersion =
        Regex.Replace(
            searchName,
            @"(?<![a-z0-9])mc(?:[\s._-]*\d+(?:\.\d+)*)?(?![a-z0-9])",
            " ",
            RegexOptions.IgnoreCase);

    string fallbackSearchName =
        NormalizeName(withoutMinecraftVersion);

    if (!string.IsNullOrWhiteSpace(fallbackSearchName) &&
        !searchNames.Contains(
            fallbackSearchName,
            StringComparer.OrdinalIgnoreCase))
    {
        searchNames.Add(fallbackSearchName);
    }

    return searchNames;
}
    
    private async void SCSearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        currentSCSearch =
            SCSearchBox.Text;

        await LoadSCContent();
    }
    private async Task LoadSCContent()
    {
        SCBrowseList.Children.Clear();

        var loadedNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        string search =
            currentSCSearch ?? "";



        string facets;

        if (currentSCType == "mod")
        {
            facets = "[[\"project_type:mod\"]]";
        }
        else if (currentSCType == "resourcepack")
        {
            facets = "[[\"project_type:resourcepack\"]]";
        }
        else
        {
            facets = "[[\"project_type:shader\"]]";
        }

        using HttpClient client = new HttpClient();

        string url =
            $"https://api.modrinth.com/v2/search" +
            $"?limit=10" +
            $"&query={Uri.EscapeDataString(search)}" +
            $"&facets={facets}";

        string json =
            await client.GetStringAsync(url);

        var modrinth =
            JsonSerializer.Deserialize<ModrinthResponse>(
                json);

        if (modrinth?.hits != null)
        {
            var settings =
                LoadSettingsObject();

            foreach (var mod in modrinth.hits)
            {
                if (settings.StandardContent.Any(x =>
                        NormalizeName(x.Name) ==
                        NormalizeName(mod.title)))
                {
                    continue;
                }

                if (!loadedNames.Add(mod.title))
                    continue;

                AddSCBrowseCard(
                    mod.title,
                    mod.description,
                    "Modrinth",
                    mod.icon_url);
            }
        }



        string loader =
            currentProfile?.Loader?.ToLower() ?? "fabric";

        string version =
            currentProfile?.Version ?? "1.21.1";

        var curseForgeMods =
            await LoadCurseForgeMods(
                search,
                loader,
                version);

        
        foreach (var mod in curseForgeMods)
        {
            var settings =
                LoadSettingsObject();
            if (settings.StandardContent.Any(x =>
                    NormalizeName(x.Name) ==
                    NormalizeName(mod.name)))
            {
                continue;
            }

            if (!loadedNames.Add(mod.name))
                continue;

            AddSCBrowseCard(
                mod.name,
                mod.summary,
                "CurseForge",
                mod.logo?.url);
        }
    }
    private void CloseAddStandardContent_Click(
        object sender,
        RoutedEventArgs e)
    {
        AddStandardContentOverlay.Visibility =
            Visibility.Collapsed;
    }
    private void UpdateSCTabUI()
    {
        ModsSCBtn.Foreground =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#64748B"));

        RPackSCBtn.Foreground =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#64748B"));

        ShaderSCBtn.Foreground =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#64748B"));

        ModsSCLine.Visibility =
            Visibility.Collapsed;

        RPackSCLine.Visibility =
            Visibility.Collapsed;

        ShaderSCLine.Visibility =
            Visibility.Collapsed;

        switch (currentSCType)
        {
            case "mod":

                ModsSCBtn.Foreground =
                    Brushes.White;

                ModsSCLine.Visibility =
                    Visibility.Visible;

                break;

            case "resourcepack":

                RPackSCBtn.Foreground =
                    Brushes.White;

                RPackSCLine.Visibility =
                    Visibility.Visible;

                break;

            case "shader":

                ShaderSCBtn.Foreground =
                    Brushes.White;

                ShaderSCLine.Visibility =
                    Visibility.Visible;

                break;
        }
    }
private void AddSCBrowseCard(
    string name,
    string description,
    string platform,
    string iconUrl)
{
    Border card = new Border
    {
        Background =
            (Brush)new BrushConverter()
                .ConvertFromString("#1E293B"),

        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(12),
        Margin = new Thickness(0,0,0,10)
    };

    Grid grid = new Grid();

    grid.ColumnDefinitions.Add(
        new ColumnDefinition
        {
            Width = new GridLength(60)
        });

    grid.ColumnDefinitions.Add(
        new ColumnDefinition());

    grid.ColumnDefinitions.Add(
        new ColumnDefinition
        {
            Width = new GridLength(120)
        });

    Image icon = new Image
    {
        Width = 48,
        Height = 48,
        Margin = new Thickness(0,0,12,0)
    };

    ApplyRoundedImageCorners(icon);

    try
    {
        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            icon.Source =
                new BitmapImage(
                    new Uri(iconUrl));
        }
    }
    catch { }

    Grid.SetColumn(icon, 0);

    grid.Children.Add(icon);

    StackPanel textPanel =
        new StackPanel();

    TextBlock title = new TextBlock
    {
        Text = name,
        Foreground = Brushes.White,
        FontWeight = FontWeights.Bold,
        FontSize = 15
    };

    TextBlock desc = new TextBlock
    {
        Text = description,
        Foreground = Brushes.Gray,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 450,
        FontSize = 12
    };

    textPanel.Children.Add(title);
    textPanel.Children.Add(desc);

    Grid.SetColumn(textPanel, 1);

    grid.Children.Add(textPanel);

    StackPanel rightPanel =
        new StackPanel
        {
            VerticalAlignment =
                VerticalAlignment.Center
        };

    TextBlock badge = new TextBlock
    {
        Text = platform.ToUpper(),
        Foreground =
            platform == "Modrinth"
                ? Brushes.LimeGreen
                : Brushes.Orange,
        FontWeight = FontWeights.Bold,
        HorizontalAlignment =
            HorizontalAlignment.Center
    };

    
    var settings =
        LoadSettingsObject();

    CheckBox cb = new CheckBox
    {
        HorizontalAlignment =
            HorizontalAlignment.Center,

        VerticalAlignment =
            VerticalAlignment.Center,

        Margin = new Thickness(0,8,0,0),

        IsChecked =
            settings.StandardContent.Any(x =>
                NormalizeName(x.Name) ==
                NormalizeName(name)),

        Style =
            (Style)FindResource(
                "ExportCheckBox")
    };

    cb.Checked += (s, e) =>
    {
        var cfg =
            LoadSettingsObject();

        if (!cfg.StandardContent.Any(x =>
                NormalizeName(x.Name) ==
                NormalizeName(name)))
            
        {
            cfg.StandardContent.Add(
                new StandardContentItem
                {
                    Name = name,
                    Type = currentSCType,
                    Platform = platform
                });

            SaveSettingsObject(cfg);
        }


        ReloadStandardContentList();


        _ = LoadSCContent();
    };

    cb.Unchecked += (s, e) =>
    {
        var cfg =
            LoadSettingsObject();

        cfg.StandardContent.RemoveAll(x =>
            NormalizeName(x.Name) ==
            NormalizeName(name));

        SaveSettingsObject(cfg);

        ReloadStandardContentList();
    };
    
    rightPanel.Children.Add(badge);
    rightPanel.Children.Add(cb);

    Grid.SetColumn(rightPanel, 2);

    grid.Children.Add(rightPanel);

    card.Child = grid;

    SCBrowseList.Children.Add(card);
}

private class MatrixDrop
    {
        public TextBlock Block { get; set; }
        public double Speed { get; set; }
    }
    private void ManageStandardContent_Click(
        object sender,
        RoutedEventArgs e)
    {
        StandardContentOverlay.Visibility =
            Visibility.Visible;

        ReloadStandardContentList();
    }
    private List<string> standardContent =
        new();

    private void ReloadStandardContentList()
{
    StandardContentList.Children.Clear();

    var settings =
        LoadSettingsObject();

    foreach (var item in settings.StandardContent)
    {
        Border card = new Border
        {
            Background =
                (Brush)new BrushConverter()
                    .ConvertFromString("#1E293B"),

            CornerRadius =
                new CornerRadius(10),

            Padding =
                new Thickness(12),

            Margin =
                new Thickness(0, 0, 0, 10)
        };

        Grid grid = new Grid();

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(55)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(100)
            });

        Image icon = new Image
        {
            Width = 42,
            Height = 42,
            Stretch = Stretch.UniformToFill,
            Margin = new Thickness(0, 0, 12, 0)
        };

        ApplyRoundedImageCorners(icon);

        Border fallbackIcon = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(8),
            Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(
                        "#0F172A"))
        };

        fallbackIcon.Child = new TextBlock
        {
            Text = "?",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        Grid.SetColumn(fallbackIcon, 0);

        grid.Children.Add(fallbackIcon);

        TextBlock title = new TextBlock
        {
            Text = item.Name,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(title, 1);

        grid.Children.Add(title);

        Button deleteBtn = new Button
        {
            Content = "Delete",
            Width = 80,
            Height = 30,

            Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(
                        "#DC2626")),

            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };

        MakeRounded(deleteBtn);

        deleteBtn.Click += (s, e) =>
        {
            var cfg =
                LoadSettingsObject();

            cfg.StandardContent.Remove(item);

            SaveSettingsObject(cfg);

            ReloadStandardContentList();

            _ = LoadSCContent();
        };

        Grid.SetColumn(deleteBtn, 2);

        grid.Children.Add(deleteBtn);

        card.Child = grid;

        StandardContentList.Children.Add(card);



        _ = Task.Run(async () =>
        {
            try
            {
                string iconUrl =
                    await GetIconForInstalledContent(item.Name);

                if (!string.IsNullOrWhiteSpace(iconUrl))
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            icon.Source =
                                new BitmapImage(
                                    new Uri(iconUrl));

                            grid.Children.Remove(fallbackIcon);

                            Grid.SetColumn(icon, 0);

                            grid.Children.Add(icon);
                        }
                        catch
                        {
                        }
                    });
                }
            }
            catch
            {
            }
        });
    }
}
    
    private void CloseStandardContent_Click(
        object sender,
          RoutedEventArgs e)
    {
        StandardContentOverlay.Visibility =
            Visibility.Collapsed;
    }
    
    
    public class CurseForgeResponse
    {
        public List<CurseForgeMod> data { get; set; }
    }
    public class CurseForgeFilesResponse
    {
        public List<CurseForgeFile> data { get; set; }
    }

    private async Task OpenContentVersionPicker(
        string provider,
        string projectId,
        string name,
        string description,
        string iconUrl,
        ContentItem existingItem = null)
    {
        if (currentProfile == null ||
            string.IsNullOrWhiteSpace(projectId))
        {
            ShowToast("Select a profile before choosing a content version.");
            return;
        }

        contentVersionRequest = new ContentVersionRequest
        {
            Provider = provider,
            ProjectId = projectId,
            Name = name,
            Description = description,
            IconUrl = iconUrl,
            ContentType = currentTab,
            ExistingItem = existingItem
        };
        contentVersionOptions = new List<ContentVersionOption>();
        ContentVersionTitle.Text = name;
        string versionDescriptionTemplate = provider switch
        {
            "Modrinth" => "Modrinth • Minecraft {0} • {1}",
            "CurseForge" => "CurseForge • Minecraft {0} • {1}",
            _ => "Minecraft {0} • {1}"
        };
        ContentVersionDescription.Text =
            LocalizationManager.Instance.Format(
                versionDescriptionTemplate,
                currentProfile.Version,
                currentProfile.Loader);
        ContentVersionDetails.Text =
            LocalizationManager.Instance.Translate(
                "Loading compatible versions...");
        ContentVersionComboBox.ItemsSource = null;
        InstallContentVersionButton.IsEnabled = false;
        InstallContentVersionButton.Content =
            LocalizationManager.Instance.Translate("Loading...");
        InstallContentVersionButton.Tag = null;
        ContentVersionOverlay.Visibility = Visibility.Visible;

        try
        {
            using HttpClient client = new();
            if (provider.Equals("Modrinth", StringComparison.OrdinalIgnoreCase))
            {
                string url =
                    $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(projectId)}/version";
                string json = await client.GetStringAsync(url);
                List<ModrinthVersion> versions =
                    JsonSerializer.Deserialize<List<ModrinthVersion>>(json);
                IEnumerable<ModrinthVersion> compatible =
                    versions?.AsEnumerable() ??
                    Enumerable.Empty<ModrinthVersion>();

                if (contentVersionRequest.ContentType == "mod")
                {
                    compatible = compatible.Where(version =>
                        IsCompatibleModrinthVersion(
                            version,
                            contentVersionRequest.ContentType,
                            currentProfile.Version,
                            currentProfile.Loader));
                }
                else
                {
                    compatible = compatible.Where(version =>
                        IsCompatibleModrinthVersion(
                            version,
                            contentVersionRequest.ContentType,
                            currentProfile.Version,
                            currentProfile.Loader));
                }

                contentVersionOptions = compatible
                    .Select(version =>
                    {
                        ModrinthFile file =
                            version.files?.FirstOrDefault(candidate =>
                                candidate.primary) ??
                            version.files?.FirstOrDefault();
                        if (file == null ||
                            string.IsNullOrWhiteSpace(file.url))
                            return null;

                        string label =
                            string.IsNullOrWhiteSpace(version.version_number)
                                ? version.name
                                : version.version_number;
                        return new ContentVersionOption
                        {
                            Provider = "Modrinth",
                            ProjectId = projectId,
                            VersionId = version.id,
                            VersionName = label,
                            FileName = file.filename,
                            DownloadUrl = file.url,
                            DisplayName =
                                $"{label ?? "Version"}  •  {version.name ?? "Release"}",
                            SourceVersion = version
                        };
                    })
                    .Where(option => option != null)
                    .ToList();
            }
            else if (provider.Equals(
                         "CurseForge",
                         StringComparison.OrdinalIgnoreCase))
            {
                client.DefaultRequestHeaders.Add(
                    "x-api-key",
                    CurseForgeApiKey);
                string url =
                    $"https://api.curseforge.com/v1/mods/{Uri.EscapeDataString(projectId)}/files?pageSize=50&index=0";
                string json = await client.GetStringAsync(url);
                CurseForgeFilesResponse response =
                    JsonSerializer.Deserialize<CurseForgeFilesResponse>(json);
                List<CurseForgeFile> gameVersionFiles =
                    (response?.data ?? new List<CurseForgeFile>())
                    .Where(file =>
                        !string.IsNullOrWhiteSpace(file.downloadUrl) &&
                        file.gameVersions?.Any(gameVersion =>
                            gameVersion.Equals(
                                currentProfile.Version,
                                StringComparison.OrdinalIgnoreCase)) == true)
                    .ToList();
                List<CurseForgeFile> compatibleFiles =
                    contentVersionRequest.ContentType == "mod"
                        ? gameVersionFiles.Where(file =>
                            IsCompatibleCurseForgeFile(
                                file,
                                contentVersionRequest.ContentType,
                                currentProfile.Version,
                                currentProfile.Loader)).ToList()
                        : gameVersionFiles;

                contentVersionOptions = compatibleFiles
                    .Select(file => new ContentVersionOption
                    {
                        Provider = "CurseForge",
                        ProjectId = projectId,
                        VersionId = file.id.ToString(),
                        CurseForgeFileId = file.id,
                        VersionName = string.IsNullOrWhiteSpace(file.displayName)
                            ? file.fileName
                            : file.displayName,
                        FileName = file.fileName,
                        DownloadUrl = file.downloadUrl,
                        DisplayName = string.IsNullOrWhiteSpace(file.displayName)
                            ? file.fileName
                            : file.displayName,
                        SourceVersion = file
                    })
                    .ToList();
            }

            ContentVersionComboBox.ItemsSource = contentVersionOptions;
            ContentVersionComboBox.SelectedItem =
                contentVersionOptions.FirstOrDefault(option =>
                    string.Equals(
                        option.VersionId,
                        existingItem?.VersionId,
                        StringComparison.OrdinalIgnoreCase)) ??
                contentVersionOptions.FirstOrDefault();

            if (contentVersionOptions.Count == 0)
            {
                ContentVersionDetails.Text =
                    LocalizationManager.Instance.Translate(
                        "No compatible downloadable versions were found.");
                InstallContentVersionButton.Content =
                    LocalizationManager.Instance.Translate("Unavailable");
                ShowToast("No compatible content versions found.");
                return;
            }

            InstallContentVersionButton.Content =
                LocalizationManager.Instance.Translate(
                    existingItem == null ? "Install version" : "Switch version");
            InstallContentVersionButton.IsEnabled = true;
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is JsonException ||
            ex is TaskCanceledException ||
            ex is InvalidOperationException)
        {
            Console.WriteLine($"[Content Versions] Could not load versions: {ex}");
            ContentVersionDetails.Text =
                LocalizationManager.Instance.Translate(
                    "Could not load versions. Check your connection and try again.");
            InstallContentVersionButton.Content =
                LocalizationManager.Instance.Translate("Retry");
            InstallContentVersionButton.Tag = "retry";
            InstallContentVersionButton.IsEnabled = true;
            ShowToast("Could not load content versions.");
        }
    }

    private static bool IsCompatibleModrinthVersion(
        ModrinthVersion version,
        string contentType,
        string gameVersion,
        string loader)
    {
        if (version?.game_versions?.Contains(
                gameVersion,
                StringComparer.OrdinalIgnoreCase) != true)
            return false;

        return contentType != "mod" ||
               version.loaders?.Contains(
                   loader,
                   StringComparer.OrdinalIgnoreCase) == true;
    }

    private static bool IsCompatibleCurseForgeFile(
        CurseForgeFile file,
        string contentType,
        string gameVersion,
        string loader)
    {
        if (file?.gameVersions?.Any(version =>
                version.Equals(
                    gameVersion,
                    StringComparison.OrdinalIgnoreCase)) != true)
            return false;

        return contentType != "mod" ||
               file.gameVersions.Any(version =>
                   version.Contains(
                       loader,
                       StringComparison.OrdinalIgnoreCase));
    }

    private ContentItem FindExistingProfileContentItem(
        string provider,
        string projectId,
        string name)
    {
        List<ContentItem> items = currentTab switch
        {
            "mod" => currentProfile?.Mods,
            "resourcepack" => currentProfile?.RP,
            "shader" => currentProfile?.Shaders,
            _ => null
        };
        if (items == null)
            return null;

        return items.FirstOrDefault(item =>
                       string.Equals(
                           item.VersionProvider,
                           provider,
                           StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(
                           item.ProjectId,
                           projectId,
                           StringComparison.OrdinalIgnoreCase)) ??
               items.FirstOrDefault(item =>
                       string.Equals(
                           NormalizeName(item.Name),
                           NormalizeName(name),
                           StringComparison.OrdinalIgnoreCase));
    }

    private void ContentVersionComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (ContentVersionComboBox.SelectedItem is not ContentVersionOption option)
            return;

        ContentVersionDetails.Text =
            LocalizationManager.Instance.Format(
                "Version: {0}\nFile: {1}\nSource: {2}",
                option.VersionName,
                option.FileName,
                option.Provider);
    }

    private void CloseContentVersionOverlay_Click(
        object sender,
        RoutedEventArgs e)
    {
        ContentVersionOverlay.Visibility = Visibility.Collapsed;
        contentVersionRequest = null;
    }

    private async void InstallContentVersion_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (contentVersionRequest == null)
            return;
        if (ContentVersionComboBox.SelectedItem is not ContentVersionOption option)
        {
            if (InstallContentVersionButton.Tag?.ToString() == "retry")
            {
                ContentVersionRequest request = contentVersionRequest;
                await OpenContentVersionPicker(
                    request.Provider,
                    request.ProjectId,
                    request.Name,
                    request.Description,
                    request.IconUrl,
                    request.ExistingItem);
            }
            return;
        }

        InstallContentVersionButton.IsEnabled = false;
        InstallContentVersionButton.Content =
            LocalizationManager.Instance.Translate("Installing...");
        bool installed = false;

        try
        {
            if (option.Provider == "Modrinth")
            {
                installed = await InstallMod(
                    option.ProjectId,
                    contentVersionRequest.Name,
                    contentVersionRequest.Description,
                    option.VersionId,
                    contentVersionRequest.ExistingItem,
                    contentVersionRequest.IconUrl,
                    contentVersionRequest.ContentType,
                    option);
            }
            else if (option.Provider == "CurseForge" &&
                     int.TryParse(option.ProjectId, out int projectId))
            {
                CurseForgeMod mod = new()
                {
                    id = projectId,
                    name = contentVersionRequest.Name,
                    summary = contentVersionRequest.Description,
                    logo = string.IsNullOrWhiteSpace(contentVersionRequest.IconUrl)
                        ? null
                        : new CurseForgeLogo
                        {
                            url = contentVersionRequest.IconUrl
                        }
                };
                installed = await InstallCurseForgeMod(
                    mod,
                    null,
                    option.CurseForgeFileId,
                    contentVersionRequest.ExistingItem,
                    contentVersionRequest.ContentType,
                    option);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Content Versions] Installation failed: {ex}");
            ShowToast("Could not install the selected version.");
        }

        if (installed)
        {
            ContentVersionOverlay.Visibility = Visibility.Collapsed;
            contentVersionRequest = null;
            ReloadProfileData();
            LoadInstalledContent();
        }
        else
        {
            InstallContentVersionButton.Content =
                LocalizationManager.Instance.Translate(
                    contentVersionRequest?.ExistingItem == null
                        ? "Install version"
                        : "Switch version");
            InstallContentVersionButton.IsEnabled = true;
        }
    }

    private sealed class ContentVersionRequest
    {
        public string Provider { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string ContentType { get; set; }
        public ContentItem ExistingItem { get; set; }
    }

    private sealed class ContentVersionOption
    {
        public string Provider { get; set; }
        public string ProjectId { get; set; }
        public string VersionId { get; set; }
        public string VersionName { get; set; }
        public string FileName { get; set; }
        public string DownloadUrl { get; set; }
        public string DisplayName { get; set; }
        public int? CurseForgeFileId { get; set; }
        public object SourceVersion { get; set; }

        public override string ToString() => DisplayName;
    }

    private bool CheckIfInstalled(CurseForgeMod mod)
    {
        return IsContentInstalledByName(mod?.name);
    }

    private bool IsContentInstalledByName(string contentName)
    {
        if (currentProfile == null ||
            string.IsNullOrWhiteSpace(contentName))
            return false;

        string folder = GetCurrentFolder();
        string basePath = Path.Combine(GetCurrentProfileDirectory(), folder);
        if (!Directory.Exists(basePath))
            return false;

        string normalizedContentName = NormalizeName(contentName);
        if (string.IsNullOrWhiteSpace(normalizedContentName))
            return false;

        HashSet<string> installedNames =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (string path in Directory.GetFiles(basePath))
        {
            string fileName = Path.GetFileName(path);
            if (fileName.EndsWith(
                    ".disabled",
                    StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName[..^".disabled".Length];
            }

            string extension = Path.GetExtension(fileName);
            bool supportedExtension = currentTab switch
            {
                "mod" =>
                    string.Equals(extension, ".jar", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase),
                "resourcepack" or "shader" =>
                    string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
            if (!supportedExtension)
                continue;

            installedNames.Add(
                NormalizeName(Path.GetFileNameWithoutExtension(fileName)));
        }

        if (installedNames.Contains(normalizedContentName))
            return true;

        List<ContentItem> profileItems = currentTab switch
        {
            "mod" => currentProfile.Mods ?? new List<ContentItem>(),
            "resourcepack" => currentProfile.RP ?? new List<ContentItem>(),
            "shader" => currentProfile.Shaders ?? new List<ContentItem>(),
            _ => new List<ContentItem>()
        };
        return profileItems.Any(item =>
            item != null &&
            !string.IsNullOrWhiteSpace(item.MarketplaceName) &&
            !string.IsNullOrWhiteSpace(item.Name) &&
            string.Equals(
                NormalizeName(item.MarketplaceName),
                normalizedContentName,
                StringComparison.OrdinalIgnoreCase) &&
            installedNames.Contains(NormalizeName(item.Name)));
    }
private async Task<bool> InstallCurseForgeMod(
    CurseForgeMod mod,
    HashSet<int> dependencyChain = null,
    int? selectedFileId = null,
    ContentItem existingItem = null,
    string contentType = null,
    ContentVersionOption selectedOption = null,
    bool stableReleaseOnly = false)
{
    if (currentProfile == null ||
        mod == null ||
        mod.id <= 0)
        return false;

    string selectedContentType = contentType ?? currentTab;

    if (selectedContentType == "mod")
    {
        dependencyChain ??= new HashSet<int>();
        if (!dependencyChain.Add(mod.id))
            return true;
    }

    using HttpClient client = new HttpClient();

    client.DefaultRequestHeaders.Add(
        "x-api-key",
        CurseForgeApiKey);

    Console.WriteLine("========== CURSEFORGE INSTALL ==========");
    Console.WriteLine("MOD NAME: " + mod.name);
    Console.WriteLine("MOD ID: " + mod.id);

    string loaderType = currentProfile.Loader?.ToLowerInvariant() switch
    {
        "forge" => "1",
        "fabric" => "4",
        "neoforge" => "6",
        _ => "0"
    };
    string fileUrl =
        $"https://api.curseforge.com/v1/mods/{mod.id}/files?pageSize=50&index=0" +
        $"&gameVersion={Uri.EscapeDataString(currentProfile.Version ?? string.Empty)}";
    if (selectedContentType == "mod")
        fileUrl += $"&modLoaderType={loaderType}";

    Console.WriteLine("FILES API:");
    Console.WriteLine(fileUrl);

    string json;

    try
    {
        json = await client.GetStringAsync(fileUrl);

        Console.WriteLine("FILES API OK");
        Console.WriteLine("JSON LENGTH: " + json.Length);
    }
    catch (Exception ex)
    {
        Console.WriteLine("FILES API FAILED");
        Console.WriteLine(ex);

        ShowToast("Files API failed");

        return false;
    }

    var result =
        JsonSerializer.Deserialize<CurseForgeFilesResponse>(json);

    if (result?.data == null ||
        result.data.Count == 0)
    {
        ShowToast("No files found");
        return false;
    }

    Console.WriteLine("FILES FOUND: " + result.data.Count);

    string loader = currentProfile.Loader.ToLower();
    string mcVersion = currentProfile.Version;

    IEnumerable<CurseForgeFile> candidates = result.data;
    if (stableReleaseOnly)
    {
        candidates = candidates
            .Where(candidate => candidate.releaseType == 1)
            .OrderByDescending(candidate => candidate.id);
    }

    CurseForgeFile file = selectedFileId.HasValue
        ? result.data.FirstOrDefault(candidate =>
            candidate.id == selectedFileId.Value)
        : candidates.FirstOrDefault(candidate =>
            IsCompatibleCurseForgeFile(
                candidate,
                selectedContentType,
                mcVersion,
                loader));

    if (file == null ||
        !IsCompatibleCurseForgeFile(
            file,
            selectedContentType,
            mcVersion,
            loader))
    {
        ShowToast("No compatible file");
        return false;
    }

    if (selectedContentType == "mod" &&
        !await InstallRequiredCurseForgeDependenciesAsync(
            mod,
            dependencyChain,
            stableReleaseOnly))
    {
        return false;
    }

    Console.WriteLine("SELECTED FILE:");
    Console.WriteLine(file.fileName);

    Console.WriteLine("DOWNLOAD URL:");
    Console.WriteLine(file.downloadUrl);

    if (string.IsNullOrWhiteSpace(file.downloadUrl))
    {
        ShowToast("downloadUrl is null");

        Console.WriteLine("downloadUrl = NULL");

        return false;
    }

    byte[] data = null;

    for (int attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            Console.WriteLine(
                $"DOWNLOAD TRY {attempt}/3");

            data = await client.GetByteArrayAsync(
                file.downloadUrl);

            Console.WriteLine(
                $"DOWNLOAD OK (TRY {attempt})");

            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"DOWNLOAD FAILED (TRY {attempt})");

            Console.WriteLine(ex);

            if (attempt < 3)
            {
                Console.WriteLine(
                    "Retrying download in 1 second...");

                await Task.Delay(1000);
            }
            else
            {
                ShowToast(
                    "Download failed after 3 tries");

                return false;
            }
        }
    }

    string folder = selectedContentType switch
    {
        "mod" => "mods",
        "resourcepack" => "resourcepacks",
        "shader" or "shaderpack" => "shaderpacks",
        _ => "mods"
    };

    string modsFolder = Path.Combine(
        GetCurrentProfileDirectory(),
        folder);

    Directory.CreateDirectory(modsFolder);

    string ext = selectedContentType switch
    {
        "mod" => ".jar",
        "resourcepack" => ".zip",
        "shader" or "shaderpack" => ".zip",
        _ => ".jar"
    };

    string cleanName = mod.name;

    foreach (char c in Path.GetInvalidFileNameChars())
    {
        cleanName =
            cleanName.Replace(
                c.ToString(),
                "");
    }

    string fullPath =
        Path.Combine(
            modsFolder,
            cleanName + ext);

    Console.WriteLine("SAVE PATH:");
    Console.WriteLine(fullPath);

    File.WriteAllBytes(
        fullPath,
        data);

    DeleteReplacedContentFile(existingItem, selectedContentType, fullPath);

    if (new FileInfo(fullPath).Length == 0)
    {
        File.Delete(fullPath);

        

       

        string searchUrl =
            $"https://api.modrinth.com/v2/search?limit=1&query={Uri.EscapeDataString(mod.name)}";

        string fallbackJson =
            await client.GetStringAsync(searchUrl);

        var fallbackResult =
            JsonSerializer.Deserialize<ModrinthResponse>(
                fallbackJson);

        var modrinthMod =
            fallbackResult?.hits?.FirstOrDefault();

        return modrinthMod != null &&
               await InstallMod(
                   modrinthMod.project_id,
                   modrinthMod.title,
                   modrinthMod.description);
    }
    Console.WriteLine("FILE SAVED");
    AddToProfileJson(
        mod.name,
        mod.summary,
        mod.logo?.url,
        selectedContentType,
        "CurseForge",
        mod.id.ToString(),
        selectedOption?.VersionId ?? file.id.ToString(),
        selectedOption?.VersionName ??
            (string.IsNullOrWhiteSpace(file.displayName)
                ? file.fileName
                : file.displayName),
        file.fileName,
        existingItem);
    if (!suppressContentInstallUi)
    {
        ReloadProfileData();
        await LoadContent();
        ShowToast($"Installed {mod.name}");
    }
    return true;
}

private async Task<bool> InstallRequiredCurseForgeDependenciesAsync(
    CurseForgeMod parentMod,
    HashSet<int> dependencyChain,
    bool stableReleaseOnly = false)
{
    if (dependencyChain.Count > 100)
    {
        ShowToast("Too many nested required dependencies.");
        return false;
    }

    try
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add(
            "x-api-key",
            CurseForgeApiKey);

        string dependencyUrl =
            $"https://api.curseforge.com/v1/mods/{parentMod.id}/dependencies?pageSize=50";
        string dependencyJson =
            await client.GetStringAsync(dependencyUrl);

        using JsonDocument dependencyDocument =
            JsonDocument.Parse(dependencyJson);

        if (!dependencyDocument.RootElement.TryGetProperty(
                "data",
                out JsonElement dependencies) ||
            dependencies.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "CurseForge returned an invalid dependency list.");
        }

        foreach (JsonElement dependency in dependencies.EnumerateArray())
        {
            if (!dependency.TryGetProperty(
                    "relationType",
                    out JsonElement relationType) ||
                relationType.ValueKind != JsonValueKind.Number ||
                !relationType.TryGetInt32(out int relationTypeValue) ||
                relationTypeValue != 3 ||
                !dependency.TryGetProperty(
                    "modId",
                    out JsonElement modIdElement) ||
                !modIdElement.TryGetInt32(out int dependencyId) ||
                dependencyId <= 0 ||
                dependencyChain.Contains(dependencyId))
            {
                continue;
            }

            string projectUrl =
                $"https://api.curseforge.com/v1/mods/{dependencyId}";
            string projectJson =
                await client.GetStringAsync(projectUrl);
            CurseForgeModDetailsResponse project =
                JsonSerializer.Deserialize<CurseForgeModDetailsResponse>(
                    projectJson);

            if (string.IsNullOrWhiteSpace(project?.data?.name))
            {
                throw new InvalidDataException(
                    $"CurseForge did not return details for required dependency {dependencyId}.");
            }

            if (IsProfileModInstalled(project.data.name))
            {
                dependencyChain.Add(dependencyId);
                Console.WriteLine(
                    $"[Dependencies] Already installed: {project.data.name} (CurseForge {dependencyId}).");
                continue;
            }

            var requiredMod = new CurseForgeMod
            {
                id = project.data.id,
                name = project.data.name,
                summary = project.data.summary,
                logo = project.data.logo
            };

            Console.WriteLine(
                $"[Dependencies] Installing required CurseForge dependency: {requiredMod.name}.");

            bool installed =
                await InstallCurseForgeMod(
                    requiredMod,
                    dependencyChain,
                    stableReleaseOnly: stableReleaseOnly);

            if (!installed ||
                !IsProfileModInstalled(requiredMod.name))
            {
                ShowToast(
                    $"Could not install required dependency: {requiredMod.name}");
                return false;
            }
        }

        return true;
    }
    catch (Exception ex) when (
        ex is HttpRequestException ||
        ex is JsonException ||
        ex is InvalidDataException ||
        ex is TaskCanceledException ||
        ex is IOException ||
        ex is UnauthorizedAccessException)
    {
        Console.WriteLine(
            $"[Dependencies] CurseForge dependency check failed for {parentMod.name}: {ex}");
        ShowToast(
            $"Could not check required dependencies for {parentMod.name}.");
        return false;
    }
}

private bool IsProfileModInstalled(string modName)
{
    if (currentProfile == null ||
        string.IsNullOrWhiteSpace(modName))
    {
        return false;
    }

    string modsFolder = Path.Combine(
        GetCurrentProfileDirectory(),
        "mods");

    if (Directory.Exists(modsFolder))
    {
        foreach (string file in Directory.GetFiles(modsFolder))
        {
            string installedName = Path.GetFileName(file);

            foreach (string suffix in new[]
                     {
                         ".disabled",
                         ".jar",
                         ".zip"
                     })
            {
                if (installedName.EndsWith(
                        suffix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    installedName =
                        installedName[..^suffix.Length];
                }
            }

            if (NormalizeName(installedName) ==
                NormalizeName(modName))
            {
                return true;
            }
        }
    }

    return false;
}

public class CurseForgeFile
{
    public int id { get; set; }
    public string fileName { get; set; }
    public string displayName { get; set; }
    public string downloadUrl { get; set; }
    public int releaseType { get; set; }

    public List<string> gameVersions { get; set; }
}
    public class CurseForgeMod
    {
        public int id { get; set; }
        public string name { get; set; }
        public string summary { get; set; }
        public CurseForgeLogo logo { get; set; }
    }

    public class CurseForgeLogo
    {
        public string url { get; set; }
    }

    private async Task<List<CurseForgeMod>> LoadCurseForgeMods(
        string search,
        string loader,
        string version)
    {
        if (currentTab != "resourcepack" &&
            (loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) ||
             loader.Equals("minecraft", StringComparison.OrdinalIgnoreCase)))
        {
            return new List<CurseForgeMod>();
        }

        using HttpClient client = new HttpClient();

        client.DefaultRequestHeaders.Add(
            "x-api-key",
            CurseForgeApiKey);

        int modLoaderType = 0;

        if (loader.Equals("fabric", StringComparison.OrdinalIgnoreCase))
            modLoaderType = 4;
        else if (loader.Equals("forge", StringComparison.OrdinalIgnoreCase))
            modLoaderType = 1;
        else if (loader.Equals("neoforge", StringComparison.OrdinalIgnoreCase))
            modLoaderType = 6;
        int classId = 6; // Mod category.

        if (currentTab == "resourcepack")
            classId = 12;

        string url =
            $"https://api.curseforge.com/v1/mods/search" +
            $"?gameId=432" +
            $"&classId={classId}" +
            $"&searchFilter={Uri.EscapeDataString(search)}" +
            $"&gameVersion={Uri.EscapeDataString(version)}" +
            $"&modLoaderType={modLoaderType}" +
            $"&pageSize=10";

        Console.WriteLine("CURSEFORGE URL:");
        Console.WriteLine(url);

        string json = null;

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
               
                

                json = await client.GetStringAsync(url);

                
                
                break;
            }
            catch (Exception ex)
            {
               
                
                Console.WriteLine(ex);

                if (attempt < 3)
                {
                   
                    

                    await Task.Delay(1000);
                }
                else
                {
                    
                    
                    ShowToast(
                        "CurseForge unavailable");

                    return new List<CurseForgeMod>();
                }
            }
        }

        var result =
            JsonSerializer.Deserialize<CurseForgeResponse>(json);

        return result?.data ??
               new List<CurseForgeMod>();
    }    
    private void ImportFromLink_Click(
        object sender,
        RoutedEventArgs e)
    {
        ImportPopup.IsOpen = false;

        ImportLinkBox.Text = "";

        ImportLinkOverlay.Visibility =
            Visibility.Visible;
    }
    private void ImportLinkCancel_Click(
        object sender,
        RoutedEventArgs e)
    {
        ImportLinkOverlay.Visibility =
            Visibility.Collapsed;
    }
    private async void ImportLinkConfirm_Click(
        object sender,
        RoutedEventArgs e)
    {
        const string downloadTaskName = "Downloading pack from link...";
        string phase = "validating link";
        string? tempFile = null;
        bool downloadTaskStarted = false;
        bool downloadCompleted = false;

        try
        {
            string link =
                ImportLinkBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(link))
            {
                ShowToast("Enter a link");
                return;
            }

            if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowToast(
                    LocalizationManager.Instance.Translate(
                        "Enter a valid HTTP or HTTPS link."));
                return;
            }

            Console.WriteLine(
                $"[JXCpack Import] Starting download from host '{uri.Host}'.");
            ImportLinkOverlay.Visibility = Visibility.Collapsed;
            StartTask(downloadTaskName);
            downloadTaskStarted = true;

            phase = "preparing download";
            string resourceFolder =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".JAXSTORE",
                    "JaxClient",
                    "resource");
    
            Directory.CreateDirectory(
                resourceFolder);

            tempFile =
                Path.Combine(
                    resourceFolder,
                    Guid.NewGuid() + ".jxcpack");

            using HttpClient client = new();

            phase = "requesting pack";
            var timer = System.Diagnostics.Stopwatch.StartNew();
            using HttpResponseMessage response =
                await client.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead);

            long? expectedBytes = response.Content.Headers.ContentLength;
            Console.WriteLine(
                $"[JXCpack Import] HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}; expected bytes: " +
                $"{expectedBytes?.ToString() ?? "unknown"}.");
            response.EnsureSuccessStatusCode();

            phase = "streaming pack to disk";
            await using Stream input =
                await response.Content.ReadAsStreamAsync();
            await using FileStream output = new(
                tempFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            byte[] buffer = new byte[81920];
            long downloadedBytes = 0;
            long nextProgressLog = 16 * 1024 * 1024;
            while (true)
            {
                int bytesRead = await input.ReadAsync(buffer);
                if (bytesRead == 0)
                break;

                await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;

                if (downloadedBytes >= nextProgressLog)
                {
                Console.WriteLine(
                    $"[JXCpack Import] Downloaded {downloadedBytes} bytes " +
                    $"in {timer.Elapsed.TotalSeconds:0.0}s.");
                nextProgressLog += 16 * 1024 * 1024;
                }
            }

            if (expectedBytes.HasValue &&
                downloadedBytes != expectedBytes.Value)
            {
                throw new IOException(
                $"The pack download was incomplete ({downloadedBytes} of " +
                $"{expectedBytes.Value} bytes received).");
            }

            await output.FlushAsync();
            downloadCompleted = true;
            Console.WriteLine(
                $"[JXCpack Import] Download completed: {downloadedBytes} bytes " +
                $"in {timer.Elapsed.TotalSeconds:0.0}s.");

            StopTask(downloadTaskName);
            downloadTaskStarted = false;

            phase = "importing downloaded pack";
            await ImportJaxProfile(tempFile);
        }
        catch (Exception ex)
        {
            string safeException = ex.ToString();
            if (tempFile is not null)
                safeException = safeException.Replace(
                tempFile,
                "[temporary pack file]",
                StringComparison.OrdinalIgnoreCase);

            Console.WriteLine(
                $"[JXCpack Import] Failed while {phase}: {safeException}");

            if (!downloadCompleted &&
                tempFile is not null &&
                File.Exists(tempFile))
            {
                try
                {
                File.Delete(tempFile);
                }
                catch (Exception cleanupException) when (
                cleanupException is IOException ||
                cleanupException is UnauthorizedAccessException)
                {
                Console.WriteLine(
                    $"[JXCpack Import] Could not remove partial download: " +
                    cleanupException.Message);
                }
            }

            ShowToast(
                LocalizationManager.Instance.Format(
                "Import from link failed: {0}",
                ex.Message));
        }
        finally
        {
            if (downloadTaskStarted)
                StopTask(downloadTaskName);
        }
    }
    private async void ImportBrowse_Click(object sender, RoutedEventArgs e)
    {
        ImportPopup.IsOpen = false;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "All Packs (*.jxcpack;*.noriskpack;*.mrpack)|*.jxcpack;*.noriskpack;*.mrpack"
        };

        if (dialog.ShowDialog() != true)
            return;

        string file = dialog.FileName;

        string ext = Path.GetExtension(file).ToLower();

        if (ext == ".jxcpack")
        {
            await ImportJaxProfile(file);
        }
        else if (ext == ".noriskpack")
        {
            await ImportNoriskProfile(file);
        }
        else if (ext == ".mrpack")
        {
            await ImportMrPack(file);
        }
        else
        {
            ShowToast("Unsupported file type");
        }
    }

    private void UpdateOfflineIcon()
    {
       

        if (IsOfflineMode)
        {
            OfflineImage.Visibility = Visibility.Visible;

            AccountImage.Visibility = Visibility.Collapsed;
            AccountIcon.Visibility = Visibility.Collapsed;

           
        }
        else
        {
            OfflineImage.Visibility = Visibility.Collapsed;

            if (AccountImage.Source != null)
            {
                AccountImage.Visibility = Visibility.Visible;
                AccountIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                AccountImage.Visibility = Visibility.Collapsed;
                AccountIcon.Visibility = Visibility.Visible;
            }

            
        }
    }
    
    private async Task ImportJaxProfile(string filePath)
{
    string profileName = Path.GetFileNameWithoutExtension(filePath);
    string targetPath = "";

    await Task.Run(() =>
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StartTask("Importing JaxClient Pack...");
        });

        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient"
        );

        string profilesBase = Path.Combine(basePath, "Profiles");
        Directory.CreateDirectory(profilesBase);

        targetPath = Path.Combine(profilesBase, profileName);

        int counter = 1;
        while (Directory.Exists(targetPath))
        {
            targetPath = Path.Combine(profilesBase, $"{profileName} ({counter})");
            counter++;
        }

        Directory.CreateDirectory(targetPath);


        System.IO.Compression.ZipFile.ExtractToDirectory(filePath, targetPath);

        Application.Current.Dispatcher.Invoke(() =>
        {
            StopTask("Importing JaxClient Pack...");
        });
    });

    // Read metadata.
    string metaPath = Path.Combine(targetPath, "profilemeta.json");

    string loader = "Imported";
    string version = "Unknown";
    string loaderVersion = "Unknown";
    bool nrcEnabled = false;

    if (File.Exists(metaPath))
    {
        try
        {
            string json = File.ReadAllText(metaPath);

            using JsonDocument metaDocument = JsonDocument.Parse(json);
            JsonElement meta = metaDocument.RootElement;

            if (meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("Loader", out JsonElement loaderElement) &&
                    loaderElement.ValueKind == JsonValueKind.String)
                    loader = loaderElement.GetString() ?? loader;

                if (meta.TryGetProperty(
                        "MinecraftVersion",
                        out JsonElement minecraftVersionElement) &&
                    minecraftVersionElement.ValueKind == JsonValueKind.String)
                    version = minecraftVersionElement.GetString() ?? version;
                else if (meta.TryGetProperty(
                             "Version",
                             out JsonElement versionElement) &&
                         versionElement.ValueKind == JsonValueKind.String)
                    version = versionElement.GetString() ?? version;

                if (meta.TryGetProperty(
                        "LoaderVersion",
                        out JsonElement loaderVersionElement) &&
                    loaderVersionElement.ValueKind == JsonValueKind.String)
                    loaderVersion =
                        loaderVersionElement.GetString() ?? loaderVersion;

                if (meta.TryGetProperty(
                        "NrcEnabled",
                        out JsonElement nrcEnabledElement) &&
                    (nrcEnabledElement.ValueKind == JsonValueKind.True ||
                     nrcEnabledElement.ValueKind == JsonValueKind.False))
                    nrcEnabled = nrcEnabledElement.GetBoolean();
            }

            Console.WriteLine(
                $"[JXCpack Import] Applying Minecraft version '{version}', " +
                $"loader '{loader}' version '{loaderVersion}'.");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[JXCpack Import] Invalid profile metadata: {ex}");
            ShowToast("The pack has invalid version metadata.");
        }
    }

    string contentManifestPath =
        Path.Combine(targetPath, "content-manifest.json");

    if (File.Exists(contentManifestPath))
    {
        StartTask("Restoring marketplace content...");
        int restoreFailures = 0;

        try
        {
            restoreFailures =
                await RestoreJxcPackMarketplaceContentAsync(
                    targetPath,
                    contentManifestPath,
                    loader,
                    version);
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine(ex);
            ShowToast("The JXCpack marketplace manifest is invalid.");
        }
        finally
        {
            StopTask("Restoring marketplace content...");
        }

        if (restoreFailures > 0)
        {
            ShowToast(
                $"Could not restore {restoreFailures} marketplace item(s). Check the app log.");
        }
    }

    // Convert mods to ContentItem objects.
    List<ContentItem> mods = new List<ContentItem>();
    string modsFolder = Path.Combine(targetPath, "mods");

    if (Directory.Exists(modsFolder))
    {
        foreach (var file in Directory.GetFiles(modsFolder, "*.jar"))
        {
            mods.Add(new ContentItem
            {
                Name = Path.GetFileName(file)
            });
        }
    }

    // Convert resource packs to ContentItem objects.
    List<ContentItem> resourcepacks = new List<ContentItem>();
    string rpFolder = Path.Combine(targetPath, "resourcepacks");

    if (Directory.Exists(rpFolder))
    {
        foreach (var file in Directory.GetFiles(rpFolder))
        {
            resourcepacks.Add(new ContentItem
            {
                Name = Path.GetFileName(file)
            });
        }
    }

    // Convert shader packs to ContentItem objects.
    List<ContentItem> shaderpacks = new List<ContentItem>();
    string shaderFolder = Path.Combine(targetPath, "shaderpacks");

    if (Directory.Exists(shaderFolder))
    {
        foreach (var file in Directory.GetFiles(shaderFolder))
        {
            shaderpacks.Add(new ContentItem
            {
                Name = Path.GetFileName(file)
            });
        }
    }


    string mainFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient"
    );

    string jsonPath = Path.Combine(mainFolder, "profiles.json");

    List<Profile> profiles;

    if (File.Exists(jsonPath))
    {
        string json = File.ReadAllText(jsonPath);
        profiles = JsonSerializer.Deserialize<List<Profile>>(json)
                   ?? new List<Profile>();
    }
    else
    {
        profiles = new List<Profile>();
    }


    var profile = new Profile
    {
        Name = Path.GetFileName(targetPath),
        Loader = loader,
        Version = version,
        LoaderVersion = loaderVersion,
        NrcEnabled = nrcEnabled,
        ProfilePicture = GetRandomProfilePicture(),
        Mods = mods,
        RP = resourcepacks,
        Shaders = shaderpacks
    };

    profiles.Add(profile);

    string output = JsonSerializer.Serialize(
        profiles,
        new JsonSerializerOptions { WriteIndented = true }
    );

    File.WriteAllText(jsonPath, output);


    LoadProfiles();

    ShowToast($"Imported Profile");
}    List<ContentItem> installedMods = new List<ContentItem>();

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (currentProfile == null)
            return;

        LoadExportItems(currentProfile);
        ExportOverlay.Visibility = Visibility.Visible;
    }
    
    private void LoadExportItems(Profile profile)
    {
        ExportList.Children.Clear();
        optionalPackSettingsUnselected = false;

        string basePath = GetProfileDirectory(profile);

        if (!Directory.Exists(basePath))
            return;


        foreach (var dir in Directory.GetDirectories(basePath))
        {
            string name = Path.GetFileName(dir);

            var cb = new CheckBox
            {
                Content = name,
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 0),
                Tag = dir,

                Style =
                    (Style)FindResource(
                        "ExportCheckBox"),

                Cursor = Cursors.Hand
            };

            cb.Checked += (_, __) => UpdateExportButtonState();
            cb.Unchecked += (_, __) => UpdateExportButtonState();

            ExportList.Children.Add(cb);
        }


        foreach (var file in Directory.GetFiles(basePath))
        {
            string name = Path.GetFileName(file);

            var cb = new CheckBox
            {
                Content = name,
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 5, 0, 0),
                Tag = file,

                Style =
                    (Style)FindResource(
                        "ExportCheckBox"),

                Cursor = Cursors.Hand
            };


            cb.Checked += (_, __) => UpdateExportButtonState();
            cb.Unchecked += (_, __) => UpdateExportButtonState();

            ExportList.Children.Add(cb);
        }

        UpdateExportSelectionMode();


        allSelected = true;
        SelectAllBtn.Content = "Deselect All";

        UpdateExportButtonState();
    }    
    
    private void ExportCancel_Click(object sender, RoutedEventArgs e)
    {
        ExportOverlay.Visibility = Visibility.Collapsed;
    }
    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        ImportPopup.IsOpen = !ImportPopup.IsOpen;
    }

    private (Window Dialog, StackPanel Content) CreatePackPopupWindow(
        string title,
        string subtitle,
        double width,
        double height)
    {
        Window dialog = new()
        {
            Width = width,
            Height = height,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            Background = Brushes.Transparent
        };

        StackPanel layout = new();
        Grid headerGrid = new()
        {
            Margin = new Thickness(20, 16, 14, 16)
        };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        StackPanel titlePanel = new();
        titlePanel.Children.Add(new TextBlock
        {
            Text = LocalizationManager.Instance.Translate(title),
            Foreground = Brushes.White,
            FontSize = 19,
            FontWeight = FontWeights.Bold
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = LocalizationManager.Instance.Translate(subtitle),
            Foreground = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 8, 0)
        });
        headerGrid.Children.Add(titlePanel);

        Button closeButton = new()
        {
            Content = new Border
            {
                Background = new SolidColorBrush(
                    Color.FromRgb(30, 64, 115)),
                CornerRadius = new CornerRadius(10),
                Child = new TextBlock
                {
                    Text = "×",
                    Foreground = Brushes.White,
                    FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            },
            Width = 34,
            Height = 34,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top,
            Template = CreatePackPopupButtonTemplate(
                new CornerRadius(10))
        };
        closeButton.Click += (_, _) => dialog.Close();
        Grid.SetColumn(closeButton, 1);
        headerGrid.Children.Add(closeButton);

        Border header = new()
        {
            Background = new LinearGradientBrush(
                Color.FromRgb(37, 99, 235),
                Color.FromRgb(29, 78, 216),
                0),
            CornerRadius = new CornerRadius(17, 17, 0, 0),
            Child = headerGrid
        };
        header.MouseLeftButtonDown += (_, args) =>
        {
            if (args.ButtonState == MouseButtonState.Pressed &&
                !closeButton.IsMouseOver)
                dialog.DragMove();
        };
        layout.Children.Add(header);

        StackPanel content = new()
        {
            Margin = new Thickness(22, 18, 22, 22)
        };
        layout.Children.Add(content);

        dialog.Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(11, 18, 32)),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(0),
            Child = layout
        };
        LocalizationManager.Instance.ApplyToVisualTree(dialog);
        return (dialog, content);
    }

    private PackUploadChoice? ShowPackUploadActionDialog(
        IReadOnlyList<WebsitePackSummary> packs,
        bool freeSlotAvailable,
        int availableCredits,
        string fileName,
        long fileSize)
    {
        bool hasExistingPacks = packs.Count > 0;
        bool isFirstFreeSlot = !hasExistingPacks && freeSlotAvailable;
        bool freeSlotUsed = !hasExistingPacks && !freeSlotAvailable;
        (Window dialog, StackPanel content) =
            CreatePackPopupWindow(
                "Pack upload",
                "Choose how to use your upload slot.",
                590,
                hasExistingPacks ? 500 : 390);

        Border fileDetails = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 14)
        };
        StackPanel fileDetailContent = new();
        fileDetailContent.Children.Add(new TextBlock
        {
            Text = LocalizationManager.Instance.Translate("Pack file"),
            Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        });
        fileDetailContent.Children.Add(new TextBlock
        {
            Text = fileName,
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = fileName,
            Margin = new Thickness(0, 4, 0, 2)
        });
        fileDetailContent.Children.Add(new TextBlock
        {
            Text = LocalizationManager.Instance.Format(
                "File size: {0}",
                FormatBytes(fileSize)),
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 12
        });
        fileDetailContent.Children.Add(new TextBlock
        {
            Text = LocalizationManager.Instance.Translate(
                "Maximum upload size: 249 MB"),
            Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        });
        fileDetails.Child = fileDetailContent;
        content.Children.Add(fileDetails);

        content.Children.Add(new TextBlock
        {
            Text = LocalizationManager.Instance.Translate(
                hasExistingPacks
                    ? "Replace a selected pack for free, or purchase an additional slot for 3 credits."
                    : isFirstFreeSlot
                        ? "Your first slot is free. No credits will be charged."
                        : "Your free slot has already been used. Another slot costs 3 credits."),
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        Border slotDetails = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(23, 37, 62)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 14)
        };
        slotDetails.Child = new TextBlock
        {
            Text = LocalizationManager.Instance.Format(
                "Pack slots in use: {0}",
                packs.Count) + "  •  " +
                LocalizationManager.Instance.Translate(
                    isFirstFreeSlot
                        ? "First slot: free"
                        : hasExistingPacks
                            ? "Replace: free; new slot: 3 credits"
                            : freeSlotUsed
                                ? "New slot: 3 credits"
                                : "Replacement: free") + "  •  " +
                LocalizationManager.Instance.Format(
                    "Available credits: {0}",
                    availableCredits),
            Foreground = new SolidColorBrush(Color.FromRgb(219, 234, 254)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(slotDetails);

        if (hasExistingPacks)
        {
            content.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Instance.Translate(
                    "Select the pack slot to replace:"),
                Foreground = new SolidColorBrush(
                    Color.FromRgb(147, 197, 253)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            });
        }

        WebsitePackSummary? selectedPack =
            hasExistingPacks ? packs[0] : null;
        StackPanel packOptions = new()
        {
            Orientation = Orientation.Vertical
        };
        List<(WebsitePackSummary Pack, Border Card, TextBlock Marker)> packCards = new();
        if (hasExistingPacks)
        {
            foreach (WebsitePackSummary pack in packs)
            {
                Border card = new()
                {
                    Background = new SolidColorBrush(
                        pack == selectedPack
                            ? Color.FromRgb(23, 49, 91)
                            : Color.FromRgb(15, 23, 42)),
                    BorderBrush = new SolidColorBrush(
                        pack == selectedPack
                            ? Color.FromRgb(59, 130, 246)
                            : Color.FromRgb(51, 65, 85)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                Grid packRow = new();
                packRow.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = GridLength.Auto
                });
                packRow.ColumnDefinitions.Add(new ColumnDefinition());

                TextBlock marker = new()
                {
                    Text = pack == selectedPack ? "✓" : "",
                    Foreground = new SolidColorBrush(
                        Color.FromRgb(147, 197, 253)),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 24
                };
                packRow.Children.Add(marker);

                TextBlock name = new()
                {
                    Text = pack.Name,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = pack.Name,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(name, 1);
                packRow.Children.Add(name);
                card.Child = packRow;

                Button packOption = new()
                {
                    Content = card,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Cursor = Cursors.Hand,
                    Template = CreatePackPopupButtonTemplate(
                        new CornerRadius(9))
                };
                packOption.Click += (_, _) =>
                {
                    selectedPack = pack;
                    foreach ((WebsitePackSummary item, Border itemCard, TextBlock itemMarker)
                             in packCards)
                    {
                        bool selected =
                            string.Equals(
                                item.Id,
                                pack.Id,
                                StringComparison.Ordinal);
                        itemCard.Background = new SolidColorBrush(
                            selected
                                ? Color.FromRgb(23, 49, 91)
                                : Color.FromRgb(15, 23, 42));
                        itemCard.BorderBrush = new SolidColorBrush(
                            selected
                                ? Color.FromRgb(59, 130, 246)
                                : Color.FromRgb(51, 65, 85));
                        itemMarker.Text = selected ? "✓" : "";
                    }
                };

                packCards.Add((pack, card, marker));
                packOptions.Children.Add(packOption);
            }
        }

        ScrollViewer packOptionsScroll = new()
        {
            Content = packOptions,
            Visibility = hasExistingPacks
                ? Visibility.Visible
                : Visibility.Collapsed,
            MaxHeight = 116,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 0, 0, 14)
        };
        content.Children.Add(packOptionsScroll);

        bool canPurchasePaidSlot =
            isFirstFreeSlot || availableCredits >= 3;
        TextBlock insufficientCreditsHint = new()
        {
            Text = LocalizationManager.Instance.Translate(
                "Not enough credits for a new pack slot (3 credits required)."),
            Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, -6, 0, 10),
            Visibility = !canPurchasePaidSlot
                ? Visibility.Visible
                : Visibility.Collapsed
        };
        content.Children.Add(insufficientCreditsHint);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button cancelButton = CreatePackPopupActionButton(
            "Cancel",
            "#334155");
        cancelButton.Margin = new Thickness(0, 0, 8, 0);
        cancelButton.Click += (_, _) => dialog.Close();
        actions.Children.Add(cancelButton);

        if (hasExistingPacks)
        {
            Button replaceButton = CreatePackPopupActionButton(
                "Replace selected",
                "#2563EB");
            replaceButton.Margin = new Thickness(0, 0, 8, 0);
            replaceButton.Click += (_, _) =>
            {
                if (selectedPack is not WebsitePackSummary pack)
                    return;

                dialog.Tag = new PackUploadChoice
                {
                    Mode = "replace",
                    PackId = pack.Id
                };
                dialog.Close();
            };
            actions.Children.Add(replaceButton);
        }

        Button buyButton = CreatePackPopupActionButton(
            isFirstFreeSlot
                ? "Create free first slot"
                : canPurchasePaidSlot
                    ? "Buy another slot — 3 credits"
                    : "Not enough credits",
            canPurchasePaidSlot ? "#2563EB" : "#334155");
        buyButton.IsEnabled = canPurchasePaidSlot;
        buyButton.Click += (_, _) =>
        {
            dialog.Tag = new PackUploadChoice
            {
                Mode = "new"
            };
            dialog.Close();
        };
        actions.Children.Add(buyButton);
        content.Children.Add(actions);

        dialog.ShowDialog();
        return dialog.Tag as PackUploadChoice;
    }

    private void ShowPackUploadSuccessDialog(
        string link,
        string fileName)
    {
        (Window dialog, StackPanel content) =
            CreatePackPopupWindow(
                "Pack uploaded",
                "Your share link is ready.",
                560,
                330);
        content.Children.Add(new TextBlock
        {
            Text = fileName,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = fileName,
            Margin = new Thickness(0, 0, 0, 6)
        });
        content.Children.Add(new TextBlock
        {
            Text = LocalizationManager.Instance.Translate(
                "Anyone with this link can download your pack."),
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });

        TextBox linkBox = new()
        {
            Text = link,
            IsReadOnly = true,
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            BorderThickness = new Thickness(1),
            CaretBrush = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        content.Children.Add(linkBox);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button closeButton = CreatePackPopupActionButton("Close", "#1E293B");
        closeButton.Margin = new Thickness(0, 0, 8, 0);
        closeButton.Click += (_, _) => dialog.Close();
        actions.Children.Add(closeButton);

        Button copyButton = CreatePackPopupActionButton("Copy link", "#2563EB");
        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(link);
                ShowToast(
                    LocalizationManager.Instance.Translate(
                        "Pack link copied to clipboard."));
                dialog.Close();
            }
            catch (Exception ex) when (
                ex is System.Runtime.InteropServices.ExternalException ||
                ex is System.Threading.ThreadStateException)
            {
                Console.WriteLine(
                    $"Could not copy uploaded JaxClient pack link: {ex}");
                ShowToast(
                    LocalizationManager.Instance.Format(
                        "Could not copy link: {0}",
                        ex.Message));
            }
        };
        actions.Children.Add(copyButton);
        content.Children.Add(actions);

        dialog.ShowDialog();
    }

    private static string FormatBytes(long bytes)
    {
        double megabytes = bytes / (1024d * 1024d);
        return $"{megabytes:0.0} MB";
    }

    private async void ExportConfirm_Click(object sender, RoutedEventArgs e)
    {
        ExportOverlay.Visibility = Visibility.Collapsed;

        if (currentProfile == null)
            return;

        // Copy UI data safely.
        var selectedItems = new List<string>();

        foreach (CheckBox cb in ExportList.Children)
        {
            if (cb.IsChecked != true)
                continue;

            string itemPath = cb.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(itemPath))
                continue;

            if (UploadPackCheckBox.IsChecked == true &&
                !IsLinkUploadEligible(itemPath))
            {
                continue;
            }

            selectedItems.Add(itemPath);
        }

        bool createImportLink =
            UploadPackCheckBox.IsChecked == true;

        bool exported;
        try
        {
            exported = await ExportSelectedItems(
                currentProfile,
                selectedItems,
                createImportLink);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is System.Security.SecurityException ||
            ex is ArgumentException ||
            ex is NotSupportedException ||
            ex is InvalidOperationException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            Console.WriteLine($"JXCpack export failed: {ex}");
            ShowToast($"Export failed: {ex.Message}");
            return;
        }

        if (!exported)
            return;

        if (createImportLink)
        {
            try
            {
                WebsiteAccount? account = websiteAccount;
                if (account is null ||
                    string.IsNullOrWhiteSpace(account.Email))
                {
                    ShowToast(
                        LocalizationManager.Instance.Translate(
                            "Sign in to your JaxClient account before uploading a pack."));
                    return;
                }
                if (string.IsNullOrWhiteSpace(account.SessionToken))
                {
                    ShowToast(
                        LocalizationManager.Instance.Translate(
                            "Your account session is missing. Sign in again before uploading a pack."));
                    return;
                }

                Console.WriteLine("[JXCpack Upload] Starting upload.");
                Console.WriteLine(
                    $"[JXCpack Upload] Pack: {lastExportPath}");

                var packInfo = new FileInfo(lastExportPath);
                if (!packInfo.Exists)
                {
                    throw new FileNotFoundException(
                        "The exported JXCpack file does not exist.",
                        lastExportPath);
                }

                if (packInfo.Length > MaxJxcPackUploadBytes)
                {
                    ShowToast(
                        LocalizationManager.Instance.Translate(
                            "Pack exceeds upload limit of 249 MB."));
                    return;
                }

                Console.WriteLine(
                    $"[JXCpack Upload] Pack size: {packInfo.Length} bytes.");

                WebsitePackInventory inventory =
                    await GetWebsitePacksAsync(account);
                int availableCredits =
                    await RefreshWebsiteCreditsAsync(account.Email);
                PackUploadChoice? choice =
                    ShowPackUploadActionDialog(
                        inventory.Packs,
                        inventory.FreeSlotAvailable,
                        availableCredits,
                        Path.GetFileName(lastExportPath),
                        packInfo.Length);
                if (choice is null)
                    return;

                bool purchasingPaidSlot =
                    choice.Mode == "new" &&
                    !inventory.FreeSlotAvailable;
                if (purchasingPaidSlot)
                {
                    availableCredits =
                        await RefreshWebsiteCreditsAsync(account.Email);
                    if (availableCredits < 3)
                    {
                        ShowToast(
                            LocalizationManager.Instance.Translate(
                                "Not enough credits for a new pack slot (3 credits required)."));
                        return;
                    }
                }

                string uploadRequestId = Guid.NewGuid().ToString("N");
                System.Net.HttpStatusCode uploadStatus;
                string? uploadReasonPhrase;
                string json;
                while (true)
                {
                    try
                    {
                        (uploadStatus, uploadReasonPhrase, json) =
                            await SendJxcPackUploadAttemptAsync(
                                account.SessionToken,
                                lastExportPath,
                                choice,
                                uploadRequestId);
                    }
                    catch (Exception ex) when (
                        ex is HttpRequestException ||
                        ex is TaskCanceledException)
                    {
                        Console.WriteLine(
                            $"[JXCpack Upload] Request {uploadRequestId} " +
                            $"failed before a response: {ex}");
                        if (!ShowPackUploadRetryDialog(ex.Message))
                            return;

                        continue;
                    }

                    bool retryableStatus =
                        uploadStatus ==
                            System.Net.HttpStatusCode.RequestTimeout ||
                        (int)uploadStatus == 429 ||
                        (int)uploadStatus >= 500;
                    if (retryableStatus)
                    {
                        Console.WriteLine(
                            $"[JXCpack Upload] Server returned retryable " +
                            $"HTTP {(int)uploadStatus} {uploadReasonPhrase}.");
                        if (!ShowPackUploadRetryDialog(
                                uploadReasonPhrase ?? json))
                            return;

                        continue;
                    }

                    break;
                }

                Console.WriteLine(
                    $"[JXCpack Upload] HTTP {(int)uploadStatus} " +
                    $"{uploadStatus}; response length: {json.Length} characters.");
                Console.WriteLine(
                    $"[JXCpack Upload] Response body: {json}");

                if (uploadStatus ==
                    System.Net.HttpStatusCode.RequestEntityTooLarge)
                {
                    ShowToast(
                        LocalizationManager.Instance.Translate(
                            "Pack exceeds upload limit of 249 MB."));
                    return;
                }

                using JsonDocument doc =
                    JsonDocument.Parse(json);

                bool uploadSucceeded =
                    (int)uploadStatus >= 200 &&
                    (int)uploadStatus <= 299 &&
                    doc.RootElement.TryGetProperty(
                        "success",
                        out JsonElement successElement) &&
                    successElement.ValueKind == JsonValueKind.True;
                if (!uploadSucceeded)
                {
                    string? errorCode =
                        doc.RootElement.TryGetProperty(
                            "code",
                            out JsonElement codeElement)
                        ? codeElement.GetString()
                        : null;
                    if (errorCode == "INSUFFICIENT_CREDITS")
                    {
                        await TryRefreshWebsiteCreditsAsync(account.Email);
                        ShowToast(
                            LocalizationManager.Instance.Translate(
                                "Not enough credits for a new pack slot (3 credits required)."));
                        return;
                    }

                    Console.WriteLine(
                        $"[JXCpack Upload] Server reported failure: " +
                        (doc.RootElement.TryGetProperty(
                            "error",
                            out JsonElement errorElement)
                            ? errorElement.ToString()
                            : uploadReasonPhrase ?? "Unknown error"));

                    string serverError =
                        doc.RootElement.TryGetProperty(
                            "error",
                            out errorElement)
                            ? errorElement.GetString() ?? "Unknown error"
                            : uploadReasonPhrase ?? "Unknown error";
                    ShowToast(
                        LocalizationManager.Instance.Format(
                            "Pack upload failed: {0}",
                            serverError));
                    return;
                }

                if (!doc.RootElement.TryGetProperty(
                        "link",
                        out JsonElement linkElement) ||
                    linkElement.ValueKind != JsonValueKind.String ||
                    !Uri.TryCreate(
                        linkElement.GetString(),
                        UriKind.Absolute,
                        out Uri? linkUri) ||
                    linkUri.Scheme != Uri.UriSchemeHttps ||
                    !string.Equals(
                        linkUri.Host,
                        "jaxroxn.lan64.de",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new JsonException(
                        "Upload succeeded, but the response did not contain a valid pack link.");
                }

                if (!doc.RootElement.TryGetProperty(
                        "slotCost",
                        out JsonElement slotCostElement) ||
                    !slotCostElement.TryGetInt32(out int slotCost) ||
                    slotCost < 0 ||
                    !doc.RootElement.TryGetProperty(
                        "jpoints",
                        out JsonElement remainingCreditsElement) ||
                    !remainingCreditsElement.TryGetInt32(
                        out int remainingCredits) ||
                    remainingCredits < 0)
                {
                    throw new JsonException(
                        "Upload succeeded, but the response did not contain a valid credit balance.");
                }

                WebsiteCreditsText.Text =
                    LocalizationManager.Instance.Format(
                        "Credits: {0}",
                        remainingCredits);
                Console.WriteLine(
                    $"[JXCpack Upload] Upload succeeded; slot cost: " +
                    $"{slotCost}, remaining credits: {remainingCredits}.");
                await TryRefreshWebsitePacksAsync(account);
                await TryRefreshWebsiteCreditsAsync(account.Email);
                ShowPackUploadSuccessDialog(
                    linkUri.ToString(),
                    Path.GetFileName(lastExportPath));

                if (choice.Mode == "new")
                {
                    ShowToast(
                        slotCost == 0
                            ? LocalizationManager.Instance.Format(
                                "Free pack slot created. Credits charged: 0. Balance: {0}",
                                remainingCredits)
                            : LocalizationManager.Instance.Format(
                                "Pack slot purchased: {0} credits charged. Balance: {1}",
                                slotCost,
                                remainingCredits));
                }
            }
            catch (Exception ex) when (
                ex is HttpRequestException ||
                ex is TaskCanceledException ||
                ex is JsonException ||
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is InvalidDataException ||
                ex is ArgumentException)
            {
                Console.WriteLine(
                    $"[JXCpack Upload] Upload failed: {ex}");
                ShowToast(
                    LocalizationManager.Instance.Format(
                        "Pack upload failed: {0}",
                        ex.Message));
            }
        }
        else
        {
            ShowToast("Export done!");
        }
    }
    private bool allSelected = true;

    private void SelectAllToggle_Click(object sender, RoutedEventArgs e)
    {
        allSelected = !allSelected;

        foreach (CheckBox cb in ExportList.Children)
        {
            if (cb.IsEnabled)
                cb.IsChecked = allSelected;
        }

        SelectAllBtn.Content = allSelected ? "Deselect All" : "Select All";

        UpdateExportButtonState();
    }
    private void UpdateExportButtonState()
    {
        bool anyChecked = false;

        foreach (CheckBox cb in ExportList.Children)
        {
            if (cb.IsEnabled && cb.IsChecked == true)
            {
                anyChecked = true;
                break;
            }
        }

        ExportBtn.IsEnabled = anyChecked;
        ExportBtn.Opacity = anyChecked ? 1.0 : 0.5;
    }

    private void UploadPackCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        UpdateExportSelectionMode();
        UpdateExportButtonState();
    }

    private void UpdateExportSelectionMode()
    {
        bool createImportLink =
            UploadPackCheckBox?.IsChecked == true;

        if (createImportLink && !optionalPackSettingsUnselected)
        {
            foreach (CheckBox cb in ExportList.Children)
            {
                if (IsOptionalJxcPackProfileFile(cb.Tag?.ToString()))
                    cb.IsChecked = false;
            }

            optionalPackSettingsUnselected = true;
        }

        foreach (CheckBox cb in ExportList.Children)
        {
            string itemPath = cb.Tag?.ToString();
            bool allowed =
                !createImportLink ||
                (!string.IsNullOrWhiteSpace(itemPath) &&
                 IsLinkUploadEligible(itemPath));

            cb.IsEnabled = allowed;
            cb.Opacity = allowed ? 1.0 : 0.4;
            cb.Cursor = allowed ? Cursors.Hand : Cursors.Arrow;

            if (!allowed)
                cb.IsChecked = false;
        }
    }

    private static bool IsLinkUploadEligible(string itemPath)
    {
        if (IsOptionalJxcPackProfileFile(itemPath))
            return true;

        if (!Directory.Exists(itemPath))
            return false;

        string directoryName =
            Path.GetFileName(
                Path.TrimEndingDirectorySeparator(itemPath));

        
        return directoryName.Equals(
                   "mods",
                   StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals(
                   "resourcepacks",
                   StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals(
                   "shaderpacks",
                   StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals(
                   "config",
                   StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals(
                   "downloads",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOptionalJxcPackProfileFile(string? itemPath)
    {
        if (string.IsNullOrWhiteSpace(itemPath) ||
            !File.Exists(itemPath))
        {
            return false;
        }

        string fileName = Path.GetFileName(itemPath);
        if (!fileName.Equals(
                "options.txt",
                StringComparison.OrdinalIgnoreCase) &&
            !fileName.Equals(
                "servers.dat",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        DirectoryInfo? profileDirectory =
            Directory.GetParent(itemPath);
        return profileDirectory?.Parent?.Name.Equals(
            "Profiles",
            StringComparison.OrdinalIgnoreCase) == true;
    }

private async Task<bool> ExportSelectedItems(
    Profile profile,
    List<string> selectedItems,
    bool createImportLink)
{
    string profileName = profile.Name;
    string basePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "Profiles",
        profileName
    );

    string exportFolderDefault = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "export"
    );

    string exportPath = null;
    string temporaryExportPath = null;
    if (createImportLink)
    {
        selectedItems = selectedItems
            .Where(IsLinkUploadEligible)
            .ToList();
    }

    StartTask("Export Profile...");

    try
    {
        Directory.CreateDirectory(exportFolderDefault);

        SaveFileDialog dlg = new()
        {
            Filter = "JXC Pack (*.jxcpack)|*.jxcpack",
            FileName = profileName + ".jxcpack",
            InitialDirectory = exportFolderDefault
        };

        if (dlg.ShowDialog() != true)
            return false;

        exportPath = dlg.FileName;

        List<JxcPackContentEntry> marketplaceContent = new();
        HashSet<string> marketplaceFiles =
            new(StringComparer.OrdinalIgnoreCase);

        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        List<(string FilePath, string RelativePath)> marketplaceCandidates =
            await Task.Run(() =>
            {
                List<(string FilePath, string RelativePath)> candidates = new();

                foreach (string selectedPath in selectedItems)
                {
                    IEnumerable<string> files =
                        Directory.Exists(selectedPath)
                            ? Directory.GetFiles(
                                selectedPath,
                                "*",
                                SearchOption.AllDirectories)
                            : File.Exists(selectedPath)
                                ? new[] { selectedPath }
                                : Array.Empty<string>();

                    foreach (string file in files)
                    {
                        string relativePath =
                            Path.GetRelativePath(basePath, file)
                                .Replace('\\', '/');

                        if (!IsJxcPackNrcFile(relativePath) &&
                            IsJxcPackMarketplaceCandidate(
                                relativePath,
                                file))
                        {
                            candidates.Add((file, relativePath));
                        }
                    }
                }

                return candidates;
            });

        foreach ((string file, string relativePath) in marketplaceCandidates)
        {
            JxcPackContentEntry entry =
                await FindJxcPackMarketplaceContentAsync(
                    client,
                    file,
                    relativePath,
                    profile);

            if (entry == null)
                continue;

            marketplaceContent.Add(entry);
            marketplaceFiles.Add(relativePath);
        }

        temporaryExportPath =
            exportPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        await Task.Run(() =>
        {
            using (var archive = System.IO.Compression.ZipFile.Open(
                       temporaryExportPath,
                       System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (string path in selectedItems)
                {
                    if (Directory.Exists(path))
                    {
                        foreach (string file in Directory.GetFiles(
                                     path,
                                     "*",
                                     SearchOption.AllDirectories))
                        {
                            string relativePath =
                                Path.GetRelativePath(basePath, file)
                                    .Replace('\\', '/');

                            if (IsJxcPackNrcFile(relativePath) ||
                                marketplaceFiles.Contains(relativePath) ||
                                IsJxcPackGeneratedMetadata(relativePath))
                                continue;

                            archive.CreateEntryFromFile(file, relativePath);
                        }

                        foreach (string directory in Directory.GetDirectories(
                                     path,
                                     "*",
                                     SearchOption.AllDirectories))
                        {
                            string relativePath =
                                Path.GetRelativePath(basePath, directory)
                                    .Replace('\\', '/') + "/";
                            archive.CreateEntry(relativePath);
                        }
                    }
                    else if (File.Exists(path))
                    {
                        string relativePath =
                            Path.GetRelativePath(basePath, path)
                                .Replace('\\', '/');

                        if (!IsJxcPackNrcFile(relativePath) &&
                            !marketplaceFiles.Contains(relativePath) &&
                            !IsJxcPackGeneratedMetadata(relativePath))
                            archive.CreateEntryFromFile(path, relativePath);
                    }
                }

                var meta = new
                {
                    Name = profileName,
                    Loader = profile.Loader,
                    MinecraftVersion = profile.Version,
                    Version = profile.Version,
                    LoaderVersion = profile.LoaderVersion,
                    NrcEnabled = false
                };

                string metaJson = JsonSerializer.Serialize(
                    meta,
                    new JsonSerializerOptions { WriteIndented = true });

                using (StreamWriter writer =
                       new(archive.CreateEntry("profilemeta.json").Open()))
                {
                    writer.Write(metaJson);
                }

                if (marketplaceContent.Count > 0)
                {
                    string manifestJson =
                        JsonSerializer.Serialize(
                            new JxcPackContentManifest
                            {
                                Items = marketplaceContent
                            },
                            new JsonSerializerOptions
                            {
                                WriteIndented = true
                            });

                    using StreamWriter writer =
                        new(archive.CreateEntry("content-manifest.json").Open());
                    writer.Write(manifestJson);
                }
            }

            File.Move(temporaryExportPath, exportPath, true);
        });

        lastExportPath = exportPath;

        ShowToast(
            $"Exported pack with {marketplaceFiles.Count} marketplace item reference(s).");
        return true;
    }
    finally
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(temporaryExportPath) &&
                File.Exists(temporaryExportPath))
            {
                File.Delete(temporaryExportPath);
            }
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is System.Security.SecurityException)
        {
            Console.WriteLine(
                $"Could not remove temporary JXCpack file: {ex.Message}");
        }
        finally
        {
            StopTask("Export Profile...");
        }
    }
}

private static bool IsJxcPackContentPath(string relativePath)
{
    string firstDirectory =
        relativePath.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

    return firstDirectory is "mods" or "resourcepacks" or "shaderpacks";
}

private static bool IsJxcPackMarketplaceCandidate(
    string relativePath,
    string filePath)
{
    string contentType =
        relativePath.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    string fileName = Path.GetFileName(filePath);

    return contentType switch
    {
        "mods" =>
            fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase),
        "resourcepacks" or "shaderpacks" =>
            fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}

private static bool IsJxcPackNrcFile(string relativePath)
{
    string normalizedPath = relativePath.Replace('\\', '/');
    string[] pathSegments =
        normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

    return pathSegments.Length >= 2 &&
           pathSegments[0].Equals(
               "mods",
               StringComparison.OrdinalIgnoreCase) &&
           Path.GetFileName(pathSegments[^1]).Contains(
               "nrc",
               StringComparison.OrdinalIgnoreCase);
}

private static bool IsJxcPackGeneratedMetadata(string relativePath)
{
    return string.Equals(
               relativePath,
               "profilemeta.json",
               StringComparison.OrdinalIgnoreCase) ||
           string.Equals(
               relativePath,
               "content-manifest.json",
               StringComparison.OrdinalIgnoreCase);
}

private async Task<JxcPackContentEntry>
    FindJxcPackMarketplaceContentAsync(
        HttpClient client,
        string filePath,
        string relativePath,
        Profile profile)
{
    string contentType =
        relativePath.Split('/')[0];

    string searchFileName =
        Path.GetFileName(filePath);

    if (searchFileName.EndsWith(
            ".disabled",
            StringComparison.OrdinalIgnoreCase))
    {
        searchFileName =
            searchFileName[
                ..(searchFileName.Length - ".disabled".Length)];
    }

    searchFileName =
        Path.GetFileNameWithoutExtension(searchFileName);

    List<string> searchNames =
        GetImportedModSearchNames(searchFileName);

    if (searchNames.Count == 0)
        return null;

    int classId = contentType switch
    {
        "mods" => 6,
        "resourcepacks" => 12,
        "shaderpacks" => 6552,
        _ => 0
    };

    int modLoaderType = profile.Loader?.ToLowerInvariant() switch
    {
        "forge" => 1,
        "fabric" => 4,
        "neoforge" => 6,
        _ => 0
    };

    foreach (string searchName in searchNames)
    {
        try
        {
            client.DefaultRequestHeaders.Remove("x-api-key");
            client.DefaultRequestHeaders.Add(
                "x-api-key",
                CurseForgeApiKey);

            string curseForgeUrl =
                "https://api.curseforge.com/v1/mods/search" +
                "?gameId=432" +
                $"&classId={classId}" +
                $"&searchFilter={Uri.EscapeDataString(searchName)}" +
                $"&gameVersion={Uri.EscapeDataString(profile.Version ?? string.Empty)}" +
                $"&modLoaderType={modLoaderType}" +
                "&pageSize=20";

            string json =
                await client.GetStringAsync(curseForgeUrl);

            CurseForgeResponse response =
                JsonSerializer.Deserialize<CurseForgeResponse>(json);

            CurseForgeMod match =
                response?.data?.FirstOrDefault(mod =>
                    mod != null &&
                    !string.IsNullOrWhiteSpace(mod.name) &&
                    searchNames.Any(name =>
                        NormalizeName(mod.name) == name));

            if (match != null)
            {
                return new JxcPackContentEntry
                {
                    Type = contentType,
                    Platform = "CurseForge",
                    ProjectId = match.id.ToString(),
                    FileName = Path.GetFileName(filePath),
                    RelativePath = relativePath
                };
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(
                $"CurseForge lookup failed for {searchName}: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"CurseForge response could not be parsed: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine(
                $"CurseForge lookup timed out for {searchName}: {ex.Message}");
        }

        client.DefaultRequestHeaders.Remove("x-api-key");

        try
        {
            string projectType = contentType switch
            {
                "mods" => "mod",
                "resourcepacks" => "resourcepack",
                "shaderpacks" => "shader",
                _ => string.Empty
            };

            string facets =
                Uri.EscapeDataString(
                    $"[[\"project_type:{projectType}\"]]");
            string modrinthUrl =
                "https://api.modrinth.com/v2/search" +
                $"?query={Uri.EscapeDataString(searchName)}" +
                $"&facets={facets}" +
                "&limit=20";

            string json =
                await client.GetStringAsync(modrinthUrl);

            ModrinthResponse response =
                JsonSerializer.Deserialize<ModrinthResponse>(json);

            ModrinthProject match =
                response?.hits?.FirstOrDefault(project =>
                    project != null &&
                    searchNames.Any(name =>
                        (!string.IsNullOrWhiteSpace(project.title) &&
                         NormalizeName(project.title) == name) ||
                        (!string.IsNullOrWhiteSpace(project.slug) &&
                         NormalizeName(project.slug) == name)));

            if (match != null)
            {
                return new JxcPackContentEntry
                {
                    Type = contentType,
                    Platform = "Modrinth",
                    ProjectId = match.project_id ?? match.id,
                    FileName = Path.GetFileName(filePath),
                    RelativePath = relativePath
                };
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(
                $"Modrinth lookup failed for {searchName}: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"Modrinth response could not be parsed: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine(
                $"Modrinth lookup timed out for {searchName}: {ex.Message}");
        }
    }

    return null;
}

private async Task<int> RestoreJxcPackMarketplaceContentAsync(
    string profilePath,
    string manifestPath,
    string loader,
    string version)
{
    JxcPackContentManifest manifest;

    try
    {
        string json = await File.ReadAllTextAsync(manifestPath);
        manifest =
            JsonSerializer.Deserialize<JxcPackContentManifest>(json);
    }
    catch (JsonException ex)
    {
        throw new InvalidDataException(
            "The JXCpack content manifest is invalid.",
            ex);
    }

    if (manifest?.Items == null)
        throw new InvalidDataException(
            "The JXCpack content manifest has no item list.");

    int failures = 0;
    using HttpClient client = new();

    foreach (JxcPackContentEntry item in manifest.Items)
    {
        if (!TryGetJxcPackContentTarget(
                profilePath,
                item,
                out string targetPath))
        {
            Console.WriteLine(
                $"Skipping invalid JXCpack content path: {item.RelativePath}");
            failures++;
            continue;
        }

        try
        {
            byte[] fileBytes;
            if (item.Platform.Equals(
                    "CurseForge",
                    StringComparison.OrdinalIgnoreCase))
            {
                fileBytes =
                    await DownloadCurseForgeJxcPackItemAsync(
                        client,
                        item,
                        loader,
                        version);
            }
            else if (item.Platform.Equals(
                         "Modrinth",
                         StringComparison.OrdinalIgnoreCase))
            {
                fileBytes =
                    await DownloadModrinthJxcPackItemAsync(
                        client,
                        item,
                        loader,
                        version);
            }
            else
            {
                throw new InvalidDataException(
                    $"Unknown JXCpack content provider: {item.Platform}");
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(targetPath));
            await File.WriteAllBytesAsync(targetPath, fileBytes);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is JsonException ||
            ex is InvalidDataException ||
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is TaskCanceledException)
        {
            Console.WriteLine(
                $"Could not restore {item.RelativePath}: {ex.Message}");
            failures++;
        }
    }

    return failures;
}

private static bool TryGetJxcPackContentTarget(
    string profilePath,
    JxcPackContentEntry item,
    out string targetPath)
{
    targetPath = null;

    if (item == null ||
        string.IsNullOrWhiteSpace(item.Type) ||
        string.IsNullOrWhiteSpace(item.RelativePath) ||
        string.IsNullOrWhiteSpace(item.Platform) ||
        string.IsNullOrWhiteSpace(item.ProjectId) ||
        !IsJxcPackContentPath(item.RelativePath) ||
        item.Type is not ("mods" or "resourcepacks" or "shaderpacks") ||
        !item.RelativePath.StartsWith(
            item.Type + "/",
            StringComparison.OrdinalIgnoreCase) ||
        Path.IsPathRooted(item.RelativePath))
    {
        return false;
    }

    try
    {
        string profileRoot = Path.GetFullPath(profilePath);
        string contentRoot = Path.GetFullPath(
            Path.Combine(profileRoot, item.Type));
        string candidate = Path.GetFullPath(
            Path.Combine(
                profileRoot,
                item.RelativePath.Replace('/', Path.DirectorySeparatorChar)));

        string contentRootPrefix =
            contentRoot.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(
                contentRootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        targetPath = candidate;
        return true;
    }
    catch (ArgumentException)
    {
        return false;
    }
    catch (NotSupportedException)
    {
        return false;
    }
    catch (PathTooLongException)
    {
        return false;
    }
}

private static async Task<byte[]> DownloadCurseForgeJxcPackItemAsync(
    HttpClient client,
    JxcPackContentEntry item,
    string loader,
    string version)
{
    if (!int.TryParse(item.ProjectId, out int projectId))
        throw new InvalidDataException("Invalid CurseForge project ID.");

    client.DefaultRequestHeaders.Remove("x-api-key");
    client.DefaultRequestHeaders.Add("x-api-key", CurseForgeApiKey);

    int modLoaderType = loader?.ToLowerInvariant() switch
    {
        "forge" => 1,
        "fabric" => 4,
        "neoforge" => 6,
        _ => 0
    };

    string url =
        $"https://api.curseforge.com/v1/mods/{projectId}/files" +
        "?pageSize=50" +
        $"&gameVersion={Uri.EscapeDataString(version ?? string.Empty)}" +
        $"&modLoaderType={modLoaderType}";
    string json = await client.GetStringAsync(url);
    CurseForgeFilesResponse response =
        JsonSerializer.Deserialize<CurseForgeFilesResponse>(json);

    CurseForgeFile[] files = response?.data?.ToArray()
        ?? Array.Empty<CurseForgeFile>();
    CurseForgeFile selected =
        files.FirstOrDefault(file =>
            string.Equals(
                file.fileName,
                item.FileName,
                StringComparison.OrdinalIgnoreCase))
        ?? files.FirstOrDefault(file =>
            file.gameVersions?.Any(gameVersion =>
                string.Equals(
                    gameVersion,
                    version,
                    StringComparison.OrdinalIgnoreCase)) == true)
        ?? files.FirstOrDefault();

    if (selected == null || string.IsNullOrWhiteSpace(selected.downloadUrl))
        throw new InvalidDataException(
            $"No downloadable CurseForge file found for {item.FileName}.");

    client.DefaultRequestHeaders.Remove("x-api-key");
    return await client.GetByteArrayAsync(selected.downloadUrl);
}

private static async Task<byte[]> DownloadModrinthJxcPackItemAsync(
    HttpClient client,
    JxcPackContentEntry item,
    string loader,
    string version)
{
    client.DefaultRequestHeaders.Remove("x-api-key");
    string projectId = Uri.EscapeDataString(item.ProjectId);
    string json = await client.GetStringAsync(
        $"https://api.modrinth.com/v2/project/{projectId}/version");
    List<ModrinthVersion> versions =
        JsonSerializer.Deserialize<List<ModrinthVersion>>(json);

    if (versions == null || versions.Count == 0)
        throw new InvalidDataException(
            $"No Modrinth versions found for {item.ProjectId}.");

    IEnumerable<ModrinthVersion> compatible = versions;
    if (item.Type == "mods")
    {
        compatible = compatible.Where(candidate =>
            candidate.game_versions?.Contains(
                version,
                StringComparer.OrdinalIgnoreCase) == true &&
            candidate.loaders?.Contains(
                loader,
                StringComparer.OrdinalIgnoreCase) == true);
    }
    else if (item.Type == "resourcepacks")
    {
        List<ModrinthVersion> versionMatches =
            compatible.Where(candidate =>
                    candidate.game_versions?.Contains(
                        version,
                        StringComparer.OrdinalIgnoreCase) == true)
                .ToList();

        if (versionMatches.Count > 0)
            compatible = versionMatches;
    }

    List<ModrinthVersion> compatibleVersions =
        compatible.ToList();
    ModrinthFile selectedFile =
        compatibleVersions
            .SelectMany(candidate =>
                candidate.files ?? new List<ModrinthFile>())
            .FirstOrDefault(file =>
                string.Equals(
                    file.filename,
                    item.FileName,
                    StringComparison.OrdinalIgnoreCase))
        ?? compatibleVersions
            .SelectMany(candidate =>
                candidate.files ?? new List<ModrinthFile>())
            .FirstOrDefault();

    if (selectedFile == null || string.IsNullOrWhiteSpace(selectedFile.url))
        throw new InvalidDataException(
            $"No compatible Modrinth file found for {item.FileName}.");

    return await client.GetByteArrayAsync(selectedFile.url);
}

private async Task ImportNoriskProfile(string filePath)
{
    try
    {
        await Task.Run(() =>
        {
            Console.WriteLine("=== IMPORT START ===");
            Console.WriteLine("File: " + filePath);


            installedMods = new List<ContentItem>();


            List<ContentItem> shaders = new List<ContentItem>();


            List<ContentItem> resourcepacks = new List<ContentItem>();

            StartTask("Extracting profile (1/6)...");

            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE", "JaxClient"
            );

            string resourcePath = Path.Combine(basePath, "resource");
            Directory.CreateDirectory(resourcePath);

            string extractPath = Path.Combine(resourcePath, "_import");

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            Directory.CreateDirectory(extractPath);

            System.IO.Compression.ZipFile.ExtractToDirectory(filePath, extractPath, true);

            StopTask("Extracting profile (1/6)...");
            StartTask("Reading profile (2/6)...");

            string profileJsonPath = Directory
                .EnumerateFiles(extractPath, "profile.json", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (profileJsonPath == null)
                throw new Exception("❌ profile.json not found");

            string json = File.ReadAllText(profileJsonPath);
            using JsonDocument doc = JsonDocument.Parse(json);

            string loader = doc.RootElement.TryGetProperty("loader", out var lProp)
                ? lProp.GetString()
                : null;

            bool isFabric =
                string.Equals(loader, "fabric",
                    StringComparison.OrdinalIgnoreCase);
            bool isForge =
                string.Equals(loader, "forge",
                    StringComparison.OrdinalIgnoreCase);
            bool isNeoForge =
                string.Equals(loader, "neoforge",
                    StringComparison.OrdinalIgnoreCase);
            bool isVanilla =
                string.IsNullOrWhiteSpace(loader) ||
                string.Equals(loader, "vanilla",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(loader, "minecraft",
                    StringComparison.OrdinalIgnoreCase);

            if (!isFabric && !isForge && !isNeoForge && !isVanilla)
            {
                throw new Exception(
                    $"❌ Unsupported loader: {loader}");
            }

            string name = doc.RootElement.TryGetProperty("name", out var nProp)
                ? nProp.GetString()
                : "ImportedProfile";

            string version = doc.RootElement.TryGetProperty("game_version", out var vProp)
                ? vProp.GetString()
                : "unknown";

            string loaderVersion = doc.RootElement.TryGetProperty("loader_version", out var lvProp)
                ? lvProp.GetString()
                : "unknown";
            if (isVanilla)
                loaderVersion = "";

            StopTask("Reading profile (2/6)...");
            StartTask("Copying files (3/6)...");


            string overrides = Directory
                .EnumerateDirectories(extractPath, "overrides", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (overrides != null)
            {
                string target = Path.Combine(basePath, "Profiles", name);

                foreach (var file in Directory.EnumerateFiles(overrides, "*.*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(overrides, file);

                    rel = rel.Replace(".disabled", "");

                    string dest = Path.Combine(target, rel);

                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(file, dest, true);
                }
            }

            string copiedModsFolder =
                Path.Combine(basePath, "Profiles", name, "mods");

            if (Directory.Exists(copiedModsFolder))
            {
                foreach (string file in Directory.GetFiles(copiedModsFolder))
                {
                    string extension = Path.GetExtension(file);
                    string fileName = Path.GetFileName(file);

                    if (extension.Equals(
                            ".jar",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = Path.GetFileNameWithoutExtension(file);
                    }
                    else if (fileName.EndsWith(
                                 ".jar.disabled",
                                 StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = fileName[..^".jar.disabled".Length];
                    }
                    else
                    {
                        continue;
                    }

                    installedMods.Add(new ContentItem
                    {
                        Name = fileName,
                        Description = "Imported"
                    });
                }
            }


            string shaderFolder = Path.Combine(basePath, "Profiles", name, "shaderpacks");

            if (Directory.Exists(shaderFolder))
            {
                foreach (var file in Directory.GetFiles(shaderFolder))
                {
                    string fileName = Path.GetFileName(file);

                    string cleanName = fileName
                        .Replace(".zip.disabled", "")
                        .Replace(".zip", "")
                        .Replace(".disabled", "")
                        .Replace("_", " ")
                        .Trim();
                    string newFileName = cleanName.EndsWith(".zip") ? cleanName : cleanName + ".zip";
                    string newPath = Path.Combine(shaderFolder, newFileName);

                    if (file != newPath)
                    {
                        try
                        {
                            if (File.Exists(newPath))
                                File.Delete(newPath);

                            File.Move(file, newPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Rename failed: " + ex.Message);
                        }
                    }

                    shaders.Add(new ContentItem
                    {
                        Name = cleanName,
                        Description = "Shaderpack"
                    });
                }
            }

            Console.WriteLine("SHADERS FOUND: " + shaders.Count);


            string rpFolder = Path.Combine(basePath, "Profiles", name, "resourcepacks");

            if (Directory.Exists(rpFolder))
            {
                foreach (var file in Directory.GetFiles(rpFolder))
                {
                    string fileName = Path.GetFileName(file);

                    string cleanName = fileName
                        .Replace(".zip.disabled", "")
                        .Replace(".zip", "")
                        .Replace(".disabled", "")
                        .Replace("_", " ")
                        .Trim();

                    string newFileName = cleanName.EndsWith(".zip") ? cleanName : cleanName + ".zip";
                    string newPath = Path.Combine(rpFolder, newFileName);

                    if (file != newPath)
                    {
                        try
                        {
                            if (File.Exists(newPath))
                                File.Delete(newPath);

                            File.Move(file, newPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("RP Rename failed: " + ex.Message);
                        }
                    }

                    resourcepacks.Add(new ContentItem
                    {
                        Name = cleanName,
                        Description = "Resourcepack"
                    });
                }
            }

            Console.WriteLine("RESOURCEPACKS FOUND: " + resourcepacks.Count);

            StopTask("Copying files (3/6)...");
            StartTask("Downloading mods (4/6)...");

            if (doc.RootElement.TryGetProperty("mods", out var modsArray))
            {
                string modsFolder = Path.Combine(basePath, "Profiles", name, "mods");
                Directory.CreateDirectory(modsFolder);

                using HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                foreach (var mod in modsArray.EnumerateArray())
                {
                    try
                    {
                        if (!mod.TryGetProperty("source", out var source))
                            continue;

                        string url = source.TryGetProperty("download_url", out var urlProp)
                            ? urlProp.GetString()
                            : null;

                        string fileName = source.TryGetProperty("file_name", out var fnProp)
                            ? fnProp.GetString()
                            : null;


                        if (string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                if (mod.TryGetProperty("project_id", out var projectProp))
                                {
                                    int projectId = projectProp.GetInt32();

                                    using HttpClient cf = new HttpClient();

                                    cf.DefaultRequestHeaders.Add(
                                        "x-api-key",
                                        CurseForgeApiKey);

                                    string cfJson =
                                        cf.GetStringAsync(
                                                $"https://api.curseforge.com/v1/mods/{projectId}/files")
                                            .GetAwaiter()
                                            .GetResult();

                                    var cfFiles =
                                        JsonSerializer.Deserialize<CurseForgeFilesResponse>(
                                            cfJson);

                                    if (cfFiles?.data != null)
                                    {
                                        var match =
                                            cfFiles.data.FirstOrDefault(f =>
                                                f.gameVersions != null &&
                                                f.gameVersions.Any(v =>
                                                    v.Equals(
                                                        version,
                                                        StringComparison.OrdinalIgnoreCase)));

                                        if (match != null)
                                        {
                                            url = match.downloadUrl;
                                            fileName = match.fileName;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(
                                    "CurseForge fallback failed: " +
                                    ex.Message);
                            }
                        }

                        if (string.IsNullOrEmpty(url))
                            continue;

                        if (string.IsNullOrEmpty(fileName))
                            fileName = "unknown.jar";

                        fileName = fileName.Replace(".disabled", "");

                        string displayName =
                            mod.TryGetProperty("display_name", out var dnProp)
                                ? dnProp.GetString()
                                : fileName;

                        foreach (char c in Path.GetInvalidFileNameChars())
                        {
                            displayName =
                                displayName.Replace(
                                    c.ToString(),
                                    "");
                        }


                        displayName = displayName.Replace("_", " ");
                        displayName = displayName.Replace("-", " ");

                        while (displayName.Contains("  "))
                        {
                            displayName = displayName.Replace("  ", " ");
                        }

                        string cleanName = displayName
                            .Trim()
                            .Replace("_", " ");

                        string destFile =
                            Path.Combine(
                                modsFolder,
                                cleanName + ".jar");

                        if (!File.Exists(destFile))
                        {
                            try
                            {
                                byte[] data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();

                                if (data != null && data.Length > 1000)
                                    File.WriteAllBytes(destFile, data);
                            }
                            catch
                            {
                                Console.WriteLine("Download failed: " + displayName);
                            }
                        }

                        ContentItem existingMod =
                            installedMods.FirstOrDefault(item =>
                                NormalizeCompareName(item.Name) ==
                                NormalizeCompareName(cleanName));

                        if (existingMod == null)
                        {
                            installedMods.Add(new ContentItem
                            {
                                Name = cleanName,
                                Description = mod.TryGetProperty("version", out var verProp)
                                    ? verProp.GetString()
                                    : "Imported"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Mod error: " + ex.Message);
                    }
                }
            }

            StopTask("Downloading mods (4/6)...");
            StartTask("Saving profile (5/6)...");

            string profilesFile = Path.Combine(basePath, "profiles.json");

            List<Profile> profiles;

            if (File.Exists(profilesFile))
                profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(profilesFile)) ?? new();
            else
                profiles = new List<Profile>();

            if (profiles.Any(p => p.Name == name))
                throw new Exception("❌ Profile already exists");


            profiles.Add(new Profile
            {
                Name = name,
                Loader = isVanilla
                    ? "Vanilla"
                    : isNeoForge
                            ? "NeoForge"
                            : isForge
                                ? "Forge"
                                : "Fabric",
                Version = version,
                LoaderVersion = loaderVersion,
                NrcEnabled = true,
                Mods = installedMods,
                ProfilePicture = GetRandomProfilePicture(),
                Shaders = shaders,
                RP = resourcepacks
            });

            File.WriteAllText(profilesFile,
                JsonSerializer.Serialize(profiles, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            );

            StopTask("Saving profile (5/6)...");
            StartTask("Cleaning up (6/6)...");

            Directory.Delete(extractPath, true);

            StopTask("Cleaning up (6/6)...");

            Console.WriteLine("=== IMPORT DONE ===");
        });

        ShowToast("Import erfolgreich!");
        LoadProfiles();
    }
    catch (Exception ex)
    {
        Console.WriteLine("=== IMPORT ERROR ===");
        Console.WriteLine(ex.ToString());
        ShowToast("Import Error!");
    }
    finally
    {
        StopTask("Cleaning up (6/6)...");
    }
}
private void SaveLastAccount(string username)
{
    string path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "data.json"
    );

    Dictionary<string, string> data;

    if (File.Exists(path))
    {
        data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
               ?? new Dictionary<string, string>();
    }
    else
    {
        data = new Dictionary<string, string>();
    }

    data["lastAccount"] = username;

    File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions
    {
        WriteIndented = true
    }));
}


void SetTaskText(string text)
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        DownloadText.Text = text;
    });
}
    
    private void ConfirmCancel_Click(object sender, RoutedEventArgs e)
    {
        ConfirmOverlay.Visibility = Visibility.Collapsed;
        profileToDelete = null;
    }
    private List<string> currentTasks = new List<string>();
    
    
    
    private async Task HandleSpecialVersion(string version)
{
    if (cancelLaunch)
    {
        ResetLaunchUI();
        return;
    }

    try
    {

        if (currentProfile?.IsExternalProfile == true ||
            !version.StartsWith("1.21.11"))
            return;
        
        if (currentProfile == null)
            return;

        StartTask("Installing required mods...");
        

        string modsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            currentProfile.Name,
            "mods"
        );

        foreach (string file in Directory.GetFiles(
                     modsPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);

            if (fileName.Contains("JXC", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not delete {fileName}: {ex}");
                }
            }
        }
        Directory.CreateDirectory(modsPath);


        string tempZip = System.IO.Path.Combine(modsPath, "temp_special.zip");

        using HttpClient client = new HttpClient();


        string url = "https://www.dropbox.com/scl/fi/m7b09hnrgdg1wfljsf5ba/Mods.zip?rlkey=8t6jo1ny188sne7pj7rkd1n6s&dl=1";


        ThrowIfCancelled();
        byte[] data = await client.GetByteArrayAsync(url);
        ThrowIfCancelled();
        await File.WriteAllBytesAsync(tempZip, data);

        if (cancelLaunch)
           
        {
            
            ResetLaunchUI();
            
            return;
            
        }
        StopTask("Installing required mods...");
        StartTask("Extracting mods...");


        string tempExtract = System.IO.Path.Combine(modsPath, "_temp_extract");

        if (Directory.Exists(tempExtract))
            Directory.Delete(tempExtract, true);

        Directory.CreateDirectory(tempExtract);

        // Extract the ZIP archive.
        System.IO.Compression.ZipFile.ExtractToDirectory(
            tempZip,
            tempExtract,
            true
        );


        foreach (var file in Directory.GetFiles(tempExtract, "*.*", SearchOption.AllDirectories))
        {
            string fileName = System.IO.Path.GetFileName(file);
            string target = System.IO.Path.Combine(modsPath, fileName);

            File.Copy(file, target, true); // Overwrite existing files.
        }


        Directory.Delete(tempExtract, true);
        File.Delete(tempZip);
        StopTask("Extracting mods...");
        
    }
    catch (Exception ex)
    {
        StopTask("Extracting mods...");
        
        Console.WriteLine(ex);
    }
}   

    private void TaskIdleIcon_Click(
        object sender,
        MouseButtonEventArgs e)
    {
        if (currentTasks.Count > 0)
        {
            TaskStatusWidget.Visibility =
                Visibility.Visible;

            TaskIdleIcon.Visibility =
                Visibility.Collapsed;
        }
        else
        {
            NoTasksPopup.IsOpen =
                !NoTasksPopup.IsOpen;
        }
    }
    private void StartTask(
        string name)
    {
        Dispatcher.Invoke(() =>
        {
            if (!currentTasks.Contains(name))
            {
                currentTasks.Add(name);
            }

            TaskIdleIcon.Visibility =
                Visibility.Collapsed;

            TaskStatusWidget.Visibility =
                Visibility.Visible;
            NoTasksPopup.IsOpen = false;
            UpdateTaskUI();
        });
    }
    private async void StopTask(
        string name)
    {
        bool shouldShowCompleted = false;
        Dispatcher.Invoke(() =>
        {
            currentTasks.Remove(name);

            if (currentTasks.Count > 0)
            {
                UpdateTaskUI();
                return;
            }

            shouldShowCompleted = true;
            TaskStatusWidget.Visibility =
                Visibility.Visible;

            TaskStatusText.Text =
                LocalizationManager.Instance.Translate("✓ Completed");

            TaskProgressBar.IsIndeterminate =
                false;

            TaskProgressBar.Value = 100;
        });

        if (!shouldShowCompleted)
            return;

        await Task.Delay(5000);

        Dispatcher.Invoke(() =>
        {
            if (currentTasks.Count > 0)
            {
                UpdateTaskUI();
                return;
            }

            TaskStatusWidget.Visibility =
                Visibility.Collapsed;

            TaskIdleIcon.Visibility =
                Visibility.Visible;

            UpdateTaskUI();
        });
    }
    private void UpdateTaskUI()
    {
        if (TaskStatusWidget == null)
            return;

        if (currentTasks.Count == 0)
        {
            TaskIdleIcon.Visibility =
                Visibility.Visible;

            TaskStatusWidget.Visibility =
                Visibility.Collapsed;

            return;
        }

        TaskIdleIcon.Visibility =
            Visibility.Collapsed;

        TaskStatusWidget.Visibility =
            Visibility.Visible;

        TaskStatusText.Text =
            LocalizationManager.Instance.Translate(currentTasks.Last());

        TaskProgressBar.IsIndeterminate =
            true;
    }
    private void TaskIcon_Click(object sender, RoutedEventArgs e)
    {
        TaskPopup.IsOpen = !TaskPopup.IsOpen;
    }
    private async Task DownloadBodyRender(string username)
    {
        StartTask("Downloading Skin...");

        try
        {
            using HttpClient client = new HttpClient();

            string url = $"https://crafatar.com/renders/body/{username}?size=200&overlay";

            byte[] data = await client.GetByteArrayAsync(url);


            if (data == null || data.Length < 1000)
                throw new Exception("Invalid image");

            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource"
            );

            Directory.CreateDirectory(folder);

            string file = System.IO.Path.Combine(folder, $"{username.ToLower()}.png");

            await File.WriteAllBytesAsync(file, data);

            StopTask("Downloading Skin...");
        }
        catch
        {
            StopTask("Downloading Skin...");
            ShowToast("Skin download failed");
        }
    }
    

    public static class SkinRenderer
    {
        public static bool Test()
        {
            try
            {
                var settings =
                    GameWindowSettings.Default;

                var native =
                    NativeWindowSettings.Default;

                using var window =
                    new GameWindow(
                        settings,
                        native
                    );

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
    
    private async Task DownloadBodySkin(string username, string uuid)
    {
        string baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient"
        );

        string resourceFolder = Path.Combine(baseFolder, "resource");
        string logFile = Path.Combine(baseFolder, "log.txt");

        Directory.CreateDirectory(resourceFolder);

        string file = Path.Combine(resourceFolder, $"{username.ToLower()}_skin.png");

        using HttpClient client = new HttpClient();


        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "JaxClient/1.0 (https://jaxclient.local; dev@jaxclient.local)"
        );

       
        try
        {
           

            string visageUrl = $"https://visage.surgeplay.com/full/512/{uuid}.png?no=cape";

            

            byte[] data = await client.GetByteArrayAsync(visageUrl);

            if (data != null && data.Length > 1000)
            {
                await File.WriteAllBytesAsync(file, data);
               
                return;
            }

            throw new Exception("Visage invalid");
        }
        catch (Exception ex)
        {
            
        }

        
    }
private async void LoadSkinLocal()
{
    try
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource");

        Directory.CreateDirectory(folder);

        

        if (currentAccountData != null &&
            !string.IsNullOrEmpty(currentAccountData.selectedSkin))
        {
            string accountGif = Path.Combine(
                folder,
                $"{Path.GetFileNameWithoutExtension(
                    currentAccountData.selectedSkin)}.gif");

            if (!File.Exists(accountGif))
            {
                string selectedSkinPath = Path.Combine(
                    folder,
                    "mp",
                    currentAccountData.selectedSkin);

                if (File.Exists(selectedSkinPath))
                {
                    await Task.Run(async () =>
                    {
                        await SkinPreviewWindow2.ShowPreview(
                            selectedSkinPath,
                            Path.GetFileNameWithoutExtension(
                                currentAccountData.selectedSkin));
                    });

                    accountGif = Path.Combine(
                        folder,
                        $"{Path.GetFileNameWithoutExtension(
                            currentAccountData.selectedSkin)}.gif");
                }
            }

            if (File.Exists(accountGif))
            {
                var gif = new BitmapImage();

                gif.BeginInit();
                gif.CacheOption = BitmapCacheOption.OnLoad;
                gif.UriSource = new Uri(
                    accountGif,
                    UriKind.Absolute);
                gif.EndInit();

                ImageBehavior.SetAnimatedSource(
                    SkinImage,
                    gif);

                return;
            }
        }
        else
        {

            string steveGif =
                "pack://application:,,,/Assets/steve_skin.gif";

            var gif = new BitmapImage();

            gif.BeginInit();
            gif.CacheOption = BitmapCacheOption.OnLoad;
            gif.UriSource = new Uri(
                steveGif,
                UriKind.Absolute);
            gif.EndInit();

            ImageBehavior.SetAnimatedSource(
                SkinImage,
                gif);

            SkinImage.UpdateLayout();

            return;

            
        }

        string userBody = currentAccountData != null
            ? Path.Combine(
                folder,
                $"{currentAccountData.username.ToLower()}_skin.png")
            : null;

        string stevePath =
            "pack://application:,,,/Assets/steve.png";

        string pathToLoad = stevePath;

        if (userBody != null &&
            File.Exists(userBody))
        {
            pathToLoad = userBody;
        }
       

        if (string.IsNullOrEmpty(pathToLoad))
        {
            SkinImage.Source = null;
            return;
        }

        var img = new BitmapImage();

        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        img.UriSource = new Uri(
            pathToLoad,
            UriKind.Absolute);
        img.EndInit();

        img.Freeze();

        SkinImage.Source = null;
        SkinImage.Source = img;
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "Skin load error: " + ex.Message);

        try
        {
            string stevePath =
                "pack://application:,,,/Assets/steve.png";

            var img = new BitmapImage();

            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(
                stevePath,
                UriKind.Absolute);
            img.EndInit();

            img.Freeze();

            SkinImage.Source = img;
           
        }
        catch
        {
            SkinImage.Source = null;
        }
    }
}
    
    private async void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        if (profileToDelete == null || isDeletingProfile)
            return;

        Profile profile = profileToDelete;
        string profilesFilePath = filePath;
        string taskName =
            LocalizationManager.Instance.Translate("Deleting profile...");
        isDeletingProfile = true;
        StartTask(taskName);

        try
        {
            await Task.Run(() =>
            {
                if (!File.Exists(profilesFilePath))
                {
                    throw new FileNotFoundException(
                        "The profiles file could not be found.",
                        profilesFilePath);
                }

                string json = File.ReadAllText(profilesFilePath);
                List<Profile> profiles =
                    JsonSerializer.Deserialize<List<Profile>>(json) ?? new();
                profiles.RemoveAll(item =>
                    string.Equals(
                        item.Name,
                        profile.Name,
                        StringComparison.Ordinal));

                string profileFolder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".JAXSTORE",
                    "JaxClient",
                    "Profiles",
                    profile.Name);

                if (Directory.Exists(profileFolder))
                    Directory.Delete(profileFolder, recursive: true);

                string output = JsonSerializer.Serialize(
                    profiles,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profilesFilePath, output);
            });

            ConfirmOverlay.Visibility = Visibility.Collapsed;
            profileToDelete = null;
            LoadLastProfile();
            LoadProfiles();
            ShowToast(
                LocalizationManager.Instance.Translate("Profile deleted!"));
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is JsonException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            Console.WriteLine($"Could not delete profile '{profile.Name}': {ex}");
            ShowToast(
                LocalizationManager.Instance.Format(
                    "Could not delete profile: {0}",
                    ex.Message));
        }
        finally
        {
            isDeletingProfile = false;
            StopTask(taskName);
        }
    }

      
    private bool editNrcEnabled;
    private bool editProfileInitializing;
    private int editLoaderVersionLoadGeneration;
    private int createLoaderVersionLoadGeneration;

    private void EditNRC_Toggle_Click(object sender, MouseButtonEventArgs e)
    {
        editNrcEnabled = !editNrcEnabled;

        if (editNrcEnabled)
        {
            EditNRC_Toggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            EditNRC_Knob.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            EditNRC_Toggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
            EditNRC_Knob.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }

    private void VersionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoaderBox_Changed(LoaderBox, e);
    }

    private void EditVersionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoaderBox_Changed(EditLoaderBox, e);
    }
    private async void LoaderBox_Changed(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (editProfileInitializing ||
            sender is not ComboBox loaderBox ||
            loaderBox.SelectedItem is not ComboBoxItem loaderItem)
        {
            return;
        }

        ComboBox targetVersionBox;
        ComboBox gameVersionBox;
        if (loaderBox == EditLoaderBox)
        {
            targetVersionBox = EditLoaderVersionBox;
            gameVersionBox = EditVersionBox;
        }
        else if (loaderBox == LoaderBox)
        {
            targetVersionBox = LoaderVersionBox;
            gameVersionBox = VersionBox;
        }
        else
        {
            return;
        }

        string? loader = loaderItem.Content?.ToString();
        string? gameVersion =
            (gameVersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(loader) ||
            string.IsNullOrWhiteSpace(gameVersion))
            return;

        if (loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
        {
            bool editingProfile = targetVersionBox == EditLoaderVersionBox;
            if (editingProfile)
                ++editLoaderVersionLoadGeneration;
            else
                ++createLoaderVersionLoadGeneration;

            targetVersionBox.Items.Clear();
            targetVersionBox.IsEnabled = false;
            return;
        }

        await LoadLoaderVersionsAsync(
            targetVersionBox,
            loader,
            gameVersion,
            preferredVersion: null);
    }

    private async Task LoadLoaderVersionsAsync(
        ComboBox targetVersionBox,
        string loader,
        string gameVersion,
        string? preferredVersion)
    {
        bool editingProfile = targetVersionBox == EditLoaderVersionBox;
        int requestGeneration = editingProfile
            ? ++editLoaderVersionLoadGeneration
            : ++createLoaderVersionLoadGeneration;

        bool IsCurrentRequest() =>
            requestGeneration ==
            (editingProfile
                ? editLoaderVersionLoadGeneration
                : createLoaderVersionLoadGeneration);

        targetVersionBox.Items.Clear();
        targetVersionBox.Items.Add(new ComboBoxItem { Content = "Loading..." });
        targetVersionBox.IsEnabled = false;

        try
        {
            using HttpClient client = new();
            List<string> versions = new();

            if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
            {
                string json = await client.GetStringAsync(
                    "https://meta.fabricmc.net/v2/versions/loader");
                using JsonDocument doc = JsonDocument.Parse(json);
                foreach (JsonElement entry in
                         doc.RootElement.EnumerateArray().Take(20))
                {
                    if (entry.TryGetProperty("version", out JsonElement version) &&
                        version.GetString() is string loaderVersion &&
                        !string.IsNullOrWhiteSpace(loaderVersion))
                    {
                        versions.Add(loaderVersion);
                    }
                }
            }
            else if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
            {
                string xml = await client.GetStringAsync(
                    "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");
                foreach (Match match in Regex.Matches(xml, @"<version>(.*?)</version>")
                             .Cast<Match>()
                             .Reverse())
                {
                    string version = match.Groups[1].Value.Trim();
                    if (string.IsNullOrWhiteSpace(version) ||
                        (!string.IsNullOrWhiteSpace(gameVersion) &&
                         !version.StartsWith(
                             gameVersion + "-",
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    string lowerVersion = version.ToLowerInvariant();
                    if (lowerVersion.Contains("pre") ||
                        lowerVersion.Contains("rc") ||
                        lowerVersion.Contains("beta"))
                    {
                        continue;
                    }

                    if (!versions.Contains(version, StringComparer.OrdinalIgnoreCase))
                        versions.Add(version);
                }
                versions = versions
                    .OrderByDescending(version =>
                        Version.TryParse(version, out Version parsed)
                            ? parsed
                            : new Version(0, 0))
                    .ToList();
            }
            else if (loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                string versionPrefix = GetNeoForgeVersionPrefix(gameVersion);
                if (string.IsNullOrWhiteSpace(versionPrefix))
                {
                    throw new InvalidOperationException(
                        $"NeoForge does not publish versions for Minecraft {gameVersion}.");
                }

                string xml = await client.GetStringAsync(
                    "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml");
                foreach (Match match in Regex.Matches(xml, @"<version>(.*?)</version>")
                             .Cast<Match>()
                             .Reverse())
                {
                    string version = match.Groups[1].Value.Trim();
                    if (!version.StartsWith(
                            versionPrefix + ".",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string lowerVersion = version.ToLowerInvariant();
                    if (lowerVersion.Contains("alpha") ||
                        lowerVersion.Contains("beta") ||
                        lowerVersion.Contains("rc") ||
                        lowerVersion.Contains("snapshot"))
                    {
                        continue;
                    }

                    if (!versions.Contains(version, StringComparer.OrdinalIgnoreCase))
                        versions.Add(version);
                }
                versions = versions
                    .OrderByDescending(version =>
                        Version.TryParse(version, out Version parsed)
                            ? parsed
                            : new Version(0, 0))
                    .ToList();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported loader: {loader}");
            }

            if (!IsCurrentRequest())
                return;

            targetVersionBox.Items.Clear();
            foreach (string version in versions)
            {
                targetVersionBox.Items.Add(new ComboBoxItem
                {
                    Content = version
                });
            }

            if (!string.IsNullOrWhiteSpace(preferredVersion) &&
                !versions.Contains(
                    preferredVersion,
                    StringComparer.OrdinalIgnoreCase))
            {
                targetVersionBox.Items.Add(new ComboBoxItem
                {
                    Content = preferredVersion
                });
            }

            if (targetVersionBox.Items.Count == 0)
            {
                targetVersionBox.Items.Add(new ComboBoxItem
                {
                    Content = "No versions found"
                });
            }

            SetComboBoxValue(
                targetVersionBox,
                preferredVersion);
            if (targetVersionBox.SelectedIndex < 0)
                targetVersionBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            if (!IsCurrentRequest())
                return;

            Console.WriteLine(
                $"Could not load {loader} versions for Minecraft {gameVersion}: {ex}");
            targetVersionBox.Items.Clear();
            if (!string.IsNullOrWhiteSpace(preferredVersion))
            {
                targetVersionBox.Items.Add(new ComboBoxItem
                {
                    Content = preferredVersion
                });
            }

            targetVersionBox.Items.Add(new ComboBoxItem
            {
                Content = "Error loading"
            });
            if (!string.IsNullOrWhiteSpace(preferredVersion))
                targetVersionBox.SelectedIndex = 0;
        }
        finally
        {
            if (IsCurrentRequest())
                targetVersionBox.IsEnabled = true;
        }
    }

    private static string GetNeoForgeVersionPrefix(string gameVersion)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
            return string.Empty;

        string[] parts = gameVersion.Split('.');
        if (parts.Length >= 3 &&
            parts[0] == "1" &&
            int.TryParse(parts[1], out _) &&
            int.TryParse(parts[2], out _))
        {
            return $"{parts[1]}.{parts[2]}";
        }

        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int major) &&
            int.TryParse(parts[1], out int minor))
        {
            return major == 1 ? "1.0" : $"{major}.{minor}";
        }

        return string.Empty;
    }
    
    
    
    public class FabricLoader
    {
        public LoaderDetail loader { get; set; }
    }

    public class LoaderDetail
    {
        public string version { get; set; }
    }

    public class LoaderInfo
    {
        public string name { get; set; }
    }
    private void OpenProfileSelector_Click(object sender, RoutedEventArgs e)
    {
        if (isLaunching)
        {
            
            return;
        }
        ProfileSelectOverlay.Visibility = Visibility.Visible;

        LoadProfileSelector();
    }
    private void CloseProfileSelector_Click(object sender, RoutedEventArgs e)
    {
        ProfileSelectOverlay.Visibility = Visibility.Collapsed;
    }
    
    private string selectedProfileSourceFilter = "All";

    private void ProfileSourceFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filter } ||
            filter is not ("All" or "NRC" or "Modrinth" or "JaxClient"))
        {
            return;
        }

        selectedProfileSourceFilter = filter;
        LauncherSettings settings = LoadSettingsObject();
        settings.ProfileSourceFilter = filter;
        SaveSettingsObject(settings);
        UpdateProfileSourceFilterButtons();
        LoadProfiles();
        LoadProfileSelector();
    }

    private void UpdateProfileSourceFilterButtons()
    {
        foreach (Button button in new[]
                 {
                     HomeFilterAllButton,
                     HomeFilterNrcButton,
                     HomeFilterModrinthButton,
                     HomeFilterJaxClientButton,
                     ProfilesFilterAllButton,
                     ProfilesFilterNrcButton,
                     ProfilesFilterModrinthButton,
                     ProfilesFilterJaxClientButton
                 })
        {
            bool selected = string.Equals(
                button.Tag as string,
                selectedProfileSourceFilter,
                StringComparison.Ordinal);
            button.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    selected ? "#2563EB" : "#1E293B"));
            button.Foreground = Brushes.White;
            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(8, 6, 8, 6);
            button.Cursor = Cursors.Hand;
        }
    }

    private bool MatchesProfileSourceFilter(Profile profile) =>
        selectedProfileSourceFilter switch
        {
            "NRC" => profile.IsNoRiskProfile,
            "Modrinth" => profile.IsModrinthProfile,
            "JaxClient" => !profile.IsExternalProfile,
            _ => true
        };

    private void ProfileSelectorSearch_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (ProfileSelectList != null)
        {
            LoadProfileSelector();
        }
    }

    private void LoadProfileSelector()
    {
        ProfileSelectList.Children.Clear();
        UpdateProfileSourceFilterButtons();

        if (!File.Exists(filePath))
            return;

        var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));

        if (profiles == null)
            return;

        string search = HomeProfileSearchBox?.Text?.Trim() ?? string.Empty;
        foreach (var p in profiles.OrderBy(
                     profile => profile.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!MatchesProfileSourceFilter(p) ||
                (!string.IsNullOrWhiteSpace(search) &&
                 !p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Button btn = new Button
            {
                Content = $"{p.Name} ({p.Loader} {p.Version})" +
                    (p.IsModrinthProfile
                        ? " · Modrinth profile"
                        : p.IsNoRiskProfile ? " · NoRisk profile" : ""),
                Margin = new Thickness(0, 5, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 40,
                Cursor = Cursors.Hand
            };


            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, btn.Background);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(content);

            btn.Template = new ControlTemplate(typeof(Button))
            {
                VisualTree = border
            };


            btn.Click += (s, e) =>
            {
                SelectProfile(p);
            };

            ProfileSelectList.Children.Add(btn);
        }
    }
    private void SaveLastProfile(string name)
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "data.json"
        );

        Dictionary<string, string> data;

        if (File.Exists(path))
        {
            data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                   ?? new Dictionary<string, string>();
        }
        else
        {
            data = new Dictionary<string, string>();
        }

        data["lastProfile"] = name;
        if (currentProfile?.Name.Equals(
                name,
                StringComparison.OrdinalIgnoreCase) == true &&
            !string.IsNullOrWhiteSpace(currentProfile.ModrinthInstanceId))
        {
            data["lastProfileId"] = currentProfile.ModrinthInstanceId;
        }
        else
        {
            data.Remove("lastProfileId");
        }
        if (currentProfile?.Name.Equals(
                name,
                StringComparison.OrdinalIgnoreCase) == true &&
            !string.IsNullOrWhiteSpace(currentProfile.NoRiskProfileId))
        {
            data["lastNoRiskProfileId"] = currentProfile.NoRiskProfileId;
        }
        else
        {
            data.Remove("lastNoRiskProfileId");
        }

        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }    private void SelectProfile(Profile profile)
    {
        currentProfile = profile;


        var txt = (TextBlock)LaunchBtn.Template.FindName("SelectedProfileText", LaunchBtn);

        if (txt != null)
        {
            txt.Text = $"{profile.Name} ({profile.Loader} {profile.Version})" +
                (profile.IsModrinthProfile
                    ? " · Modrinth profile"
                    : profile.IsNoRiskProfile ? " · NoRisk profile" : "");
        }

        ProfileSelectOverlay.Visibility = Visibility.Collapsed;
        SaveLastProfile(profile.Name);
        
    }

    private void OpenProfile(Profile profile)
    {
        

        currentProfile = profile;
        try
        {
            ProfileBannerImage.Source =
                new BitmapImage(
                    new Uri(
                        $"pack://application:,,,/Assets/pp/{profile.ProfilePicture}",
                        UriKind.Absolute));
        }
        catch
        {
        }

// Always use Mods as the default tab.
        currentTab = "mod";


        ModsTab.Foreground = Brushes.White;
        RPacksTab.Foreground = Brushes.Gray;
        ShadersTab.Foreground = Brushes.Gray;

        ModsLine.Visibility = Visibility.Visible;
        RPacksLine.Visibility = Visibility.Collapsed;
        ShadersLine.Visibility = Visibility.Collapsed;


        

        ProfilesPage.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Visible;

        ProfileTitle.Text = profile.Name;
        ProfileSubInfo.Text = profile.IsModrinthProfile
            ? $"Modrinth profile • {profile.Loader} {profile.LoaderVersion} • {profile.Version}"
            : profile.IsNoRiskProfile
                ? $"NoRisk profile • {profile.Loader} {profile.LoaderVersion} • {profile.Version}"
                : $"{profile.Loader} {profile.LoaderVersion} • {profile.Version}";
        ReloadProfileData();
       
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        ProfileDetailPage.Visibility = Visibility.Collapsed;
        ProfilesPage.Visibility = Visibility.Visible;
    }
    private void CB_TabSwitch_Click(object sender, RoutedEventArgs e)
    {

        CB_ModsTab.Foreground = Brushes.Gray;
        CB_RPacksTab.Foreground = Brushes.Gray;
        CB_ShadersTab.Foreground = Brushes.Gray;

        CB_ModsLine.Visibility = Visibility.Collapsed;
        CB_RPacksLine.Visibility = Visibility.Collapsed;
        CB_ShadersLine.Visibility = Visibility.Collapsed;

        Button clicked = (Button)sender;

        if (clicked == CB_ModsTab)
        {
            currentTab = "mod";
            CB_ModsTab.Foreground = Brushes.White;
            CB_ModsLine.Visibility = Visibility.Visible;
        }
        else if (clicked == CB_RPacksTab)
        {
            currentTab = "resourcepack";
            CB_RPacksTab.Foreground = Brushes.White;
            CB_RPacksLine.Visibility = Visibility.Visible;
        }
        else if (clicked == CB_ShadersTab)
        {
            currentTab = "shader";
            CB_ShadersTab.Foreground = Brushes.White;
            CB_ShadersLine.Visibility = Visibility.Visible;
        }

        _ = LoadContent();
    }
    private void TabSwitch_Click(object sender, RoutedEventArgs e)
    {

        ModsTab.Foreground = Brushes.Gray;
        RPacksTab.Foreground = Brushes.Gray;
        ShadersTab.Foreground = Brushes.Gray;

        ModsLine.Visibility = Visibility.Collapsed;
        RPacksLine.Visibility = Visibility.Collapsed;
        ShadersLine.Visibility = Visibility.Collapsed;

        Button clicked = (Button)sender;

        // Switch tabs and update state.
        if (clicked == ModsTab)
        {
            currentTab = "mod";
            ModsTab.Foreground = Brushes.White;
            ModsLine.Visibility = Visibility.Visible;


            InstalledSearchBox.Text = installedModsSearch;
        }
        else if (clicked == RPacksTab)
        {
            currentTab = "resourcepack";
            RPacksTab.Foreground = Brushes.White;
            RPacksLine.Visibility = Visibility.Visible;


            InstalledSearchBox.Text = installedRPacksSearch;
        }
        else if (clicked == ShadersTab)
        {
            currentTab = "shader";
            ShadersTab.Foreground = Brushes.White;
            ShadersLine.Visibility = Visibility.Visible;


            InstalledSearchBox.Text = installedShadersSearch;
        }


        InstalledSearchBox.ToolTip = currentTab switch
        {
            "mod" => "Search Mods...",
            "resourcepack" => "Search Resourcepacks...",
            "shader" => "Search Shaderpacks...",
            _ => "Search..."
        };


        _ = LoadContent();
        ReloadProfileData();
    }
       
    
    private string currentSearch = "";
    private void InstalledSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        string text = InstalledSearchBox.Text?.Trim().ToLower() ?? "";

        if (currentTab == "mod")
            installedModsSearch = text;

        else if (currentTab == "resourcepack")
            installedRPacksSearch = text;

        else if (currentTab == "shader")
            installedShadersSearch = text;

        LoadInstalledContent();
    }
    public class CurseForgeModDetailsResponse
    {
        public CurseForgeModDetails data { get; set; }
    }

    public class CurseForgeModDetails
    {
        public int id { get; set; }

        public string name { get; set; }

        public string summary { get; set; }

        public CurseForgeLogo logo { get; set; }

        public List<CurseForgeScreenshot> screenshots { get; set; }
    }

    public class CurseForgeScreenshot
    {
        public string url { get; set; }
    }
    
    private void LoadInstalledContent()
{
    if (InstalledContentList == null)
    {
        ShowToast("InstalledContentList not found");
        return;
    }

    InstalledContentList.Items.Clear();

    if (currentProfile == null)
        return;

    var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));
    currentProfile = profiles.FirstOrDefault(
        profile => IsSameProfile(profile, currentProfile));

    if (currentProfile == null)
        return;

    // Items for each tab.
    List<ContentItem> items = currentTab switch
    {
        "mod" => currentProfile.Mods,
        "resourcepack" => currentProfile.RP,
        "shader" => currentProfile.Shaders,
        _ => null
    };

    if (items == null)
        return;


    string search = currentTab switch
    {
        "mod" => installedModsSearch,
        "resourcepack" => installedRPacksSearch,
        "shader" => installedShadersSearch,
        _ => ""
    };

    foreach (var item in items.OrderBy(
        item => item.Name,
        StringComparer.CurrentCultureIgnoreCase))
    {

        if (!string.IsNullOrEmpty(search))
        {
            if (!item.Name.ToLower().Contains(search))
                continue;
        }

        Border card = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString("#1E293B"),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(15),
            MinHeight = 70
        };


        Grid grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(94) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

        Image icon = new Image
        {
            Width = 48,
            Height = 48,
            Stretch = Stretch.UniformToFill,
            Margin = new Thickness(0,0,12,0)
        };

        ApplyRoundedImageCorners(icon);

        Border fallbackIcon = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#0F172A"))
        };

        fallbackIcon.Child = new TextBlock
        {
            Text = "?",
            Foreground = Brushes.White,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        if (!string.IsNullOrEmpty(item.IconUrl))
        {
            try
            {
                icon.Source = new BitmapImage(new Uri(item.IconUrl));

                Grid.SetColumn(icon, 0);
                Grid.SetRowSpan(icon, 2);
                grid.Children.Add(icon);
            }
            catch
            {
                Grid.SetColumn(fallbackIcon, 0);
                Grid.SetRowSpan(fallbackIcon, 2);
                grid.Children.Add(fallbackIcon);
            }
        }
        else
        {
            Grid.SetColumn(fallbackIcon, 0);
            Grid.SetRowSpan(fallbackIcon, 2);
            grid.Children.Add(fallbackIcon);

            _ = Task.Run(async () =>
            {
                string url = await GetIconForInstalledContent(item.Name);

                if (!string.IsNullOrEmpty(url))
                {
                    item.IconUrl = url;

                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            icon.Source = new BitmapImage(new Uri(url));

                            grid.Children.Remove(fallbackIcon);
                            Grid.SetColumn(icon, 0);
                            Grid.SetRowSpan(icon, 2);
                            grid.Children.Add(icon);
                        }
                        catch { }
                    });
                }
            });
        }
        Grid.SetColumn(icon, 0);
        Grid.SetRowSpan(icon, 2);

        StackPanel textStack = new StackPanel();

        bool isDisabled = currentProfile.IsNoRiskProfile
            ? !item.Enabled
            : IsFileDisabled(item.Name);

        TextBlock title = new TextBlock
        {
            Text = item.Name,
            Foreground = isDisabled ? Brushes.Gray : Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 16
        };

        TextBlock desc = new TextBlock
        {
            Text = item.Description,
            Foreground = isDisabled ? Brushes.DimGray : Brushes.Gray,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        };

        TextBlock versionInfo = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.VersionName)
                ? "Custom content • version not tracked"
                : $"{item.VersionProvider} • {item.VersionName}",
            Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#60A5FA")),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 6, 0, 0)
        };

        textStack.Children.Add(title);
        textStack.Children.Add(desc);
        textStack.Children.Add(versionInfo);

        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);


        Button toggleBtn = new Button
        {
            Content = isDisabled ? "Enable" : "Disable",
            Width = 88,
            Height = 30,
            Background = (Brush)new BrushConverter().ConvertFromString(
                isDisabled ? "#22C55E" : "#F59E0B"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };

        MakeRounded(toggleBtn);

        if (!currentProfile.IsNoRiskProfile)
        {
            toggleBtn.Click += (s, e) =>
            {
                ToggleMod(item);
            };

            toggleBtn.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(toggleBtn, 3);
            grid.Children.Add(toggleBtn);
        }

        if (!string.IsNullOrWhiteSpace(item.VersionProvider) &&
            !string.IsNullOrWhiteSpace(item.ProjectId) &&
            !string.IsNullOrWhiteSpace(item.VersionId))
        {
            Button versionBtn = new Button
            {
                Content = "Version",
                Width = 76,
                Height = 30,
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#334155")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Change installed version"
            };
            MakeRounded(versionBtn);
            versionBtn.Click += async (s, e) =>
            {
                versionBtn.IsEnabled = false;
                await OpenContentVersionPicker(
                    item.VersionProvider,
                    item.ProjectId,
                    item.Name,
                    item.Description,
                    item.IconUrl,
                    item);
                versionBtn.IsEnabled = true;
            };
            Grid.SetColumn(versionBtn, 2);
            grid.Children.Add(versionBtn);
        }


        Button deleteBtn = new Button
        {
            Content = "🗑",
            Width = 40,
            Height = 30,
            Background = (Brush)new BrushConverter().ConvertFromString("#EF4444"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 14
        };

        MakeRounded(deleteBtn);

        if (!currentProfile.IsNoRiskProfile)
        {
            deleteBtn.Click += (s, e) =>
            {
                DeleteInstalledItem(item);
            };

            Grid.SetColumn(deleteBtn, 4);
            grid.Children.Add(deleteBtn);
        }

        card.Child = grid;
        InstalledContentList.Items.Add(card);
        card.Cursor = Cursors.Hand;

        card.MouseLeftButtonUp += async (s, e) =>
        {
            // Ignore clicks on buttons.
            if (e.OriginalSource is Button)
                return;

            await OpenInstalledContentDetails(item.Name);
        };
    }


    if (InstalledContentList.Items.Count == 0)
    {
        InstalledScroll.Visibility = Visibility.Collapsed;
        NoContentCard.Visibility = Visibility.Visible;

        if (currentTab == "mod")
            NoContentText.Text = LocalizationManager.Instance.Translate(
                string.IsNullOrWhiteSpace(installedModsSearch)
                    ? "No mods installed"
                    : "No results found");

        else if (currentTab == "resourcepack")
            NoContentText.Text = LocalizationManager.Instance.Translate(
                string.IsNullOrWhiteSpace(installedRPacksSearch)
                    ? "No resourcepacks installed"
                    : "No results found");

        else if (currentTab == "shader")
            NoContentText.Text = LocalizationManager.Instance.Translate(
                string.IsNullOrWhiteSpace(installedShadersSearch)
                    ? "No shaderpacks installed"
                    : "No results found");
    }
    else
    {
        InstalledScroll.Visibility = Visibility.Visible;
        NoContentCard.Visibility = Visibility.Collapsed;
    }
}    

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        currentSearch = ContentSearchBox.Text;
        _ = LoadContent();
    } 
    
    private void AddCustomContent_Click(
        object sender,
        RoutedEventArgs e)
    {
        CustomPopup.IsOpen =
            !CustomPopup.IsOpen;
    }
    private void SelectMod_Click(object sender, RoutedEventArgs e)
    {
        OpenFileAndInstall("mod");
    }
    private void SelectRP_Click(object sender, RoutedEventArgs e)
    {
        OpenFileAndInstall("resourcepack");
    }
    private void SelectShader_Click(object sender, RoutedEventArgs e)
    {
        OpenFileAndInstall("shaderpack");
    }

    private async void OpenFileAndInstall(string type)
    {
        CustomPopup.IsOpen = false;

        var dialog = new OpenFileDialog
        {
            Filter = "Supported Files|*.jar;*.zip"
        };

        if (dialog.ShowDialog() != true)
            return;

        await InstallCustomContent(dialog.FileName, type);
    }
    private void Window_Click(
        object sender,
        MouseButtonEventArgs e)
    {
        if (!CustomPopup.IsOpen)
            return;

        var clickedElement =
            e.OriginalSource as DependencyObject;

        while (clickedElement != null)
        {
            if (clickedElement == CustomPopup)
                return;

            clickedElement =
                VisualTreeHelper.GetParent(
                    clickedElement);
        }

        CustomPopup.IsOpen = false;
    }
   
    private async Task InstallCustomContent(string filePath, string type)
    {
        if (currentProfile == null)
            return;

        try
        {
            string basePath = GetCurrentProfileDirectory();

            string folder = type switch
            {
                "mod" => "mods",
                "resourcepack" => "resourcepacks",
                "shaderpack" => "shaderpacks",
                _ => null
            };

            if (folder == null)
                return;

            string targetPath = Path.Combine(basePath, folder);
            Directory.CreateDirectory(targetPath);

            string fileName = Path.GetFileName(filePath);
            string contentName =
                Path.GetFileNameWithoutExtension(fileName);
            string destFile = Path.Combine(targetPath, fileName);

            File.Copy(filePath, destFile, true);

            string description = "Custom Content";
            string iconUrl = null;
            string marketplaceName = null;

            if (type == "mod")
            {
                description = "";

                using HttpClient client = new();
                ModrinthProject metadata =
                    await FindImportedModMetadataAsync(
                        client,
                        contentName,
                        null);

                if (metadata != null)
                {
                    description = metadata.description ?? "";
                    iconUrl = metadata.icon_url;
                    marketplaceName = metadata.title;
                }
            }

            AddToProfileJson(
                contentName,
                description,
                iconUrl,
                type,
                marketplaceName: marketplaceName);

            ReloadProfileData();
            LoadInstalledContent();

            ShowToast($"Added {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            ShowToast("Error to Add file!");
        }
    }
    
    private void AssignMissingProfilePictures()
    {
        if (!File.Exists(filePath))
            return;

        var profiles =
            JsonSerializer.Deserialize<List<Profile>>(
                File.ReadAllText(filePath));

        if (profiles == null)
            return;

        bool changed = false;

        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.ProfilePicture))
            {
                profile.ProfilePicture =
                    GetRandomProfilePicture();

                changed = true;
            }
        }

        if (changed)
        {
            File.WriteAllText(
                filePath,
                JsonSerializer.Serialize(
                    profiles,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
    }
    private string GetRandomProfilePicture()
    {
        return profilePictures[
            profileIconRandom.Next(profilePictures.Length)
        ];
    }
    private string GetRandomProfileIcon()
    {
        string[] icons =
        {
            "pack://application:,,,/Assets/pp/1.png"
        };

        return icons[profileIconRandom.Next(icons.Length)];
    }
private void LoadProfiles()
{
    ProfilesList.Children.Clear();
    UpdateProfileSourceFilterButtons();

    if (!File.Exists(filePath))
    {
        UpdateProfilesUI();
        return;
    }

    string json = File.ReadAllText(filePath);
    var profiles = JsonSerializer.Deserialize<List<Profile>>(json);

    if (profiles == null)
        return;

    string search = SearchBox?.Text?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(search) ||
        string.Equals(
            search,
            LocalizationManager.Instance.Translate("Search..."),
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(search, "Search...", StringComparison.OrdinalIgnoreCase))
    {
        search = string.Empty;
    }

    foreach (var p in profiles.OrderBy(
                 profile => profile.Name,
                 StringComparer.OrdinalIgnoreCase))
    {
        if (!MatchesProfileSourceFilter(p) ||
            (!string.IsNullOrWhiteSpace(search) &&
             !p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }


        Border container = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString("#1E293B"),
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#334155"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Tag = p
        };

        // Highlight the container border on hover.
        container.MouseEnter += (s, e) => container.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#3B82F6");
        container.MouseLeave += (s, e) => container.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#334155");


        Grid innerGrid = new Grid();
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });


        Image profileIcon = new Image
        {
            Width = 42,
            Height = 42,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.UniformToFill
        };

        profileIcon.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Assets/pp/{p.ProfilePicture}", UriKind.Absolute));

        // Round the image corners.
        RectangleGeometry clipGeometry = new RectangleGeometry
        {
            Rect = new Rect(0, 0, 42, 42),
            RadiusX = 8,
            RadiusY = 8
        };
        profileIcon.Clip = clipGeometry;

        Grid.SetRowSpan(profileIcon, 2);
        Grid.SetColumn(profileIcon, 0);
        innerGrid.Children.Add(profileIcon);


        TextBlock title = new TextBlock
        {
            Text = p.Name,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetRow(title, 0);
        Grid.SetColumn(title, 1);
        innerGrid.Children.Add(title);


        TextBlock sub = new TextBlock
        {
            Text = p.IsModrinthProfile
                ? $"Modrinth profile • {p.Loader} • {p.Version}"
                : p.IsNoRiskProfile
                    ? $"NoRisk profile • {p.Loader} • {p.Version}"
                    : $"{p.Loader} • {p.Version}",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#94A3B8"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetRow(sub, 1);
        Grid.SetColumn(sub, 1);
        innerGrid.Children.Add(sub);


        Border clickable = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };

        clickable.MouseLeftButtonUp +=
            (s, e) =>
            {
                e.Handled = true;

                OpenProfile(p);
            };
        Grid.SetRowSpan(clickable, 2);
        Grid.SetColumn(clickable, 1);
        innerGrid.Children.Add(clickable);


        StackPanel actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };


        Button editBtn = new Button
        {
            Content = "✏️",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#64748B"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Width = 32,
            Height = 32,
            FontSize = 14,
            Margin = new Thickness(0, 0, 4, 0)
        };

        editBtn.Template = new ControlTemplate(typeof(Button))
        {
            VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
        };

        editBtn.MouseEnter += (s, e) => editBtn.Foreground = (Brush)new BrushConverter().ConvertFromString("#60A5FA");
        editBtn.MouseLeave += (s, e) => editBtn.Foreground = (Brush)new BrushConverter().ConvertFromString("#64748B");

        // Open the edit panel.
        editBtn.Click += (s, e) =>
        {
            currentProfile = p;
            EditProfile_Click(s, e);
        };


        Button deleteBtn = new Button
        {
            Content = "🗑",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#64748B"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Width = 32,
            Height = 32,
            FontSize = 14
        };

        deleteBtn.Template = new ControlTemplate(typeof(Button))
        {
            VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
        };

        deleteBtn.MouseEnter += (s, e) => deleteBtn.Foreground = (Brush)new BrushConverter().ConvertFromString("#EF4444");
        deleteBtn.MouseLeave += (s, e) => deleteBtn.Foreground = (Brush)new BrushConverter().ConvertFromString("#64748B");

        deleteBtn.Click += (s, e) => DeleteProfile(p);

        // Source launchers own the metadata and files of synchronized profiles.
        if (!p.IsExternalProfile)
        {
            actions.Children.Add(editBtn);
            actions.Children.Add(deleteBtn);
        }
        else
        {
            Button importButton = new Button
            {
                Content = "Import to JaxClient",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2563EB")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(14, 9, 14, 9),
                MinHeight = 40,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            MakeRounded(importButton);
            importButton.Click += async (_, _) =>
            {
                importButton.IsEnabled = false;
                const string importTaskName = "Importing profile to JaxClient...";
                StartTask(importTaskName);
                try
                {
                    await ImportExternalProfileAsync(p);
                }
                finally
                {
                    StopTask(importTaskName);
                    importButton.IsEnabled = true;
                }
            };
            actions.Children.Add(importButton);
        }


        Grid.SetRowSpan(actions, 2);
        Grid.SetColumn(actions, 2);
        innerGrid.Children.Add(actions);


        container.Child = innerGrid;
        ProfilesList.Children.Add(container);
    }
    UpdateProfilesUI();
}    private void DeleteProfile(Profile profile)
{
    if (profile.IsExternalProfile)
    {
        ShowToast("External profiles are managed by their source launcher.");
        return;
    }

    profileToDelete = profile;

    ConfirmText.Text = LocalizationManager.Instance.Translate(
        $"Delete profile '{profile.Name}'?");

    ConfirmOverlay.Visibility = Visibility.Visible;
}

private async Task ImportExternalProfileAsync(Profile externalProfile)
{
    if (!externalProfile.IsExternalProfile ||
        string.IsNullOrWhiteSpace(filePath))
    {
        return;
    }

    string sourceDirectory = GetProfileDirectory(externalProfile);
    if (!Directory.Exists(sourceDirectory))
    {
        ShowToast("The source profile folder no longer exists.");
        return;
    }

    List<Profile> profiles = File.Exists(filePath)
        ? JsonSerializer.Deserialize<List<Profile>>(
            await File.ReadAllTextAsync(filePath)) ?? new List<Profile>()
        : new List<Profile>();

    string profileName = MakeValidProfileFolderName(externalProfile.Name);
    string baseName = profileName;
    int suffix = 1;
    while (profiles.Any(profile =>
               string.Equals(
                   profile.Name,
                   profileName,
                   StringComparison.OrdinalIgnoreCase)) ||
           Directory.Exists(GetNativeProfileDirectory(profileName)))
    {
        profileName = $"{baseName} (JaxClient {suffix++})";
    }

    string destinationDirectory = GetNativeProfileDirectory(profileName);
    string stagingDirectory = Path.Combine(
        Path.GetDirectoryName(destinationDirectory)!,
        $".import-{Guid.NewGuid():N}");
    bool destinationCreated = false;
    bool profileSaved = false;
    int copiedNoRiskCachedModCount = 0;

    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationDirectory)!);
        await Task.Run(() =>
            CopyProfileDirectory(sourceDirectory, stagingDirectory));
        if (externalProfile.IsNoRiskProfile)
        {
            copiedNoRiskCachedModCount = await Task.Run(() =>
                CopyNoRiskCachedMods(externalProfile, stagingDirectory));
        }

        Directory.Move(stagingDirectory, destinationDirectory);
        destinationCreated = true;

        Profile importedProfile = new()
        {
            Name = profileName,
            Loader = externalProfile.Loader,
            LoaderVersion = externalProfile.LoaderVersion,
            Version = externalProfile.Version,
            ProfilePicture = externalProfile.ProfilePicture,
            NrcEnabled = externalProfile.NrcEnabled,
            Mods = CloneImportedContent(externalProfile.Mods),
            RP = CloneImportedContent(externalProfile.RP),
            Shaders = CloneImportedContent(externalProfile.Shaders)
        };
        profiles.Add(importedProfile);

        string temporaryFilePath = filePath + ".import.tmp";
        await File.WriteAllTextAsync(
            temporaryFilePath,
            JsonSerializer.Serialize(
                profiles,
                new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            File.Move(temporaryFilePath, filePath, overwrite: true);
            profileSaved = true;
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }

        LoadProfiles();
        OpenProfile(importedProfile);
        string importedMessage =
            $"Imported '{profileName}' to JaxClient.";
        if (externalProfile.IsNoRiskProfile &&
            (copiedNoRiskCachedModCount > 0 ||
             externalProfile.NoRiskMissingCachedModCount > 0 ||
             !string.IsNullOrWhiteSpace(externalProfile.NoRiskCacheWarning)))
        {
            importedMessage +=
                $" Copied {copiedNoRiskCachedModCount} NoRisk cache mod(s).";
            if (externalProfile.NoRiskMissingCachedModCount > 0)
            {
                importedMessage +=
                    $" {externalProfile.NoRiskMissingCachedModCount} compatible cache mod(s) were not available.";
            }

            if (!string.IsNullOrWhiteSpace(externalProfile.NoRiskCacheWarning))
            {
                importedMessage +=
                    $" Pack mods could not be fully resolved: {externalProfile.NoRiskCacheWarning}";
            }
        }

        ShowToast(importedMessage);
    }
    catch (Exception ex)
    {
        try
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }

            if (!profileSaved &&
                destinationCreated &&
                Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }
        }
        catch (Exception cleanupException)
        {
            Console.WriteLine(
                $"Could not clean up failed profile import: {cleanupException}");
            ShowToast(
                $"Import failed and cleanup was incomplete: {cleanupException.Message}");
        }

        Console.WriteLine(
            $"External profile import failed for '{externalProfile.Name}': {ex}");
        ShowToast($"Profile import failed: {ex.Message}");
    }
}

private static int CopyNoRiskCachedMods(
    Profile externalProfile,
    string destinationDirectory)
{
    if (externalProfile.NoRiskCachedMods.Count == 0)
    {
        return 0;
    }

    string cacheDirectory = NoRiskProfileSynchronizer.GetModCacheDirectory();
    string modsDirectory = Path.Combine(destinationDirectory, "mods");
    HashSet<string> existingFileNames = Directory.Exists(modsDirectory)
        ? Directory.EnumerateFiles(
                modsDirectory,
                "*",
                SearchOption.AllDirectories)
            .Select(path => NoRiskProfileSynchronizer.GetEffectiveModFileName(
                Path.GetFileName(path)))
        .OfType<string>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    int copiedCount = 0;

    foreach (NoRiskCachedModSnapshot cachedMod in externalProfile.NoRiskCachedMods)
    {
        if (!cachedMod.IsInCache)
        {
            continue;
        }

        string fileName = cachedMod.FileName;
        if (Path.IsPathRooted(fileName) ||
            !string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal) ||
            !Path.GetExtension(fileName).Equals(
                ".jar",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The NoRisk cache contains an invalid mod filename: '{fileName}'.");
        }

        if (!existingFileNames.Add(fileName))
        {
            continue;
        }

        string sourcePath = Path.Combine(cacheDirectory, fileName);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                $"The NoRisk cached mod '{fileName}' is no longer available.",
                sourcePath);
        }

        Directory.CreateDirectory(modsDirectory);
        File.Copy(
            sourcePath,
            Path.Combine(modsDirectory, fileName),
            overwrite: false);
        copiedCount++;
    }

    return copiedCount;
}

private string GetNativeProfileDirectory(string profileName) =>
    Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "Profiles",
        profileName);

private static string MakeValidProfileFolderName(string name)
{
    string safeName = name;
    foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
    {
        safeName = safeName.Replace(invalidCharacter, '_');
    }

    safeName = safeName.Trim().TrimEnd('.', ' ');
    return string.IsNullOrWhiteSpace(safeName) ? "Imported Profile" : safeName;
}

private static List<ContentItem> CloneImportedContent(
    IEnumerable<ContentItem>? items) =>
    (items ?? Enumerable.Empty<ContentItem>())
        .Where(item => item != null)
        .Select(item => new ContentItem
        {
            Name = item.Name,
            Description = item.Description,
            IconUrl = item.IconUrl,
            VersionProvider = item.VersionProvider,
            ProjectId = item.ProjectId,
            VersionId = item.VersionId,
            VersionName = item.VersionName,
            VersionFileName = item.VersionFileName,
            MarketplaceName = item.MarketplaceName,
            Enabled = item.Enabled
        })
        .ToList();

private static void CopyProfileDirectory(
    string sourceDirectory,
    string destinationDirectory)
{
    Directory.CreateDirectory(destinationDirectory);
    foreach (string file in Directory.EnumerateFiles(
                 sourceDirectory,
                 "*",
                 SearchOption.TopDirectoryOnly))
    {
        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
        {
            continue;
        }

        File.Copy(
            file,
            Path.Combine(destinationDirectory, Path.GetFileName(file)));
    }

    foreach (string directory in Directory.EnumerateDirectories(
                 sourceDirectory,
                 "*",
                 SearchOption.TopDirectoryOnly))
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
        {
            continue;
        }

        CopyProfileDirectory(
            directory,
            Path.Combine(destinationDirectory, Path.GetFileName(directory)));
    }
}

private async Task SynchronizeModrinthProfilesAsync()
{
    try
    {
        IReadOnlyList<ModrinthProfileSnapshot>? snapshots =
            await Task.Run(ModrinthProfileSynchronizer.ReadProfiles);
        if (snapshots == null || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        HashSet<string> supportedMinecraftVersions =
            await GetSupportedMinecraftVersionsAsync();

        snapshots = snapshots
            .Where(snapshot =>
                supportedMinecraftVersions.Contains(snapshot.GameVersion) &&
                (snapshot.Loader.Equals(
                     "Fabric",
                     StringComparison.OrdinalIgnoreCase) ||
                 snapshot.Loader.Equals(
                     "Forge",
                     StringComparison.OrdinalIgnoreCase) ||
                 snapshot.Loader.Equals(
                     "NeoForge",
                     StringComparison.OrdinalIgnoreCase) ||
                 snapshot.Loader.Equals(
                     "Vanilla",
                     StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        List<Profile> existingProfiles = File.Exists(filePath)
            ? JsonSerializer.Deserialize<List<Profile>>(
                await File.ReadAllTextAsync(filePath)) ?? new List<Profile>()
            : new List<Profile>();
        Dictionary<string, Profile> existingModrinthProfiles =
            existingProfiles
                .Where(profile => profile.IsModrinthProfile)
                .GroupBy(
                    profile => profile.ModrinthInstanceId!,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);

        List<Profile> syncedProfiles = snapshots
            .Select(snapshot =>
            {
                existingModrinthProfiles.TryGetValue(
                    snapshot.InstanceId,
                    out Profile? previous);

                List<ContentItem> ContentItems(string type) =>
                    snapshot.Content
                        .Where(item => item.Type == type)
                        .Select(item => new ContentItem
                        {
                            Name = item.Name,
                            Description = string.Empty,
                            IconUrl = string.Empty,
                            VersionProvider = "Modrinth",
                            ProjectId = item.ProjectId ?? string.Empty,
                            VersionId = item.VersionId ?? string.Empty,
                            VersionName = string.Empty,
                            VersionFileName = Path.GetFileName(item.RelativePath),
                            MarketplaceName = "Modrinth",
                            Enabled = item.Enabled,
                            ModrinthRelativePath = item.RelativePath
                        })
                        .ToList();

                return new Profile
                {
                    Name = snapshot.Name,
                    Loader = snapshot.Loader,
                    LoaderVersion = snapshot.LoaderVersion,
                    Version = snapshot.GameVersion,
                    ProfilePicture =
                        previous?.ProfilePicture ?? GetRandomProfilePicture(),
                    NrcEnabled = previous?.NrcEnabled ?? false,
                    ModrinthInstanceId = snapshot.InstanceId,
                    ModrinthRelativePath = snapshot.RelativePath,
                    ModrinthDirectory = snapshot.Directory,
                    ModrinthInstallStage = snapshot.InstallStage,
                    ModrinthUpdateChannel = snapshot.UpdateChannel,
                    ModrinthIconPath = snapshot.IconPath,
                    ModrinthLastPlayed = snapshot.LastPlayed,
                    ModrinthSubmittedTimePlayed =
                        snapshot.SubmittedTimePlayed,
                    ModrinthRecentTimePlayed = snapshot.RecentTimePlayed,
                    ModrinthContentSetId = snapshot.ContentSetId,
                    ModrinthContentSetName = snapshot.ContentSetName,
                    ModrinthContentSetStatus = snapshot.ContentSetStatus,
                    ModrinthSourceKind = snapshot.SourceKind,
                    ModrinthProtocolVersion = snapshot.ProtocolVersion,
                    ModrinthLinkKind = snapshot.LinkKind,
                    ModrinthProjectId = snapshot.ProjectId,
                    ModrinthVersionId = snapshot.VersionId,
                    ModrinthCreated = snapshot.Created,
                    ModrinthModified = snapshot.Modified,
                    Mods = ContentItems("mod"),
                    RP = ContentItems("resourcepack"),
                    Shaders = ContentItems("shader")
                };
            })
            .ToList();

        List<Profile> mergedProfiles = existingProfiles
            .Where(profile => !profile.IsModrinthProfile)
            .Concat(syncedProfiles)
            .ToList();
        string temporaryPath = filePath + ".modrinth-sync.tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(
                mergedProfiles,
                new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, filePath, overwrite: true);

        LoadLastProfile();
        LoadProfiles();
        if (syncedProfiles.Count > 0)
        {
            ShowToast($"Synchronized {syncedProfiles.Count} Modrinth profiles.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Modrinth profile sync failed: {ex}");
        ShowToast($"Modrinth profile sync failed: {ex.Message}");
    }
}

private async Task SynchronizeNoRiskProfilesAsync()
{
    try
    {
        IReadOnlyList<NoRiskProfileSnapshot>? snapshots =
            await Task.Run(NoRiskProfileSynchronizer.ReadProfiles);
        if (snapshots == null || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        HashSet<string> supportedMinecraftVersions =
            await GetSupportedMinecraftVersionsAsync();
        snapshots = snapshots
            .Where(snapshot =>
                supportedMinecraftVersions.Contains(snapshot.GameVersion) &&
                IsSupportedProfileLoader(snapshot.Loader))
            .ToArray();

        List<Profile> existingProfiles = File.Exists(filePath)
            ? JsonSerializer.Deserialize<List<Profile>>(
                await File.ReadAllTextAsync(filePath)) ?? new List<Profile>()
            : new List<Profile>();
        Dictionary<string, Profile> existingNoRiskProfiles =
            existingProfiles
                .Where(profile => profile.IsNoRiskProfile)
                .GroupBy(
                    profile => profile.NoRiskProfileId!,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);

        List<Profile> syncedProfiles = snapshots
            .Select(snapshot =>
            {
                existingNoRiskProfiles.TryGetValue(
                    snapshot.Id,
                    out Profile? previous);

                List<ContentItem> ContentItems(string type) =>
                    snapshot.Content
                        .Where(item => item.Type == type)
                        .Select(item =>
                        {
                            string fileName = NoRiskProfileSynchronizer
                                .GetEffectiveModFileName(
                                    Path.GetFileName(item.RelativePath));
                            NoRiskCachedModSnapshot? mod = type == "mod"
                                ? snapshot.CachedMods.FirstOrDefault(candidate =>
                                    string.Equals(
                                        candidate.FileName,
                                        fileName,
                                        StringComparison.OrdinalIgnoreCase))
                                : null;
                            bool installed = File.Exists(Path.Combine(
                                snapshot.Directory,
                                item.RelativePath.Replace(
                                    '/',
                                    Path.DirectorySeparatorChar)));
                            string source = mod?.SourceType ?? "local";
                            string state = installed
                                ? item.Enabled ? "installed" : "disabled"
                                : mod?.IsInCache == true
                                    ? "cached; copied on launch"
                                    : "downloaded on launch";

                            return new ContentItem
                            {
                                Name = mod?.Name ?? item.Name,
                                Description = type == "mod"
                                    ? $"NoRisk {source} mod • {state}"
                                    : "Installed in NoRisk profile",
                                IconUrl = string.Empty,
                                VersionProvider = $"NoRisk • {source}",
                                ProjectId = string.Empty,
                                VersionId = string.Empty,
                                VersionName = mod?.Version ?? string.Empty,
                                VersionFileName = fileName,
                                MarketplaceName = "NoRisk",
                                Enabled = item.Enabled,
                                ModrinthRelativePath = item.RelativePath
                            };
                        })
                        .ToList();

                return new Profile
                {
                    Name = snapshot.Name,
                    Loader = snapshot.Loader,
                    LoaderVersion = snapshot.LoaderVersion,
                    Version = snapshot.GameVersion,
                    ProfilePicture =
                        previous?.ProfilePicture ?? GetRandomProfilePicture(),
                    NoRiskProfileId = snapshot.Id,
                    NoRiskRelativePath = snapshot.RelativePath,
                    NoRiskDirectory = snapshot.Directory,
                    NoRiskCachedMods = snapshot.CachedMods.ToList(),
                    NoRiskMissingCachedModCount = snapshot.MissingCachedModCount,
                    NoRiskCacheWarning = snapshot.CacheWarning,
                    Mods = ContentItems("mod"),
                    RP = ContentItems("resourcepack"),
                    Shaders = ContentItems("shader")
                };
            })
            .ToList();

        List<Profile> mergedProfiles = existingProfiles
            .Where(profile => !profile.IsNoRiskProfile)
            .Concat(syncedProfiles)
            .ToList();
        string temporaryPath = filePath + ".norisk-sync.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(
                    mergedProfiles,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        LoadLastProfile();
        LoadProfiles();
        if (syncedProfiles.Count > 0)
        {
            string? cacheWarning = syncedProfiles
                .FirstOrDefault(profile =>
                    !string.IsNullOrWhiteSpace(profile.NoRiskCacheWarning))
                ?.NoRiskCacheWarning;
            ShowToast(cacheWarning == null
                ? $"Synchronized {syncedProfiles.Count} NoRisk profiles."
                : $"Synchronized {syncedProfiles.Count} NoRisk profiles. Pack cache warning: {cacheWarning}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"NoRisk profile sync failed: {ex}");
        ShowToast($"NoRisk profile sync failed: {ex.Message}");
    }
}

private async Task<HashSet<string>> GetSupportedMinecraftVersionsAsync()
{
    using HttpClient versionClient = new();
    string manifestJson = await versionClient.GetStringAsync(
        "https://launchermeta.mojang.com/mc/game/version_manifest.json");
    using JsonDocument manifest = JsonDocument.Parse(manifestJson);
    return manifest
        .RootElement
        .GetProperty("versions")
        .EnumerateArray()
        .Select(version => version.GetProperty("id").GetString())
        .Where(version => !string.IsNullOrWhiteSpace(version))
        .Select(version => version!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

private static bool IsSupportedProfileLoader(string? loader) =>
    string.Equals(loader, "Fabric", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(loader, "Forge", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(loader, "NeoForge", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(loader, "Vanilla", StringComparison.OrdinalIgnoreCase);
   
    
    
private async Task<bool> DownloadAssets(
    string version,
    bool strict = false)
{
    
    
    try
    {
        using HttpClient client = new HttpClient();


        string manifestJson =
            await client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
        using JsonDocument manifestDoc = JsonDocument.Parse(manifestJson);

        string versionUrl = null;

        foreach (var v in manifestDoc.RootElement.GetProperty("versions").EnumerateArray())
        {
            if (v.GetProperty("id").GetString() == version)
            {
                versionUrl = v.GetProperty("url").GetString();
                break;
            }
        }

        if (versionUrl == null)
        {
            
            ShowToast("Version not found!");

            return false;
        }


        string versionJson = await client.GetStringAsync(versionUrl);
        using JsonDocument versionDoc = JsonDocument.Parse(versionJson);

        var assetIndex = versionDoc.RootElement.GetProperty("assetIndex");

        string assetUrl = assetIndex.GetProperty("url").GetString();
        string assetId = assetIndex.GetProperty("id").GetString();

        if (string.IsNullOrEmpty(assetUrl))
        {
            
            ShowToast("Asset download error!");

            return false;
        }


        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "assets"
        );

        string indexesPath = System.IO.Path.Combine(basePath, "indexes");
        Directory.CreateDirectory(indexesPath);

        string indexFile = System.IO.Path.Combine(indexesPath, $"{assetId}.json");


        string indexJson = await client.GetStringAsync(assetUrl);
        File.WriteAllText(indexFile, indexJson);

        using JsonDocument indexDoc = JsonDocument.Parse(indexJson);

        var objects = indexDoc.RootElement.GetProperty("objects");

        int counter = 0;

        foreach (var entry in objects.EnumerateObject())
        {
            try
            {
                string hash = entry.Value.GetProperty("hash").GetString();

                if (string.IsNullOrEmpty(hash))
                    continue;

                string sub = hash.Substring(0, 2);

                // Resolve the final asset path.
                string fullPath = System.IO.Path.Combine(basePath, "objects", sub, hash);


                if (File.Exists(fullPath))
                    continue;


                string url = $"https://resources.download.minecraft.net/{sub}/{hash}";

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));

                ThrowIfCancelled();

                byte[] data = await client.GetByteArrayAsync(url);

                ThrowIfCancelled();
                if (strict && data.Length == 0)
                    throw new InvalidDataException(
                        $"Minecraft asset download was empty: {entry.Name}.");
                File.WriteAllBytes(fullPath, data);

                counter++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Could not download Minecraft asset {entry.Name}: {ex.Message}");
                if (strict)
                    return false;
            }
        }

        return true;
    }
    catch (Exception ex)
    {
        ShowToast("Asset download error!");
        Console.WriteLine(ex);
        return false;
    }
}
private async Task DownloadJavaVersions()
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return;
        }

        using HttpClient client = new HttpClient();

        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "Java"
        );

        Directory.CreateDirectory(basePath);


        async Task InstallJava(string version, string url)
        {
            string extractPath = System.IO.Path.Combine(basePath, $"jdk-{version}");
            string javaExePath = System.IO.Path.Combine(extractPath, "bin", "java.exe");


            if (File.Exists(javaExePath))
            {
                Console.WriteLine($"✅ Java {version} already installed");
                return;
            }


            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }

            Console.WriteLine($"⬇ Downloading Java {version}...");

            string zipPath = System.IO.Path.Combine(basePath, $"java{version}.zip");


            if (!File.Exists(zipPath))
            {
                ThrowIfCancelled();

                byte[] data = await client.GetByteArrayAsync(url);

                ThrowIfCancelled();
                File.WriteAllBytes(zipPath, data);
            }

            // Scan existing folders before extraction to identify the new folder.
            var dirsBefore = Directory.GetDirectories(basePath);


            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, basePath, true);

            // Scan folders after extraction.
            var dirsAfter = Directory.GetDirectories(basePath);

            // Find the newly added folder.
            string extractedDir = null;
            foreach (var dir in dirsAfter)
            {
                if (!System.Linq.Enumerable.Contains(dirsBefore, dir) && !dir.EndsWith($"jdk-{version}"))
                {
                    extractedDir = dir;
                    break;
                }
            }

            // Rename the folder to jdk-VERSION.
            if (extractedDir != null && Directory.Exists(extractedDir))
            {
                Directory.Move(extractedDir, extractPath);
            }


            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }


            if (File.Exists(javaExePath))
            {
                Console.WriteLine($"✅ Java {version} installed");
            }
            else
            {
                ShowToast($"Java {version} install failed!");
            }
        }


        await InstallJava("17",
            "https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jdk/hotspot/normal/eclipse");

        await InstallJava("21",
            "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse");

        await InstallJava("25",
            "https://api.adoptium.net/v3/binary/latest/25/ga/windows/x64/jdk/hotspot/normal/eclipse");
    }
    catch (Exception)
    {
        ShowToast("Java install error!");
    }
}private async Task<bool> DownloadFabricLibraries(
    string mcVersion,
    string loaderVersion,
    bool forceDownload = false)
    {
        try
        {
            if (cancelLaunch)
            {
                ResetLaunchUI();
                return false;
            }

            using HttpClient client = new HttpClient();

            string url = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";

            string json = await client.GetStringAsync(url);

            using JsonDocument doc = JsonDocument.Parse(json);

            var libs = doc.RootElement.GetProperty("libraries");

            foreach (var lib in libs.EnumerateArray())
            {
                if (!lib.TryGetProperty("name", out var nameProp))
                    continue;

                string name = nameProp.GetString();

                if (string.IsNullOrWhiteSpace(name))
                    continue;


                string[] parts = name.Split(':');
                if (parts.Length < 3)
                    continue;

                string group = parts[0].Replace('.', '/');
                string artifact = parts[1];
                string ver = parts[2];

                string jarName = $"{artifact}-{ver}.jar";

                string downloadUrl = $"https://maven.fabricmc.net/{group}/{artifact}/{ver}/{jarName}";

                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".JAXSTORE",
                    "JaxClient",
                    "meta",
                    "libraries",
                    mcVersion,
                    group,
                    artifact,
                    ver
                );

                Directory.CreateDirectory(path);

                string fullPath = System.IO.Path.Combine(path, jarName);

                if (!forceDownload && File.Exists(fullPath))
                    continue;
                
               ThrowIfCancelled(); 
                byte[] data = await client.GetByteArrayAsync(downloadUrl);
                ThrowIfCancelled();
               if (data.Length == 0)
                   throw new InvalidDataException(
                       $"Fabric library download was empty: {jarName}.");
               File.WriteAllBytes(fullPath, data);
            }
            return true;
        }
        catch (Exception ex)
        {
            ShowToast(ex.ToString());
            return false;
        }
    }

    private async Task<bool> DownloadMinecraftClient(
    string version,
    bool forceDownload = false)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return false;
        }

        string path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "versions",
            version
        );

        string jarPath = System.IO.Path.Combine(path, $"{version}.jar");


        if (!forceDownload && File.Exists(jarPath))
        {
            return new FileInfo(jarPath).Length > 0;
        }

        using HttpClient client = new HttpClient();


        string manifestJson = await client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
        using JsonDocument manifestDoc = JsonDocument.Parse(manifestJson);

        string versionUrl = null;

        version = version.Trim();

        foreach (var v in manifestDoc.RootElement.GetProperty("versions").EnumerateArray())
        {
            string id = v.GetProperty("id").GetString();

            if (string.Equals(id, version, StringComparison.OrdinalIgnoreCase))
            {
                versionUrl = v.GetProperty("url").GetString();
                break;
            }
        }

        if (versionUrl == null)
        {
            ShowToast($"Version NOT found: {version}");
            return false;
        }


        string versionJson = await client.GetStringAsync(versionUrl);
        using JsonDocument versionDoc = JsonDocument.Parse(versionJson);

        string clientUrl = versionDoc
            .RootElement
            .GetProperty("downloads")
            .GetProperty("client")
            .GetProperty("url")
            .GetString();

        Directory.CreateDirectory(path);


        ThrowIfCancelled();
        byte[] jar = await client.GetByteArrayAsync(clientUrl);
        ThrowIfCancelled();
        if (jar.Length == 0)
            throw new InvalidDataException(
                $"Minecraft {version} download was empty.");

        string temporaryJarPath = jarPath + ".reinstall";
        try
        {
            File.WriteAllBytes(temporaryJarPath, jar);
            File.Move(temporaryJarPath, jarPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryJarPath))
                File.Delete(temporaryJarPath);
        }


        if (File.Exists(jarPath))
        {
            Console.Write($"Minecraft {version} installed!");
            return new FileInfo(jarPath).Length > 0;
        }
        else
            ShowToast("Minecraft install failed!");
        return false;
    }
    catch (Exception ex)
    {
        ShowToast("Minecraft download failed");
        MessageBox.Show(ex.ToString());
        return false;
    }
}
private async Task<bool> DownloadForgeProfile(string mcVersion, string forgeVersion)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return false;
        }

        using HttpClient client = new HttpClient();

        string forgeProfileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            "ForgeProfiles");

        Directory.CreateDirectory(forgeProfileDir);

        string launcherProfileFile = Path.Combine(
            forgeProfileDir,
            "launcher_profiles.json");

        if (!File.Exists(launcherProfileFile))
        {
            File.WriteAllText(
                launcherProfileFile,
                """
                {
                  "profiles": {}
                }
                """);
        }

        string installerUrl =
            $"https://maven.minecraftforge.net/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}-installer.jar";

        string installerFile = Path.Combine(
            forgeProfileDir,
            $"forge-{forgeVersion}-installer.jar");

        Console.WriteLine("=== FORGE DOWNLOAD ===");
        Console.WriteLine("MC Version: " + mcVersion);
        Console.WriteLine("Forge Version: " + forgeVersion);
        Console.WriteLine("Forge Directory: " + forgeProfileDir);
        Console.WriteLine("Installer URL: " + installerUrl);
        Console.WriteLine("Installer File: " + installerFile);

        if (!File.Exists(installerFile))
        {
            Console.WriteLine("Downloading Forge installer...");
            ThrowIfCancelled();
            byte[] data =
                await client.GetByteArrayAsync(installerUrl);
            ThrowIfCancelled();
            await File.WriteAllBytesAsync(
                installerFile,
                data);

            Console.WriteLine("Forge installer downloaded");
            Console.WriteLine("Size: " + data.Length);
        }

        string javaExe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "Java",
            "jdk-21",
            "bin",
            "java.exe");

        Console.WriteLine("=== FORGE INSTALL ===");
        Console.WriteLine("Java: " + javaExe);
        Console.WriteLine("WorkingDir: " + forgeProfileDir);
        Console.WriteLine("launcher_profiles.json Exists: "
                          + File.Exists(launcherProfileFile));

        ProcessStartInfo install = new ProcessStartInfo
        {
            FileName = javaExe,
            Arguments =
                $"-jar \"{installerFile}\" --installClient",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = forgeProfileDir
        };

        Console.WriteLine("Arguments:");
        Console.WriteLine(install.Arguments);

        Process p = Process.Start(install);

        if (p == null)
            throw new Exception("Forge installer process is null");

        string output =
            await p.StandardOutput.ReadToEndAsync();

        string error =
            await p.StandardError.ReadToEndAsync();

        await p.WaitForExitAsync();

        Console.WriteLine("=== FORGE OUTPUT ===");
        Console.WriteLine(output);

        Console.WriteLine("=== FORGE ERROR ===");
        Console.WriteLine(error);

        Console.WriteLine("=== FORGE EXIT CODE ===");
        Console.WriteLine(p.ExitCode);
        return p.ExitCode == 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine("=== FORGE INSTALL ERROR ===");
        Console.WriteLine(ex);
        return false;
    }
}

private async Task<bool> DownloadNeoForgeProfile(
    string minecraftVersion,
    string neoForgeVersion,
    bool forceDownload = false)
{
    try
    {
        if (cancelLaunch)
            throw new OperationCanceledException();

        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient");
        string metaPath = Path.Combine(basePath, "meta");
        if (!forceDownload &&
            FindNeoForgeVersionJson(
                metaPath,
                minecraftVersion,
                neoForgeVersion) != null)
        {
            return true;
        }

        string installerDirectory = Path.Combine(metaPath, "installers");
        Directory.CreateDirectory(installerDirectory);

        string installerPath = Path.Combine(
            installerDirectory,
            $"neoforge-{neoForgeVersion}-installer.jar");
        if (forceDownload ||
            !File.Exists(installerPath) ||
            new FileInfo(installerPath).Length == 0)
        {
            using HttpClient client = new();
            string installerUrl =
                $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{Uri.EscapeDataString(neoForgeVersion)}/neoforge-{Uri.EscapeDataString(neoForgeVersion)}-installer.jar";
            byte[] installerData =
                await client.GetByteArrayAsync(installerUrl);
            if (installerData.Length == 0)
                throw new InvalidDataException(
                    $"NeoForge installer download was empty: {neoForgeVersion}.");
            await File.WriteAllBytesAsync(installerPath, installerData);
        }

        string javaMajor = minecraftVersion.StartsWith("26.", StringComparison.Ordinal)
            ? "25"
            : minecraftVersion.StartsWith("1.21.", StringComparison.Ordinal) ||
              minecraftVersion is "1.21" or "1.20.5" or "1.20.6"
                ? "21"
                : "17";
        string javaPath = Path.Combine(
            metaPath,
            "Java",
            $"jdk-{javaMajor}",
            "bin",
            "java.exe");
        if (!File.Exists(javaPath))
        {
            await DownloadJavaVersions();
            if (!File.Exists(javaPath))
                throw new FileNotFoundException(
                    $"Java {javaMajor} is required to install NeoForge.",
                    javaPath);
        }

        string launcherProfilesPath =
            Path.Combine(metaPath, "launcher_profiles.json");
        if (!File.Exists(launcherProfilesPath))
        {
            await File.WriteAllTextAsync(
                launcherProfilesPath,
                """{"profiles":{}}""");
        }

        ProcessStartInfo install = new()
        {
            FileName = javaPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = metaPath
        };
        install.ArgumentList.Add("-jar");
        install.ArgumentList.Add(installerPath);
        install.ArgumentList.Add("--installClient");
        install.ArgumentList.Add(metaPath);

        using Process? process = Process.Start(install);
        if (process == null)
            throw new InvalidOperationException(
                "NeoForge installer process could not be started.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"NeoForge installer failed with exit code {process.ExitCode}: {stderr}{Environment.NewLine}{stdout}");
        }

        string? versionJson = FindNeoForgeVersionJson(
            metaPath,
            minecraftVersion,
            neoForgeVersion);
        if (versionJson == null)
        {
            throw new FileNotFoundException(
                $"NeoForge {neoForgeVersion} installed without a client version profile.");
        }

        return true;
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"NeoForge installation failed: {ex}");
        ShowToast($"NeoForge install failed: {ex.Message}");
        return false;
    }
}

private static string? FindNeoForgeVersionJson(
    string metaPath,
    string minecraftVersion,
    string neoForgeVersion)
{
    string versionsPath = Path.Combine(metaPath, "versions");
    if (!Directory.Exists(versionsPath))
        return null;

    foreach (string jsonPath in Directory.EnumerateFiles(
                 versionsPath,
                 "*.json",
                 SearchOption.AllDirectories))
    {
        try
        {
            using JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(jsonPath));
            JsonElement root = document.RootElement;
            string? id = root.TryGetProperty("id", out JsonElement idElement)
                ? idElement.GetString()
                : null;
            string? parent = root.TryGetProperty(
                    "inheritsFrom",
                    out JsonElement parentElement)
                ? parentElement.GetString()
                : null;
            if (id?.Contains("neoforge", StringComparison.OrdinalIgnoreCase) == true &&
                id.Contains(neoForgeVersion, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    parent,
                    minecraftVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                return jsonPath;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    return null;
}

private sealed class NeoForgeLaunchConfiguration
{
    public string MainClass { get; init; } = string.Empty;
    public string VersionId { get; init; } = string.Empty;
    public List<string> Libraries { get; init; } = new();
    public string JvmArguments { get; init; } = string.Empty;
    public string GameArguments { get; init; } = string.Empty;
}

private static NeoForgeLaunchConfiguration LoadNeoForgeLaunchConfiguration(
    string metaPath,
    string minecraftVersion,
    string neoForgeVersion)
{
    string jsonPath = FindNeoForgeVersionJson(
        metaPath,
        minecraftVersion,
        neoForgeVersion)
        ?? throw new FileNotFoundException(
            $"NeoForge {neoForgeVersion} is not installed for Minecraft {minecraftVersion}.");

    using JsonDocument document =
        JsonDocument.Parse(File.ReadAllText(jsonPath));
    JsonElement root = document.RootElement;
    string versionId = root.GetProperty("id").GetString()
        ?? throw new InvalidDataException("NeoForge version profile has no id.");
    string mainClass = root.GetProperty("mainClass").GetString()
        ?? throw new InvalidDataException("NeoForge version profile has no main class.");
    string librariesRoot = Path.Combine(metaPath, "libraries");

    List<string> libraries = new();
    if (root.TryGetProperty("libraries", out JsonElement libraryEntries))
    {
        foreach (JsonElement library in libraryEntries.EnumerateArray())
        {
            string? relativePath = null;
            if (library.TryGetProperty("downloads", out JsonElement downloads) &&
                downloads.TryGetProperty("artifact", out JsonElement artifact) &&
                artifact.TryGetProperty("path", out JsonElement pathElement))
            {
                relativePath = pathElement.GetString();
            }

            if (relativePath == null &&
                library.TryGetProperty("name", out JsonElement nameElement))
            {
                relativePath = GetMavenLibraryPath(nameElement.GetString());
            }

            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            string fullPath = Path.Combine(
                librariesRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                libraries.Add(fullPath);
        }
    }

    return new NeoForgeLaunchConfiguration
    {
        MainClass = mainClass,
        VersionId = versionId,
        Libraries = libraries.Distinct(
            StringComparer.OrdinalIgnoreCase).ToList(),
        JvmArguments = ReadNeoForgeArguments(
            root,
            "jvm",
            librariesRoot,
            versionId,
            removeStandardJvmArguments: true),
        GameArguments = ReadNeoForgeArguments(
            root,
            "game",
            librariesRoot,
            versionId,
            removeStandardGameArguments: true)
    };
}

private static string? GetMavenLibraryPath(string? coordinate)
{
    if (string.IsNullOrWhiteSpace(coordinate))
        return null;

    string[] extensionParts = coordinate.Split('@', 2);
    string extension = extensionParts.Length == 2
        ? extensionParts[1]
        : "jar";
    string[] parts = extensionParts[0].Split(':');
    if (parts.Length < 3)
        return null;

    string group = parts[0].Replace('.', '/');
    string artifact = parts[1];
    string version = parts[2];
    string classifier = parts.Length > 3 ? $"-{parts[3]}" : string.Empty;
    return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.{extension}";
}

private static List<string> GetClasspathLibraries(
    string versionJsonPath,
    string librariesRoot)
{
    using JsonDocument document =
        JsonDocument.Parse(File.ReadAllText(versionJsonPath));
    if (!document.RootElement.TryGetProperty(
            "libraries",
            out JsonElement libraries) ||
        libraries.ValueKind != JsonValueKind.Array)
    {
        throw new InvalidDataException(
            $"Minecraft library metadata is missing from {versionJsonPath}.");
    }

    List<string> paths = new();
    foreach (JsonElement library in libraries.EnumerateArray())
    {
        if (!IsMinecraftLibraryAllowed(library))
            continue;
        if (IsWindowsNativeLibrary(library))
            continue;

        string? relativePath = null;
        if (library.TryGetProperty("downloads", out JsonElement downloads) &&
            downloads.TryGetProperty("artifact", out JsonElement artifact) &&
            artifact.TryGetProperty("path", out JsonElement pathElement))
        {
            relativePath = pathElement.GetString();
        }

        if (relativePath == null &&
            library.TryGetProperty("name", out JsonElement nameElement))
        {
            relativePath = GetMavenLibraryPath(nameElement.GetString());
        }

        if (string.IsNullOrWhiteSpace(relativePath) ||
            !Path.GetExtension(relativePath).Equals(
                ".jar",
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        string fullPath = Path.Combine(
            librariesRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"A required Minecraft/loader library is missing: {relativePath}",
                fullPath);
        }

        paths.Add(fullPath);
    }

    return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

private static bool IsWindowsNativeLibrary(JsonElement library)
{
    if (!library.TryGetProperty("name", out JsonElement nameElement))
        return false;

    string? coordinate = nameElement.GetString();
    if (string.IsNullOrWhiteSpace(coordinate))
        return false;

    string[] parts = coordinate.Split(':');
    return parts.Length > 3 &&
           parts[3].StartsWith(
               "natives-windows",
               StringComparison.OrdinalIgnoreCase);
}

private static void ExtractMinecraftNatives(
    string versionJsonPath,
    string librariesRoot,
    string nativesDirectory)
{
    using JsonDocument document =
        JsonDocument.Parse(File.ReadAllText(versionJsonPath));
    if (!document.RootElement.TryGetProperty(
            "libraries",
            out JsonElement libraries) ||
        libraries.ValueKind != JsonValueKind.Array)
    {
        throw new InvalidDataException(
            $"Minecraft library metadata is missing from {versionJsonPath}.");
    }

    string expectedClassifier =
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "natives-windows-arm64"
            : "natives-windows";
    int extractedArchives = 0;
    Directory.CreateDirectory(nativesDirectory);

    foreach (JsonElement library in libraries.EnumerateArray())
    {
        if (!IsMinecraftLibraryAllowed(library) ||
            !library.TryGetProperty("name", out JsonElement nameElement))
        {
            continue;
        }

        string? coordinate = nameElement.GetString();
        string[] parts = coordinate?.Split(':') ?? Array.Empty<string>();
        if (parts.Length <= 3 ||
            !parts[3].Equals(
                expectedClassifier,
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        string? relativePath = null;
        if (library.TryGetProperty("downloads", out JsonElement downloads) &&
            downloads.TryGetProperty("artifact", out JsonElement artifact) &&
            artifact.TryGetProperty("path", out JsonElement pathElement))
        {
            relativePath = pathElement.GetString();
        }

        if (string.IsNullOrWhiteSpace(relativePath))
            relativePath = GetMavenLibraryPath(coordinate);
        if (string.IsNullOrWhiteSpace(relativePath))
            continue;

        string archivePath = Path.Combine(
            librariesRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException(
                $"Required Minecraft native archive is missing: {relativePath}",
                archivePath);
        }

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) ||
                entry.FullName.StartsWith(
                    "META-INF/",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string destination = Path.GetFullPath(
                Path.Combine(nativesDirectory, entry.FullName));
            string nativeRoot = Path.GetFullPath(nativesDirectory) +
                                Path.DirectorySeparatorChar;
            if (!destination.StartsWith(
                    nativeRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Native archive contains an invalid path: {entry.FullName}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        extractedArchives++;
    }

    if (extractedArchives == 0)
    {
        throw new InvalidDataException(
            $"No Windows native libraries were listed in {versionJsonPath}.");
    }
}

private static string BuildMinecraftGameArguments(
    string versionJsonPath,
    string version,
    AccountData account,
    string gameDirectory,
    string assetsDirectory,
    string assetIndex,
    bool offline,
    string? offlineUuid = null)
{
    using JsonDocument document =
        JsonDocument.Parse(File.ReadAllText(versionJsonPath));
    JsonElement root = document.RootElement;
    if (!root.TryGetProperty("arguments", out JsonElement arguments) ||
        !arguments.TryGetProperty("game", out JsonElement gameArguments) ||
        gameArguments.ValueKind != JsonValueKind.Array)
    {
        throw new InvalidDataException(
            $"Minecraft launch arguments are missing from {versionJsonPath}.");
    }

    string versionType = root.TryGetProperty("type", out JsonElement type)
        ? type.GetString() ?? "release"
        : "release";
    Dictionary<string, string> substitutions = new(
        StringComparer.Ordinal)
    {
        ["${auth_player_name}"] = account.username ?? string.Empty,
        ["${version_name}"] = version,
        ["${game_directory}"] = gameDirectory,
        ["${assets_root}"] = assetsDirectory,
        ["${assets_index_name}"] = assetIndex,
        ["${auth_uuid}"] = offline
            ? offlineUuid ?? account.uuid ?? string.Empty
            : account.uuid ?? string.Empty,
        ["${auth_access_token}"] = offline
            ? "0"
            : account.accessToken ?? string.Empty,
        ["${clientid}"] = "e16699bb-2aa8-46da-b5e3-45cbcce29091",
        ["${auth_xuid}"] = offline
            ? string.Empty
            : GetJwtClaim(account.accessToken, "xuid"),
        ["${version_type}"] = versionType
    };

    List<string> resolvedArguments = new();
    foreach (JsonElement argument in gameArguments.EnumerateArray())
    {
        IEnumerable<string> values;
        if (argument.ValueKind == JsonValueKind.String)
        {
            values = new[] { argument.GetString() ?? string.Empty };
        }
        else if (argument.ValueKind == JsonValueKind.Object &&
                 IsMinecraftGameArgumentAllowed(argument) &&
                 argument.TryGetProperty("value", out JsonElement value))
        {
            values = value.ValueKind switch
            {
                JsonValueKind.String =>
                    new[] { value.GetString() ?? string.Empty },
                JsonValueKind.Array =>
                    value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString() ?? string.Empty)
                        .ToArray(),
                _ => Array.Empty<string>()
            };
        }
        else
        {
            continue;
        }

        foreach (string rawValue in values)
        {
            string resolved = rawValue;
            foreach ((string placeholder, string replacement) in substitutions)
                resolved = resolved.Replace(placeholder, replacement);

            if (resolved.Contains("${", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Minecraft argument contains an unresolved placeholder: {resolved}");
            }

            resolvedArguments.Add(resolved);
        }
    }

    return string.Join(" ", resolvedArguments.Select(QuoteProcessArgument));
}

private static bool IsMinecraftGameArgumentAllowed(JsonElement argument)
{
    if (!argument.TryGetProperty("rules", out JsonElement rules) ||
        rules.ValueKind != JsonValueKind.Array)
    {
        return true;
    }

    bool allowed = false;
    foreach (JsonElement rule in rules.EnumerateArray())
    {
        if (rule.TryGetProperty("os", out JsonElement os) &&
            os.TryGetProperty("name", out JsonElement osName) &&
            !string.Equals(
                osName.GetString(),
                "windows",
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (rule.TryGetProperty("features", out JsonElement features) &&
            features.ValueKind == JsonValueKind.Object &&
            features.EnumerateObject().Any(feature =>
                feature.Value.ValueKind != JsonValueKind.False))
        {
            continue;
        }

        string? action = rule.TryGetProperty(
                "action",
                out JsonElement actionElement)
            ? actionElement.GetString()
            : null;
        if (string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase))
            allowed = true;
        else if (string.Equals(action, "disallow", StringComparison.OrdinalIgnoreCase))
            allowed = false;
    }

    return allowed;
}

private static void EnsureSupportedProfileLoader(Profile? profile)
{
    if (profile == null)
        throw new InvalidOperationException("No profile is selected.");

    if (profile.Loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase) ||
        profile.Loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
        profile.Loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase) ||
        profile.Loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    throw new NotSupportedException(
        $"The saved {profile.Loader} profile is not supported. " +
        "Create a new profile using Fabric, Forge, NeoForge, or Vanilla.");
}

private string GetProfileDirectory(Profile profile)
{
    if (profile.IsModrinthProfile)
    {
        if (string.IsNullOrWhiteSpace(profile.ModrinthDirectory))
        {
            throw new InvalidDataException(
                $"The Modrinth profile '{profile.Name}' has no directory path.");
        }

        string modrinthDirectory =
            Path.GetFullPath(profile.ModrinthDirectory);
        if (!Directory.Exists(modrinthDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The Modrinth profile directory no longer exists: {modrinthDirectory}");
        }

        return modrinthDirectory;
    }

    if (profile.IsNoRiskProfile)
    {
        if (string.IsNullOrWhiteSpace(profile.NoRiskDirectory))
        {
            throw new InvalidDataException(
                $"The NoRisk profile '{profile.Name}' has no directory path.");
        }

        string noRiskDirectory = Path.GetFullPath(profile.NoRiskDirectory);
        if (!Directory.Exists(noRiskDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The NoRisk profile directory no longer exists: {noRiskDirectory}");
        }

        return noRiskDirectory;
    }

    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "Profiles",
        profile.Name);
}

private string GetCurrentProfileDirectory()
{
    if (currentProfile == null)
    {
        throw new InvalidOperationException("No profile is selected.");
    }

    return GetProfileDirectory(currentProfile);
}

private static bool IsSameProfile(Profile candidate, Profile selected)
{
    if (candidate.IsModrinthProfile || selected.IsModrinthProfile)
    {
        return candidate.IsModrinthProfile &&
               selected.IsModrinthProfile &&
               string.Equals(
                   candidate.ModrinthInstanceId,
                   selected.ModrinthInstanceId,
                   StringComparison.Ordinal);
    }

    if (candidate.IsNoRiskProfile || selected.IsNoRiskProfile)
    {
        return candidate.IsNoRiskProfile &&
               selected.IsNoRiskProfile &&
               string.Equals(
                   candidate.NoRiskProfileId,
                   selected.NoRiskProfileId,
                   StringComparison.Ordinal);
    }

    return string.Equals(
        candidate.Name,
        selected.Name,
        StringComparison.OrdinalIgnoreCase);
}

private static string GetJwtClaim(string token, string claimName)
{
    string[] parts = token?.Split('.') ?? Array.Empty<string>();
    if (parts.Length < 2)
        return string.Empty;

    try
    {
        string payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(
            payload.Length + (4 - payload.Length % 4) % 4,
            '=');
        using JsonDocument document =
            JsonDocument.Parse(Convert.FromBase64String(payload));
        return document.RootElement.TryGetProperty(
                claimName,
                out JsonElement value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
    catch (FormatException)
    {
        return string.Empty;
    }
    catch (JsonException)
    {
        return string.Empty;
    }
}

private static string QuoteProcessArgument(string argument)
{
    StringBuilder quoted = new("\"");
    int backslashes = 0;
    foreach (char character in argument)
    {
        if (character == '\\')
        {
            backslashes++;
            continue;
        }

        if (character == '"')
        {
            quoted.Append('\\', backslashes * 2 + 1);
            quoted.Append('"');
            backslashes = 0;
            continue;
        }

        quoted.Append('\\', backslashes);
        quoted.Append(character);
        backslashes = 0;
    }

    quoted.Append('\\', backslashes * 2);
    quoted.Append('"');
    return quoted.ToString();
}

private string ConfigureMinecraftProcessLogging(
    Process process,
    RunningInstance instance,
    string gameDirectory)
{
    string logDirectory = Path.Combine(gameDirectory, "logs");
    Directory.CreateDirectory(logDirectory);
    string logPath = Path.Combine(logDirectory, "launcher-latest.log");
    File.WriteAllText(
        logPath,
        $"Profile: {instance.ProfileName}{Environment.NewLine}" +
        $"Minecraft: {instance.Version}{Environment.NewLine}" +
        $"Loader: {instance.Loader}{Environment.NewLine}" +
        $"Started: {DateTimeOffset.Now:O}{Environment.NewLine}{Environment.NewLine}");
    object logLock = new();

    void RecordLine(string source, string line)
    {
        string entry = $"[{DateTimeOffset.Now:O}] [{source}] {line}";
        try
        {
            lock (logLock)
                File.AppendAllText(logPath, entry + Environment.NewLine);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Could not write Minecraft launch log: {ex.Message}");
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            instance.ConsoleLines.Add(entry);
            logWindow?.RefreshInstance(instance);
        }));
    }

    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data != null)
            RecordLine("OUT", eventArgs.Data);
    };
    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data != null)
            RecordLine("ERR", eventArgs.Data);
    };
    process.EnableRaisingEvents = true;
    process.Exited += (_, _) =>
    {
        int exitCode = process.ExitCode;
        RecordLine("EXIT", $"Minecraft process exited with code {exitCode}.");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateDiscordPresence();
            if (exitCode != 0)
            {
                ShowLogsWindow(instance);
                ShowToast(
                    $"Minecraft exited with code {exitCode}. See {logPath}");
            }
        }));
    };

    return logPath;
}

private static bool IsMinecraftLibraryAllowed(JsonElement library)
{
    if (!library.TryGetProperty("rules", out JsonElement rules) ||
        rules.ValueKind != JsonValueKind.Array)
    {
        return true;
    }

    bool allowed = false;
    foreach (JsonElement rule in rules.EnumerateArray())
    {
        string? action = rule.TryGetProperty(
                "action",
                out JsonElement actionElement)
            ? actionElement.GetString()
            : null;
        if (!string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(action, "disallow", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (rule.TryGetProperty("os", out JsonElement os) &&
            os.TryGetProperty("name", out JsonElement osName) &&
            !string.Equals(
                osName.GetString(),
                "windows",
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        allowed = string.Equals(
            action,
            "allow",
            StringComparison.OrdinalIgnoreCase);
    }

    return allowed;
}

private static string ReadNeoForgeArguments(
    JsonElement root,
    string argumentType,
    string librariesRoot,
    string versionId,
    bool removeStandardGameArguments = false,
    bool removeStandardJvmArguments = false)
{
    if (!root.TryGetProperty("arguments", out JsonElement arguments) ||
        !arguments.TryGetProperty(argumentType, out JsonElement values) ||
        values.ValueKind != JsonValueKind.Array)
    {
        return string.Empty;
    }

    List<string> result = new();
    List<string> rawValues = new();
    foreach (JsonElement value in values.EnumerateArray())
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            rawValues.Add(value.GetString() ?? string.Empty);
        }
        else if (value.ValueKind == JsonValueKind.Object &&
                 IsNeoForgeArgumentAllowed(value) &&
                 value.TryGetProperty("value", out JsonElement conditionalValue))
        {
            if (conditionalValue.ValueKind == JsonValueKind.String)
            {
                rawValues.Add(conditionalValue.GetString() ?? string.Empty);
            }
            else if (conditionalValue.ValueKind == JsonValueKind.Array)
            {
                rawValues.AddRange(conditionalValue.EnumerateArray()
                    .Where(entry => entry.ValueKind == JsonValueKind.String)
                    .Select(entry => entry.GetString() ?? string.Empty));
            }
        }
    }

    HashSet<string> standardGameArguments = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "--username", "--uuid", "--accessToken", "--version", "--gameDir",
        "--assetsDir", "--assetIndex", "--userType", "--clientId", "--xuid",
        "--userProperties", "--versionType", "--authSession", "--session"
    };
    HashSet<string> standardJvmArguments = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "-cp", "-classpath", "--class-path", "-Djava.library.path"
    };
    bool skipNext = false;
    foreach (string rawValue in rawValues)
    {
        if (skipNext)
        {
            skipNext = false;
            continue;
        }

        string value = rawValue
            .Replace("${library_directory}", librariesRoot)
            .Replace("${classpath_separator}", Path.PathSeparator.ToString())
            .Replace("${version_name}", versionId)
            .Replace("${version_type}", "release");

        if (removeStandardJvmArguments &&
            (standardJvmArguments.Contains(value) ||
             value.StartsWith(
                 "-Djava.library.path=",
                 StringComparison.OrdinalIgnoreCase)))
        {
            skipNext = !value.Contains('=');
            continue;
        }

        if (removeStandardGameArguments)
        {
            string option = value.Split('=', 2)[0];
            if (standardGameArguments.Contains(option))
            {
                skipNext = !value.Contains('=');
                continue;
            }
        }

        result.Add(value.Contains(' ') ? $"\"{value}\"" : value);
    }

    return string.Join(" ", result);
}

private static bool IsNeoForgeArgumentAllowed(JsonElement argument)
{
    if (!argument.TryGetProperty("rules", out JsonElement rules) ||
        rules.ValueKind != JsonValueKind.Array)
    {
        return true;
    }

    bool allowed = false;
    foreach (JsonElement rule in rules.EnumerateArray())
    {
        string? action = rule.TryGetProperty(
                "action",
                out JsonElement actionElement)
            ? actionElement.GetString()
            : null;
        if (!string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(action, "disallow", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (rule.TryGetProperty("os", out JsonElement os) &&
            os.TryGetProperty("name", out JsonElement osName) &&
            !string.Equals(
                osName.GetString(),
                "windows",
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        allowed = string.Equals(
            action,
            "allow",
            StringComparison.OrdinalIgnoreCase);
    }

    return allowed;
}

private async Task<bool> DownloadForgeLibraries(
    string mcVersion,
    string forgeVersion,
    bool forceDownload = false)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return false;
        }

        using HttpClient client = new HttpClient();

        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "libraries",
            mcVersion);

        Directory.CreateDirectory(basePath);

        string universalUrl =
            $"https://maven.minecraftforge.net/net/minecraftforge/forge/{forgeVersion}/forge-{forgeVersion}-universal.jar";

        string forgePath = Path.Combine(
            basePath,
            $"forge-{forgeVersion}-universal.jar");

        if (forceDownload || !File.Exists(forgePath))
        {
            ThrowIfCancelled();
            byte[] data = await client.GetByteArrayAsync(universalUrl);
            ThrowIfCancelled();
            if (data.Length == 0)
                throw new InvalidDataException(
                    $"Forge library download was empty: {forgeVersion}.");
            await File.WriteAllBytesAsync(forgePath, data);
        }
        return File.Exists(forgePath) &&
               new FileInfo(forgePath).Length > 0;
    }
    catch (Exception ex)
    {
        ShowToast("Forge libraries failed!");
        Console.WriteLine(ex);
        return false;
    }
}
private async Task<bool> DownloadLibraries(
    string version,
    bool forceDownload = false)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return false;
        }

        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "libraries",
            version
        );


        string minecraftVersionsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "versions",
            version);
        string minecraftVersionJsonPath = Path.Combine(
            minecraftVersionsPath,
            $"{version}.json");

        if (!forceDownload &&
            File.Exists(minecraftVersionJsonPath) &&
            Directory.Exists(basePath) &&
            Directory.GetFiles(basePath, "*", SearchOption.AllDirectories).Length > 50)
        {
            return true;
        }

        using HttpClient client = new HttpClient();


        string manifestJson = await client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
        using JsonDocument manifestDoc = JsonDocument.Parse(manifestJson);

        string versionUrl = null;

        foreach (var v in manifestDoc.RootElement.GetProperty("versions").EnumerateArray())
        {
            if (v.GetProperty("id").GetString() == version)
            {
                versionUrl = v.GetProperty("url").GetString();
                break;
            }
        }

        if (versionUrl == null)
        {
            ShowToast("Version not found!");
            return false;
        }


        string versionJson = await client.GetStringAsync(versionUrl);
        using JsonDocument versionDoc = JsonDocument.Parse(versionJson);
        Directory.CreateDirectory(minecraftVersionsPath);
        await File.WriteAllTextAsync(minecraftVersionJsonPath, versionJson);

        var libraries = versionDoc.RootElement.GetProperty("libraries");

        foreach (var lib in libraries.EnumerateArray())
        {
            try
            {
                if (!lib.TryGetProperty("downloads", out var downloads))
                    continue;

                if (!downloads.TryGetProperty("artifact", out var artifact))
                    continue;

                string url = artifact.GetProperty("url").GetString();
                string pathInJar = artifact.GetProperty("path").GetString();

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(pathInJar))
                    continue;

                string fullPath = System.IO.Path.Combine(basePath, pathInJar);

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));

                if (!forceDownload && File.Exists(fullPath))
                    continue;
                ThrowIfCancelled();
                byte[] data = await client.GetByteArrayAsync(url);
                ThrowIfCancelled();
                if (data.Length == 0)
                    throw new InvalidDataException(
                        $"Minecraft library download was empty: {pathInJar}.");
                File.WriteAllBytes(fullPath, data);
            }
            catch
            {
                if (forceDownload)
                    throw;
            }
        }
        return true;
    }
    catch (Exception ex)
    {
        ShowToast(ex.ToString());
        return false;
    }
}

    private async Task DownloadFabricProfile(string mcVersion, string loaderVersion)
    {
        try
        {
            if (cancelLaunch)
            {
                ResetLaunchUI();
                return;
            }

            using HttpClient client = new HttpClient();


            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "meta",
                "versions",
                mcVersion
            );

            Directory.CreateDirectory(path);




            string jsonUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";

            string json = await client.GetStringAsync(jsonUrl);

            string jsonFile = System.IO.Path.Combine(
                path,
                $"fabric-loader-{loaderVersion}-{mcVersion}.json"
            );

            File.WriteAllText(jsonFile, json);




            string jarUrl = $"https://maven.fabricmc.net/net/fabricmc/fabric-loader/{loaderVersion}/fabric-loader-{loaderVersion}.jar";
ThrowIfCancelled();
            byte[] jarData = await client.GetByteArrayAsync(jarUrl);
ThrowIfCancelled();
            string jarFile = System.IO.Path.Combine(
                path,
                $"fabric-loader-{loaderVersion}.jar"
            );

            File.WriteAllBytes(jarFile, jarData);

            
        }
        catch (Exception ex)
        {
            ShowToast("Fabric install failed!");
            
        }
    }    
    private async void Minimize_Click(object sender, RoutedEventArgs e)
    {
        var fade = new DoubleAnimation(1, 0.2,
            TimeSpan.FromMilliseconds(150));

        BeginAnimation(OpacityProperty, fade);

        await Task.Delay(150);

        WindowState = WindowState.Minimized;


        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
    }    private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Prevent duplicate launch clicks.
        if (e.ClickCount > 1)
            return;


        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            return;
        }

        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }
    
    private void UpdateProfilesUI()
    {
        if (ProfilesList.Children.Count == 0)
        {
            ProfilesScroll.Visibility = Visibility.Collapsed;
            NoProfilesCard.Visibility = Visibility.Visible;
        }
        else
        {
            ProfilesScroll.Visibility = Visibility.Visible;
            NoProfilesCard.Visibility = Visibility.Collapsed;
        }
    }
  
    private void CloseCreateProfile_Click(object sender, RoutedEventArgs e)
    {
        CreateProfilePanel.Visibility = Visibility.Collapsed;
        RightColumn.Width = new GridLength(0);
    }
    
    private void OpenCreateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (isLaunching)
        {
            
            return;
        }
        if (IsOfflineMode)
            
        {
            
            ShowToast("You must be online to create profiles.");
            
            return;
            
        }
        CreateProfilePanel.Visibility = Visibility.Visible;
        RightColumn.Width = new GridLength(300);
    }
    
    private void Search_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SearchBox.Text == "Search...")
        {
            SearchBox.Text = "";
            SearchBox.Foreground = Brushes.White;
        }
    }

    private void Search_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            SearchBox.Text =
                LocalizationManager.Instance.Translate("Search...");
            SearchBox.Foreground = Brushes.Gray;
            LoadProfiles();
        }
    }
  
    private async Task LoadContent()
    {
        if (currentProfile == null)
            return;

        ContentList.Children.Clear();
        var loadedNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
       
        
        string loader = currentProfile.Loader.ToLower();
        string version = currentProfile.Version;
        Console.WriteLine("=== MODRINTH SEARCH DEBUG ===");
        Console.WriteLine("Loader: " + loader);
        Console.WriteLine("Version: " + version);
        string query = string.IsNullOrWhiteSpace(currentSearch)
            ? ""
            : currentSearch;


        bool showInstalled = ShowInstalledCheck.IsChecked == true;

        string facets;

        // Filter items by tab.
        if (currentTab == "mod")
        {
            facets = $"[[\"project_type:mod\"],[\"categories:{loader}\"],[\"versions:{version}\"]]";
        }
        else if (currentTab == "resourcepack")
        {
            facets = $"[[\"project_type:resourcepack\"],[\"versions:{version}\"]]";
        }
        else
        {
            facets = $"[[\"project_type:shader\"]]";
        }

        string url = $"https://api.modrinth.com/v2/search?limit=10&query={query}&facets={facets}";
        Console.WriteLine(url);
        using (HttpClient client = new HttpClient())
        {
            string json = await client.GetStringAsync(url);

            var result = JsonSerializer.Deserialize<ModrinthResponse>(json);

            if (result?.hits == null)
                return;

            foreach (var mod in result.hits)
            {
                if (!loadedNames.Add(mod.title.Trim()))
                    continue;

                bool isInstalled = CheckIfInstalled(mod);

                if (!showInstalled && isInstalled)
                    continue;

                AddModToUI(mod, isInstalled);
            }
            var curseForgeMods =
                await LoadCurseForgeMods(
                    query,
                    loader,
                    version);

            foreach (var mod in curseForgeMods)
            {
                if (!loadedNames.Add(mod.name.Trim()))
                    continue;

                bool isInstalled =
                    CheckIfInstalled(mod);

                if (!showInstalled && isInstalled)
                    continue;

                AddCurseForgeModToUI(
                    mod,
                    isInstalled);
            }
        }
        
        
    }    
    private void AddCurseForgeModToUI(
        CurseForgeMod mod,
        bool isInstalled)
    {
        Border card = new Border
        {
            Background =
                (Brush)new BrushConverter()
                    .ConvertFromString("#1E293B"),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0,0,0,10),
            Padding = new Thickness(12)
        };
        card.Cursor = Cursors.Hand;
        card.MouseLeftButtonUp += async (s, e) =>
        {
            await OpenCurseForgeDetails(mod);

            openedFromInstalledContent = false;
        };
        Grid grid = new Grid();

        grid.RowDefinitions.Add(
            new RowDefinition());

        grid.RowDefinitions.Add(
            new RowDefinition());

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(60)
            });

        grid.ColumnDefinitions.Add(
            new ColumnDefinition());

        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(110)
            });

        Image icon = new Image
        {
            Width = 48,
            Height = 48,
            Margin = new Thickness(0,0,12,0)
        };

        ApplyRoundedImageCorners(icon);

        try
        {
            if (mod.logo?.url != null)
            {
                icon.Source =
                    new BitmapImage(
                        new Uri(mod.logo.url));
            }
        }
        catch { }

        Grid.SetRowSpan(icon, 2);
        Grid.SetColumn(icon, 0);

        grid.Children.Add(icon);

        TextBlock title = new TextBlock
        {
            Text = mod.name,
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 16
        };

        Grid.SetColumn(title, 1);

        grid.Children.Add(title);

        TextBlock desc = new TextBlock
        {
            Text = mod.summary,
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,6,0,0)
        };
        Button installBtn = new Button
        {
            Width = 100,
            Height = 32,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold
        };
        var borderFactory = new FrameworkElementFactory(typeof(Border));

        borderFactory.Name = "border";

        borderFactory.SetBinding(
            Border.BackgroundProperty,
            new Binding("Background")
            {
                Source = installBtn
            });

        borderFactory.SetValue(
            Border.CornerRadiusProperty,
            new CornerRadius(10));

        var contentFactory =
            new FrameworkElementFactory(
                typeof(ContentPresenter));

        contentFactory.SetValue(
            ContentPresenter.HorizontalAlignmentProperty,
            HorizontalAlignment.Center);

        contentFactory.SetValue(
            ContentPresenter.VerticalAlignmentProperty,
            VerticalAlignment.Center);

        borderFactory.AppendChild(contentFactory);

        installBtn.Template =
            new ControlTemplate(typeof(Button))
            {
                VisualTree = borderFactory
            };
        if (isInstalled)
        {
            installBtn.Content =
                LocalizationManager.Instance.Translate("Versions");
            installBtn.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#334155"));
            installBtn.Foreground = Brushes.White;
            installBtn.IsEnabled = true;
            installBtn.Click += async (s, e) =>
            {
                installBtn.IsEnabled = false;
                installBtn.Content =
                    LocalizationManager.Instance.Translate("Loading...");
                await OpenContentVersionPicker(
                    "CurseForge",
                    mod.id.ToString(),
                    mod.name,
                    mod.summary,
                    mod.logo?.url,
                    FindExistingProfileContentItem(
                        "CurseForge",
                        mod.id.ToString(),
                        mod.name));
                installBtn.Content =
                    LocalizationManager.Instance.Translate("Versions");
                installBtn.IsEnabled = true;
            };
        }
        else
        {
            installBtn.Content =
                LocalizationManager.Instance.Translate("Install");

            installBtn.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#F97316"));

            installBtn.Foreground =
                Brushes.White;

            installBtn.MouseEnter += (s, e) =>
                installBtn.Background =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#EA580C"));

            installBtn.MouseLeave += (s, e) =>
                installBtn.Background =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#F97316"));

            installBtn.Click += async (s, e) =>
            {
                installBtn.IsEnabled = false;
                installBtn.Content =
                    LocalizationManager.Instance.Translate("Loading...");
                await OpenContentVersionPicker(
                    "CurseForge",
                    mod.id.ToString(),
                    mod.name,
                    mod.summary,
                    mod.logo?.url);
                installBtn.Content =
                    LocalizationManager.Instance.Translate("Install");
                installBtn.IsEnabled = true;
            };
        }

       
        
        Grid.SetColumn(installBtn, 2);

        grid.Children.Add(installBtn);
        TextBlock sourceText = new TextBlock
        {
            Text = "CURSEFORGE",
            Foreground = Brushes.Orange,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -2, 0, 0)
        };

        Grid.SetRow(sourceText, 1);
        Grid.SetColumn(sourceText, 2);

        grid.Children.Add(sourceText);
        Grid.SetRow(desc, 1);
        Grid.SetColumn(desc, 1);

        grid.Children.Add(desc);

        card.Child = grid;

        ContentList.Children.Add(card);
    }
    private bool CheckIfInstalled(ModrinthProject mod)
    {
        return IsContentInstalledByName(mod?.title);
    }
    private void BackToProfile_Click(object sender, RoutedEventArgs e)
    {
        ContentBrowserPage.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Visible;


        currentTab = "mod";


        ModsTab.Foreground = Brushes.White;
        RPacksTab.Foreground = Brushes.Gray;
        ShadersTab.Foreground = Brushes.Gray;

        ModsLine.Visibility = Visibility.Visible;
        RPacksLine.Visibility = Visibility.Collapsed;
        ShadersLine.Visibility = Visibility.Collapsed;



        ReloadProfileData();

        InstalledContentList.Items.Clear();
        LoadInstalledContent();
    }
    
    public class CurseForgeDescriptionResponse
    {
        public string data { get; set; }
    }
    private async Task OpenInstalledContentDetails(string modName)
    {
        List<string> searchNames =
            GetImportedModSearchNames(modName);

        if (searchNames.Count == 0)
        {
            ShowToast("No description available for this custom content");
            return;
        }

        try
        {
            using HttpClient client = new();
            ModrinthProject mod =
                await FindImportedModMetadataAsync(
                    client,
                    modName,
                    null);

            if (mod != null)
            {
                openedFromInstalledContent = true;
                ProfileDetailPage.Visibility = Visibility.Collapsed;
                await OpenModDetails(mod);
                return;
            }

            if (currentTab == "mod")
            {
                foreach (string searchName in searchNames)
                {
                    List<CurseForgeMod> curseForgeMods =
                        await LoadCurseForgeMods(
                            searchName,
                            currentProfile?.Loader ?? "fabric",
                            currentProfile?.Version ?? "1.21.1");

                    string normalizedSearchName =
                        NormalizeCompareName(searchName);

                    CurseForgeMod curseForgeMatch =
                        curseForgeMods.FirstOrDefault(candidate =>
                            NormalizeCompareName(candidate.name) ==
                            normalizedSearchName);

                    if (curseForgeMatch != null)
                    {
                        openedFromInstalledContent = true;
                        ProfileDetailPage.Visibility = Visibility.Collapsed;
                        await OpenCurseForgeDetails(curseForgeMatch);
                        return;
                    }
                }
            }

            ShowToast(
                $"No description found for {modName}");
        }
        catch (HttpRequestException ex)
        {
            ReportInstalledContentDetailsError(
                ex,
                "Could not load content description");
        }
        catch (JsonException ex)
        {
            ReportInstalledContentDetailsError(
                ex,
                "Could not read content description");
        }
        catch (OperationCanceledException ex)
        {
            ReportInstalledContentDetailsError(
                ex,
                "Loading the content description timed out");
        }
    }

    private void ReportInstalledContentDetailsError(
        Exception exception,
        string message)
    {
        Console.WriteLine(exception);
        openedFromInstalledContent = false;
        ContentDetailPage.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Visible;
        ShowToast(message);
    }

private async Task OpenModDetails(ModrinthProject mod)
{
    DetailTitle.Text = "";
    
    DetailDescription.Text = "";
    
    DetailIcon.Source = null;
        
    DescriptionImagesPanel.Children.Clear();
    
    GalleryPanel.Children.Clear();
    ContentBrowserPage.Visibility = Visibility.Collapsed;
    ContentDetailPage.Visibility = Visibility.Visible;

    DetailTitle.Text = mod.title;

    DescriptionImagesPanel.Children.Clear();

    using HttpClient client = new();

    string json =
        await client.GetStringAsync(
            $"https://api.modrinth.com/v2/project/{mod.project_id}");

    JsonDocument doc =
        JsonDocument.Parse(json);

    string body =
        doc.RootElement
            .GetProperty("body")
            .GetString() ?? "";
    bool isHtml =
        body.Contains("<p>")
        || body.Contains("<img")
        || body.Contains("<iframe");
    string resourceFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            ".JAXSTORE",
            "JaxClient",
            "resource");

    Directory.CreateDirectory(resourceFolder);


    var imgTags = Regex.Matches(
        body,
        "<img[^>]*src=\"([^\"]+)\"",
        RegexOptions.IgnoreCase);

    foreach (Match match in imgTags)
    {
        try
        {
            string imageUrl =
                match.Groups[1].Value;

            string localFile =
                Path.Combine(
                    resourceFolder,
                    Path.GetFileName(
                        new Uri(imageUrl).LocalPath));

            if (!File.Exists(localFile))
            {
                byte[] imageData =
                    await client.GetByteArrayAsync(imageUrl);

                await File.WriteAllBytesAsync(
                    localFile,
                    imageData);
            }

            Image img = new Image
            {
                Source = new BitmapImage(
                    new Uri(localFile)),

                MaxWidth = 850,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0,10,0,10),
                Cursor = Cursors.Hand
            };

            ApplyRoundedImageCorners(img, 12);

            img.MouseLeftButtonUp += (s, e) =>
            {
                FullImage.Source = img.Source;
                ImageModal.Visibility = Visibility.Visible;
            };

            DescriptionImagesPanel.Children.Add(img);
        }
        catch
        {
        }
    }

    // Remove all HTML tags.
    body = Regex.Replace(
        body,
        @"<[^>]+>",
        "",
        RegexOptions.IgnoreCase);
    // Remove image tags.
    body = Regex.Replace(
        body,
        "<img[^>]*>",
        "",
        RegexOptions.IgnoreCase);


    body = Regex.Replace(
        body,
        @"\[(.*?)\]\((.*?)\)",
        "$1");

    body = body.Replace("### ", "");
    body = body.Replace("## ", "");
    body = body.Replace("# ", "");
    body = body.Replace(">", "");
// Remove images.
    body = Regex.Replace(
        body,
        "<img[^>]*>",
        "",
        RegexOptions.IgnoreCase);

// Replace link URLs with their display text.
    body = Regex.Replace(
        body,
        @"\[(.*?)\]\((.*?)\)",
        "$1");

// Remove HTML tags.
    body = Regex.Replace(
        body,
        @"</?p[^>]*>",
        "",
        RegexOptions.IgnoreCase);

    body = Regex.Replace(
        body,
        @"</?ul[^>]*>",
        "",
        RegexOptions.IgnoreCase);

    body = Regex.Replace(
        body,
        @"</?ol[^>]*>",
        "",
        RegexOptions.IgnoreCase);

    body = Regex.Replace(
        body,
        @"</?li[^>]*>",
        "",
        RegexOptions.IgnoreCase);

    body = Regex.Replace(
        body,
        @"</?a[^>]*>",
        "",
        RegexOptions.IgnoreCase);


    body = body.Replace("### ", "");
    body = body.Replace("## ", "");
    body = body.Replace("# ", "");

// Remove bold and italic markers.
    body = body.Replace("***", "");
    body = body.Replace("**", "");
    body = body.Replace("*", "");

// Remove blockquote markers.
    body = body.Replace("> ", "");

// Decode HTML entities.
    body = System.Net.WebUtility.HtmlDecode(body);


    body = body.Replace("###", "");
    body = body.Replace("##", "");
    body = body.Replace("#", "");


    body = Regex.Replace(
        body,
        @"\[(.*?)\]\((.*?)\)",
        "$1");

// Remove all square brackets.
    body = body.Replace("[", "");
    body = body.Replace("]", "");

// Remove bold and italic markers.
    body = body.Replace("***", "");
    body = body.Replace("**", "");
    body = body.Replace("*", "");

// Remove blockquote markers.
   

// Collapse repeated spaces.
   

// Collapse repeated blank lines.
    body = Regex.Replace(
        body,
        @"(\r?\n){3,}",
        "\n\n");
// Remove excess blank lines.
    body = Regex.Replace(
        body,
        @"(\r?\n){3,}",
        "\n\n");

    DetailDescription.Text = body.Trim();
    body = Regex.Replace(
        body,
        @"<iframe.*?</iframe>",
        "",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    body = Regex.Replace(
        body,
        @"<img[^>]*>",
        "",
        RegexOptions.IgnoreCase);

    body = Regex.Replace(
        body,
        @"<[^>]+>",
        "",
        RegexOptions.IgnoreCase);

    body = System.Net.WebUtility.HtmlDecode(body);

    body = Regex.Replace(
        body,
        @"(\r?\n){3,}",
        "\n\n");
    DetailDescription.Text = body;

    try
    {
        DetailIcon.Source =
            new BitmapImage(
                new Uri(mod.icon_url));
    }
    catch
    {
    }

    await LoadGallery(mod.project_id);
}

private async Task OpenCurseForgeDetails(
    CurseForgeMod mod)
{
    DetailTitle.Text = "";
    DetailDescription.Text = "";
    DetailIcon.Source = null;

    GalleryPanel.Children.Clear();
    DescriptionImagesPanel.Children.Clear();

    ContentBrowserPage.Visibility =
        Visibility.Collapsed;

    ContentDetailPage.Visibility =
        Visibility.Visible;

    DetailTitle.Text = mod.name;
    DetailDescription.Text =
        LocalizationManager.Instance.Translate("Loading description...");

    try
    {
        if (!string.IsNullOrEmpty(mod.logo?.url))
        {
            DetailIcon.Source =
                new BitmapImage(
                    new Uri(mod.logo.url));
        }
    }
    catch
    {
    }

    using HttpClient client = new HttpClient();

    client.DefaultRequestHeaders.Add(
        "x-api-key",
        CurseForgeApiKey);

    try
    {
        string descJson =
            await client.GetStringAsync(
                $"https://api.curseforge.com/v1/mods/{mod.id}/description");

        var desc =
            JsonSerializer.Deserialize<
                CurseForgeDescriptionResponse>(
                descJson);

        if (!string.IsNullOrWhiteSpace(desc?.data))
        {
            string text = desc.data;

            text = Regex.Replace(
                text,
                "<img[^>]*>",
                "",
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                "<[^>]+>",
                "");

            text =
                System.Net.WebUtility.HtmlDecode(text);

            text = Regex.Replace(
                text,
                @"(\r?\n){3,}",
                "\n\n");

            DetailDescription.Text =
                text.Trim();
        }
        else
        {
            DetailDescription.Text =
                mod.summary;
        }
    }
    catch
    {
        DetailDescription.Text =
            mod.summary;
    }
    string json =
        await client.GetStringAsync(
            $"https://api.curseforge.com/v1/mods/{mod.id}");

    var details =
        JsonSerializer.Deserialize<
            CurseForgeModDetailsResponse>(
                json);

    if (details?.data?.screenshots == null)
        return;

    foreach (var shot in details.data.screenshots)
    {
        try
        {
            Image image = new Image
            {
                Width = 250,
                Height = 140,
                Margin = new Thickness(5),
                Stretch = Stretch.UniformToFill,
                Cursor = Cursors.Hand,
                Source =
                    new BitmapImage(
                        new Uri(shot.url))
            };

            ApplyRoundedImageCorners(image, 12);

            image.MouseLeftButtonUp += (s, e) =>
            {
                FullImage.Source =
                    image.Source;

                ImageModal.Visibility =
                    Visibility.Visible;
            };

            GalleryPanel.Children.Add(image);
        }
        catch
        {
        }
    }
}
private async Task LoadGallery(string projectId)
    {
        GalleryPanel.Children.Clear();

        using HttpClient client = new();

        string json =
            await client.GetStringAsync(
                $"https://api.modrinth.com/v2/project/{projectId}");

        JsonDocument doc =
            JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("gallery", out var gallery))
        {
            foreach (var img in gallery.EnumerateArray())
            {
                string url =
                    img.GetProperty("url").GetString();

                Image image = new Image
                {
                    Width = 250,
                    Height = 140,
                    Margin = new Thickness(5),
                    Stretch = Stretch.UniformToFill,
                    Cursor = Cursors.Hand
                };

                image.Source =
                    new BitmapImage(new Uri(url));

                ApplyRoundedImageCorners(image, 12);

                image.MouseLeftButtonUp += (s, e) =>
                {
                    FullImage.Source = image.Source;
                    ImageModal.Visibility = Visibility.Visible;
                };

                image.MouseEnter += (s, e) =>
                {
                    image.Opacity = 0.85;
                };

                image.MouseLeave += (s, e) =>
                {
                    image.Opacity = 1;
                };

                GalleryPanel.Children.Add(image);
            }
        }
    }

    private void ApplyRoundedImageCorners(
        Image image,
        double cornerRadius = 8)
    {
        RectangleGeometry clip =
            new()
            {
                RadiusX = cornerRadius,
                RadiusY = cornerRadius
            };

        void UpdateClip(object sender, SizeChangedEventArgs e)
        {
            clip.Rect = new Rect(
                0,
                0,
                image.ActualWidth,
                image.ActualHeight);
        }

        image.Clip = clip;
        image.SizeChanged += UpdateClip;
        clip.Rect = new Rect(
            0,
            0,
            image.ActualWidth,
            image.ActualHeight);
    }

    private void ContentBackBtn_Click(
        object sender,
        RoutedEventArgs e)
    {

        DetailTitle.Text = "";
        DetailDescription.Text = "";
        DetailIcon.Source = null;

        DescriptionImagesPanel.Children.Clear();
        GalleryPanel.Children.Clear();

        FullImage.Source = null;
        ImageModal.Visibility = Visibility.Collapsed;

        ContentDetailPage.Visibility = Visibility.Collapsed;

        if (openedFromModpacks)
        {
            ModpacksPage.Visibility =
                Visibility.Visible;

            openedFromModpacks = false;
        }
        else if (openedFromInstalledContent)
        {
            ProfileDetailPage.Visibility =
                Visibility.Visible;
        }
        else
        {
            ContentBrowserPage.Visibility =
                Visibility.Visible;
        }
        openedFromInstalledContent = false;
    }

    // Close the modal when its close button is clicked.
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ImageModal.Visibility = Visibility.Collapsed;
    }

// Close the modal when the dimmed backdrop is clicked.
    private void ImageModal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ImageModal.Visibility = Visibility.Collapsed;
    }


    private void PreventModalClose_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }


   
    private void DetailTab_Click(object sender, RoutedEventArgs e)
    {
        
        if (sender == DescTab)
        {
            DescriptionView.Visibility = Visibility.Visible;
            GalleryView.Visibility = Visibility.Collapsed;

            DescTab.Foreground = Brushes.White;
            GalleryTab.Foreground =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#64748B"));

            DescLine.Visibility = Visibility.Visible;
            GalleryLine.Visibility = Visibility.Collapsed;
        }
        else
        {
            DescriptionView.Visibility = Visibility.Collapsed;
            GalleryView.Visibility = Visibility.Visible;

            GalleryTab.Foreground = Brushes.White;
            DescTab.Foreground =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#64748B"));

            GalleryLine.Visibility = Visibility.Visible;
            DescLine.Visibility = Visibility.Collapsed;
        }
    }
private void AddModToUI(ModrinthProject mod, bool isInstalled)
{
    Border card = new Border
    {
        Background = (Brush)new BrushConverter().ConvertFromString("#1E293B"),
        CornerRadius = new CornerRadius(8),
        Margin = new Thickness(0, 0, 0, 10),
        Padding = new Thickness(12)
    };
    card.Cursor = Cursors.Hand;

    card.MouseLeftButtonUp += async (s, e) =>
    {
        await OpenModDetails(mod);
        openedFromInstalledContent = false;
    };
    Grid grid = new Grid();
    
    Image icon = new Image
    {
        Width = 48,
        Height = 48,
        Stretch = Stretch.UniformToFill,
        Margin = new Thickness(0, 0, 12, 0)
    };

    ApplyRoundedImageCorners(icon);

    try
    {
        if (!string.IsNullOrEmpty(mod.icon_url))
        {
            icon.Source = new BitmapImage(new Uri(mod.icon_url));
        }
    }
    catch { }

    Grid.SetRowSpan(icon, 2);
    Grid.SetColumn(icon, 0);
    grid.Children.Add(icon);


    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });


    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
    grid.ColumnDefinitions.Add(new ColumnDefinition());
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });


    TextBlock title = new TextBlock
    {
        Text = mod.title,
        Foreground = Brushes.White,
        FontWeight = FontWeights.Bold,
        FontSize = 16
    };

    Grid.SetRow(title, 0);
    Grid.SetColumn(title, 1);
    grid.Children.Add(title);


    Button installBtn = new Button
    {
        Width = 100,
        Height = 32,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        FontWeight = FontWeights.SemiBold
    };


    if (isInstalled)
    {
        installBtn.Content =
            LocalizationManager.Instance.Translate("Versions");
        installBtn.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString("#334155"));
        installBtn.Foreground = Brushes.White;
        installBtn.IsEnabled = true;
        installBtn.Click += async (s, e) =>
        {
            installBtn.IsEnabled = false;
            installBtn.Content =
                LocalizationManager.Instance.Translate("Loading...");
            await OpenContentVersionPicker(
                "Modrinth",
                mod.project_id,
                mod.title,
                mod.description,
                mod.icon_url,
                FindExistingProfileContentItem(
                    "Modrinth",
                    mod.project_id,
                    mod.title));
            installBtn.Content =
                LocalizationManager.Instance.Translate("Versions");
            installBtn.IsEnabled = true;
        };
    }
    else
    {
        installBtn.Content =
            LocalizationManager.Instance.Translate("Install");

        installBtn.Background =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#22C55E"));

        installBtn.Foreground = Brushes.White;


        installBtn.MouseEnter += (s, e) =>
            installBtn.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#16A34A"));

        installBtn.MouseLeave += (s, e) =>
            installBtn.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#22C55E"));


        installBtn.Click += async (s, e) =>
        {
            installBtn.IsEnabled = false;
            installBtn.Content =
                LocalizationManager.Instance.Translate("Loading...");
            await OpenContentVersionPicker(
                "Modrinth",
                mod.project_id,
                mod.title,
                mod.description,
                mod.icon_url);
            installBtn.Content =
                LocalizationManager.Instance.Translate("Install");
            installBtn.IsEnabled = true;
        };
    }


    var borderFactory = new FrameworkElementFactory(typeof(Border));
    borderFactory.Name = "border";
    borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { Source = installBtn });
    borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));

    var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
    contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
    contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

    borderFactory.AppendChild(contentFactory);

    installBtn.Template = new ControlTemplate(typeof(Button))
    {
        VisualTree = borderFactory
    };

    Grid.SetRow(installBtn, 0);
    Grid.SetColumn(installBtn, 2);
    grid.Children.Add(installBtn);
    TextBlock sourceText = new TextBlock
    {
        Text = "MODRINTH",
        Foreground = Brushes.LimeGreen,
        FontSize = 10,
        FontWeight = FontWeights.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, -2, 0, 0)
    };

    Grid.SetRow(sourceText, 1);
    Grid.SetColumn(sourceText, 2);

    grid.Children.Add(sourceText);

    TextBlock desc = new TextBlock
    {
        Text = mod.description,
        Foreground = Brushes.Gray,
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 0)
    };

    Grid.SetRow(desc, 1);
    Grid.SetColumn(desc, 1);
    Grid.SetColumnSpan(desc, 2);

    grid.Children.Add(desc);

    card.Child = grid;
    ContentList.Children.Add(card);
}
private async Task<bool> InstallMod(
    string projectId,
    string modName,
    string description,
    string selectedVersionId = null,
    ContentItem existingItem = null,
    string iconUrl = null,
    string contentType = null,
    ContentVersionOption selectedOption = null,
    bool stableReleaseOnly = false)
{
        if (currentProfile == null ||
            string.IsNullOrWhiteSpace(projectId))
            return false;

    string selectedContentType = contentType ?? currentTab;
    string loader = currentProfile.Loader.ToLower();
        string version = currentProfile.Version;

        using (HttpClient client = new HttpClient())
        {

            string url = $"https://api.modrinth.com/v2/project/{projectId}/version";

            string json = await client.GetStringAsync(url);
            var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(json);

            if (versions == null || versions.Count == 0)
            {
                ShowToast("No versions found!");
                return false;
            }


            IEnumerable<ModrinthVersion> candidateVersions = versions;
            if (stableReleaseOnly)
            {
                candidateVersions = candidateVersions
                    .Where(candidate =>
                        string.Equals(
                            candidate.version_type,
                            "release",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(candidate =>
                        DateTimeOffset.TryParse(
                            candidate.date_published,
                            out DateTimeOffset published)
                            ? published
                            : DateTimeOffset.MinValue);
            }

            ModrinthVersion match =
                string.IsNullOrWhiteSpace(selectedVersionId)
                    ? null
                    : versions.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.id,
                            selectedVersionId,
                            StringComparison.OrdinalIgnoreCase));

            if (match == null &&
                string.IsNullOrWhiteSpace(selectedVersionId) &&
                selectedContentType == "mod")
            {

                match = candidateVersions.FirstOrDefault(v =>
                    v.game_versions?.Contains(
                        version,
                        StringComparer.OrdinalIgnoreCase) == true &&
                    v.loaders?.Contains(
                        loader,
                        StringComparer.OrdinalIgnoreCase) == true
                );
            }
            else if (match == null &&
                     string.IsNullOrWhiteSpace(selectedVersionId) &&
                     selectedContentType == "resourcepack")
            {

                match = candidateVersions.FirstOrDefault(v =>
                            v.game_versions?.Contains(
                                version,
                                StringComparer.OrdinalIgnoreCase) == true
                        )
                        // Use a fallback when no version matches.
                        ?? candidateVersions.FirstOrDefault();
            }
            else if (match == null &&
                     string.IsNullOrWhiteSpace(selectedVersionId) &&
                     selectedContentType is "shader" or "shaderpack")
            {

                match = candidateVersions.FirstOrDefault();
            }
            if (match == null)
            {
                ShowToast("No compatible version!");
                return false;
            }
            if (!IsCompatibleModrinthVersion(
                    match,
                    selectedContentType,
                    version,
                    loader))
            {
                ShowToast("No compatible version!");
                return false;
            }

            var file = match.files?.FirstOrDefault(candidate =>
                           candidate.primary) ??
                       match.files?.FirstOrDefault();

            if (file == null)
            {
                ShowToast("No file found!");
                return false;
            }

            if (selectedContentType == "mod" &&
                !await InstallRequiredModrinthDependenciesAsync(
                    match,
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        projectId
                    },
                    stableReleaseOnly))
            {
                return false;
            }

            string downloadUrl = file.url;
            string ext = selectedContentType switch
            {
                "mod" => ".jar",
                "resourcepack" => ".zip",
                "shader" or "shaderpack" => ".zip",
                _ => ".jar"
            };

            string cleanName = modName;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanName =
                    cleanName.Replace(
                        c.ToString(),
                        "");
            }

            cleanName = cleanName.Trim();

            cleanName += ext;
            


            string folder = selectedContentType switch
            {
                "mod" => "mods",
                "resourcepack" => "resourcepacks",
                "shader" or "shaderpack" => "shaderpacks",
                _ => "mods"
            };

            string basePath = System.IO.Path.Combine(
                GetCurrentProfileDirectory(),
                folder
            );


            Directory.CreateDirectory(basePath);

            string fullPath = System.IO.Path.Combine(basePath, cleanName);


            var data = await client.GetByteArrayAsync(downloadUrl);
            Console.WriteLine("DOWNLOAD URL:");
            Console.WriteLine(downloadUrl);

            Console.WriteLine("FILE NAME:");
            Console.WriteLine(file.filename);

            Console.WriteLine("FULL PATH:");
            Console.WriteLine(fullPath);

            Console.WriteLine("SIZE:");
            Console.WriteLine(data.Length);
            File.WriteAllBytes(fullPath, data);
            Console.WriteLine("FILE EXISTS:");
            Console.WriteLine(File.Exists(fullPath));

            Console.WriteLine("FILE SIZE:");
            Console.WriteLine(new FileInfo(fullPath).Length);

            if (new FileInfo(fullPath).Length == 0)
            {
                File.Delete(fullPath);

                

                var cfMods =
                    await LoadCurseForgeMods(
                        modName,
                        currentProfile.Loader,
                        currentProfile.Version);

                var cf =
                    cfMods.FirstOrDefault(x =>
                        x.name.Equals(
                            modName,
                            StringComparison.OrdinalIgnoreCase));

                return cf != null &&
                       await InstallCurseForgeMod(cf);
            }
            DeleteReplacedContentFile(
                existingItem,
                selectedContentType,
                fullPath);
            AddToProfileJson(
                modName,
                description,
                iconUrl,
                selectedContentType,
                "Modrinth",
                projectId,
                match.id,
                selectedOption?.VersionName ??
                    (string.IsNullOrWhiteSpace(match.version_number)
                        ? match.name
                        : match.version_number),
                file.filename,
                existingItem);
            if (!suppressContentInstallUi)
            {
                ReloadProfileData();
                await LoadContent();
                ShowToast($"Installed: {cleanName}");
            }
            return true;
        }
    }

    private async Task<bool> InstallRequiredModrinthDependenciesAsync(
        ModrinthVersion modVersion,
        HashSet<string> dependencyChain,
        bool stableReleaseOnly = false)
    {
        if (currentProfile == null)
            return false;

        if (dependencyChain.Count > 100)
        {
            ShowToast("Too many nested required dependencies.");
            return false;
        }

        foreach (ModrinthDependency dependency in
                 modVersion.dependencies ?? new List<ModrinthDependency>())
        {
            if (dependency == null ||
                !string.Equals(
                    dependency.dependency_type,
                    "required",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using HttpClient client = new();
                ModrinthVersion dependencyVersion = null;
                string dependencyProjectId = dependency.project_id;

                if (!stableReleaseOnly &&
                    !string.IsNullOrWhiteSpace(dependency.version_id))
                {
                    string pinnedVersionJson =
                        await client.GetStringAsync(
                            "https://api.modrinth.com/v2/version/" +
                            Uri.EscapeDataString(dependency.version_id));

                    dependencyVersion =
                        JsonSerializer.Deserialize<ModrinthVersion>(
                            pinnedVersionJson);

                    if (string.IsNullOrWhiteSpace(dependencyProjectId))
                        dependencyProjectId =
                            dependencyVersion?.project_id;
                }

                if (string.IsNullOrWhiteSpace(dependencyProjectId))
                {
                    throw new InvalidDataException(
                        "A required Modrinth dependency has no resolvable project ID.");
                }

                if (dependencyChain.Contains(dependencyProjectId))
                    continue;

                string projectJson =
                    await client.GetStringAsync(
                        "https://api.modrinth.com/v2/project/" +
                        Uri.EscapeDataString(dependencyProjectId));
                ModrinthProject project =
                    JsonSerializer.Deserialize<ModrinthProject>(
                        projectJson);

                if (string.IsNullOrWhiteSpace(project?.title))
                {
                    throw new InvalidDataException(
                        $"Could not read Modrinth dependency metadata for {dependencyProjectId}.");
                }

                if (IsProfileModInstalled(project.title))
                {
                    dependencyChain.Add(dependencyProjectId);
                    Console.WriteLine(
                        $"[Dependencies] Already installed: {project.title} (Modrinth {dependencyProjectId}).");
                    continue;
                }

                if (!dependencyChain.Add(dependencyProjectId))
                    continue;

                if (dependencyVersion == null)
                {
                    string versionsJson =
                        await client.GetStringAsync(
                            "https://api.modrinth.com/v2/project/" +
                            Uri.EscapeDataString(dependencyProjectId) +
                            "/version");
                    List<ModrinthVersion> versions =
                        JsonSerializer.Deserialize<List<ModrinthVersion>>(
                            versionsJson);

                    IEnumerable<ModrinthVersion> compatibleVersions =
                        versions ?? new List<ModrinthVersion>();
                    if (stableReleaseOnly)
                    {
                        compatibleVersions = compatibleVersions
                            .Where(candidate =>
                                string.Equals(
                                    candidate.version_type,
                                    "release",
                                    StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(candidate =>
                                DateTimeOffset.TryParse(
                                    candidate.date_published,
                                    out DateTimeOffset published)
                                    ? published
                                    : DateTimeOffset.MinValue);
                    }

                    dependencyVersion =
                        compatibleVersions.FirstOrDefault(
                            IsCompatibleModrinthModVersion);
                }

                if (dependencyVersion == null ||
                    !IsCompatibleModrinthModVersion(dependencyVersion))
                {
                    throw new InvalidDataException(
                        $"No compatible version found for required dependency {dependencyProjectId}.");
                }

                if (!await InstallRequiredModrinthDependenciesAsync(
                        dependencyVersion,
                        dependencyChain,
                        stableReleaseOnly))
                {
                    return false;
                }

                ModrinthFile dependencyFile =
                    dependencyVersion.files?.FirstOrDefault(file =>
                        file.primary) ??
                    dependencyVersion.files?.FirstOrDefault();

                if (dependencyFile == null ||
                    string.IsNullOrWhiteSpace(dependencyFile.url))
                {
                    throw new InvalidDataException(
                        $"No downloadable file found for required dependency {project.title}.");
                }

                byte[] data =
                    await client.GetByteArrayAsync(dependencyFile.url);

                if (data.Length == 0)
                {
                    throw new InvalidDataException(
                        $"Downloaded dependency file is empty: {project.title}.");
                }

                string modsFolder = Path.Combine(
                    GetCurrentProfileDirectory(),
                    "mods");
                Directory.CreateDirectory(modsFolder);

                string safeName = project.title;
                foreach (char invalidCharacter in
                         Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(
                        invalidCharacter.ToString(),
                        string.Empty);
                }

                if (string.IsNullOrWhiteSpace(safeName))
                {
                    throw new InvalidDataException(
                        $"The Modrinth dependency has an invalid file name: {project.title}.");
                }

                string destination =
                    Path.Combine(modsFolder, safeName.Trim() + ".jar");
                await File.WriteAllBytesAsync(destination, data);

                AddToProfileJson(
                    project.title,
                    project.description,
                    project.icon_url,
                    "mod",
                    "Modrinth",
                    dependencyVersion.project_id,
                    dependencyVersion.id,
                    string.IsNullOrWhiteSpace(
                        dependencyVersion.version_number)
                        ? dependencyVersion.name
                        : dependencyVersion.version_number,
                    dependencyFile.filename);

                Console.WriteLine(
                    $"[Dependencies] Installed required Modrinth dependency: {project.title}.");
            }
            catch (Exception ex) when (
                ex is HttpRequestException ||
                ex is JsonException ||
                ex is InvalidDataException ||
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is TaskCanceledException)
            {
                Console.WriteLine(
                    $"[Dependencies] Failed to install required Modrinth dependency {dependency.project_id ?? dependency.version_id}: {ex}");
                ShowToast(
                    $"Could not install a required dependency: {dependency.project_id ?? dependency.version_id}");
                return false;
            }
        }

        return true;
    }

    private bool IsCompatibleModrinthModVersion(
        ModrinthVersion version)
    {
        if (currentProfile == null || version == null)
            return false;

        return version.game_versions?.Contains(
                   currentProfile.Version,
                   StringComparer.OrdinalIgnoreCase) == true &&
               version.loaders?.Contains(
                   currentProfile.Loader,
                   StringComparer.OrdinalIgnoreCase) == true;
    }

    private async void FilterChanged(object sender, RoutedEventArgs e)
    {

        if (!IsLoaded)
            return;

        if (ContentList == null)
            return;

        if (ShowInstalledCheck == null)
            return;

        if (currentProfile == null)
            return;

        ContentList.Children.Clear();
        await LoadContent();
    }
    
    private async void ShowToast(string message)
    {
        message = LocalizationManager.Instance.Translate(message);
        Border toast = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10),
            Opacity = 0,
            RenderTransform = new TranslateTransform(0, -20),
            Child = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 14
            }
        };


        ToastContainer.Children.Insert(0, toast);


        var slideIn = new DoubleAnimation
        {
            From = -20,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        ((TranslateTransform)toast.RenderTransform)
            .BeginAnimation(TranslateTransform.YProperty, slideIn);

        toast.BeginAnimation(OpacityProperty, fadeIn);


        await Task.Delay(2000);


        var slideOut = new DoubleAnimation
        {
            From = 0,
            To = -20,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        ((TranslateTransform)toast.RenderTransform)
            .BeginAnimation(TranslateTransform.YProperty, slideOut);

        toast.BeginAnimation(OpacityProperty, fadeOut);

        await Task.Delay(250);

        ToastContainer.Children.Remove(toast);
    }
    private void ReloadProfileData()
    {
        if (currentProfile == null)
            return;


        var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));

        currentProfile = profiles.FirstOrDefault(
            profile => IsSameProfile(profile, currentProfile));

        if (currentProfile == null)
            return;

        LoadInstalledContent();
    }
    private void DeleteInstalledItem(ContentItem item)
    {
        if (currentProfile == null)
            return;

        string folder = GetCurrentFolder();

        string basePath = Path.Combine(
            GetCurrentProfileDirectory(),
            folder);

        if (!Directory.Exists(basePath))
        {
            ShowToast("Folder not found");
            return;
        }

        var files = Directory.GetFiles(basePath);

        string itemClean = item.Name
            .Replace(".zip", "")
            .Replace(".jar", "")
            .Replace(".disabled", "")
            .Trim()
            .ToLower();

        string match = files.FirstOrDefault(f =>
        {
            string clean = Path.GetFileName(f)
                .Replace(".zip.disabled", "")
                .Replace(".jar.disabled", "")
                .Replace(".zip", "")
                .Replace(".jar", "")
                .Replace(".disabled", "")
                .Trim()
                .ToLower();

            return NormalizeName(clean) ==
                   NormalizeName(itemClean);
        });

        if (match == null)
        {
            ShowToast("File not found: " + item.Name);
            return;
        }

        try
        {
            File.Delete(match);
            
        }
        catch (Exception ex)
        {
            ShowToast("Delete failed: " + ex.Message);
        }

        RemoveFromProfileJson(item);
        ReloadProfileData();
        LoadInstalledContent();
    }
    
    private void RemoveFromProfileJson(ContentItem item)
    {
        var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));

        var profile = profiles.FirstOrDefault(
            candidate => IsSameProfile(candidate, currentProfile));

        if (profile == null) return;

        if (currentTab == "mod")
            profile.Mods.RemoveAll(x => x.Name == item.Name);

        else if (currentTab == "resourcepack")
            profile.RP.RemoveAll(x => x.Name == item.Name);

        else if (currentTab == "shader")
            profile.Shaders.RemoveAll(x => x.Name == item.Name);

        File.WriteAllText(filePath, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
    }
    private bool IsFileDisabled(string name)
    {
        string folder = GetCurrentFolder();

        string basePath = Path.Combine(
            GetCurrentProfileDirectory(),
            folder);

        if (!Directory.Exists(basePath))
            return false;

        var files = Directory.GetFiles(basePath);

        string itemClean = name
            .Replace(".zip", "")
            .Replace(".jar", "")
            .Replace(".disabled", "")
            .Trim()
            .ToLower();

        foreach (var f in files)
        {
            string clean = Path.GetFileName(f)
                .Replace(".zip.disabled", "")
                .Replace(".jar.disabled", "")
                .Replace(".zip", "")
                .Replace(".jar", "")
                .Replace(".disabled", "")
                .Trim()
                .ToLower();

            if (NormalizeName(clean) ==
                NormalizeName(itemClean))
            {
                return f.EndsWith(".disabled");
            }
        }

        return false;
    }
    private void ToggleMod(ContentItem item)
    {
        string folder = GetCurrentFolder();

        string basePath = Path.Combine(
            GetCurrentProfileDirectory(),
            folder);

        if (!Directory.Exists(basePath))
        {
            ShowToast("Folder not found");
            return;
        }

        var files = Directory.GetFiles(basePath);


        string match = files.FirstOrDefault(f =>
        {
            string clean = Path.GetFileName(f)
                .Replace(".zip.disabled", "")
                .Replace(".jar.disabled", "")
                .Replace(".zip", "")
                .Replace(".jar", "")
                .Replace(".disabled", "")
                .Trim();

            string itemClean = item.Name
                .Replace(".zip", "")
                .Replace(".jar", "")
                .Replace(".disabled", "")
                .Trim();

            return NormalizeName(clean) ==
                   NormalizeName(itemClean);
                
        });

        if (match == null)
        {
            ShowToast("File not found: [" + item.Name + "]");
            return;
        }

        try
        {
            if (match.EndsWith(".disabled"))
            {
                File.Move(match, match.Replace(".disabled", ""));
                ShowToast("Enabled: " + item.Name);
            }
            else
            {
                File.Move(match, match + ".disabled");
                ShowToast("Disabled: " + item.Name);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Toggle failed: " + ex.Message);
        }

        ReloadProfileData();
        LoadInstalledContent();
    }
    private string GetCurrentFolder()
    {
        return currentTab switch
        {
            "mod" => "mods",
            "resourcepack" => "resourcepacks",
            "shader" => "shaderpacks",
            _ => "mods"
        };
    }
    private void MakeRounded(Button btn)
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, btn.Background);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

        borderFactory.AppendChild(contentFactory);

        btn.Template = new ControlTemplate(typeof(Button))
        {
            VisualTree = borderFactory
        };
    }
    private string GetFileExtension()
    {
        return currentTab switch
        {
            "mod" => ".jar",
            "resourcepack" => ".zip",
            "shader" => ".zip",
            _ => ".jar"
        };
    }

    private void DeleteReplacedContentFile(
        ContentItem existingItem,
        string contentType,
        string newFilePath)
    {
        if (existingItem == null || currentProfile == null)
            return;

        string folder = contentType switch
        {
            "mod" => "mods",
            "resourcepack" => "resourcepacks",
            "shader" or "shaderpack" => "shaderpacks",
            _ => null
        };
        if (folder == null)
            return;

        string directory = Path.Combine(
            GetCurrentProfileDirectory(),
            folder);
        if (!Directory.Exists(directory))
            return;

        string expectedName = NormalizeName(existingItem.Name);
        bool wasDisabled = false;
        foreach (string oldPath in Directory.GetFiles(directory))
        {
            string fileName = Path.GetFileName(oldPath);
            bool disabled = fileName.EndsWith(
                ".disabled",
                StringComparison.OrdinalIgnoreCase);
            string baseName = disabled
                ? fileName[..^".disabled".Length]
                : fileName;
            if (!string.Equals(
                    NormalizeName(Path.GetFileNameWithoutExtension(baseName)),
                    expectedName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            wasDisabled |= disabled;
            if (!string.Equals(
                    Path.GetFullPath(oldPath),
                    Path.GetFullPath(newFilePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(oldPath);
            }
        }

        if (wasDisabled && File.Exists(newFilePath))
        {
            string disabledPath = newFilePath + ".disabled";
            if (File.Exists(disabledPath))
                File.Delete(disabledPath);
            File.Move(newFilePath, disabledPath);
        }
    }

    private void AddToProfileJson(
        string name,
        string description,
        string iconUrl = null,
        string contentType = null,
        string versionProvider = null,
        string projectId = null,
        string versionId = null,
        string versionName = null,
        string sourceFileName = null,
        ContentItem existingItem = null,
        string marketplaceName = null)
    {
        var profiles =
            JsonSerializer.Deserialize<List<Profile>>(
                File.ReadAllText(filePath));

        var profile = profiles.FirstOrDefault(
            candidate => IsSameProfile(candidate, currentProfile));

        if (profile == null)
            return;

        profile.Mods ??= new List<ContentItem>();
        profile.RP ??= new List<ContentItem>();
        profile.Shaders ??= new List<ContentItem>();

        var item = new ContentItem
        {
            Name = name,
            Description = description,
            IconUrl = iconUrl,
            VersionProvider = versionProvider,
            ProjectId = projectId,
            VersionId = versionId,
            VersionName = versionName,
            VersionFileName = sourceFileName,
            MarketplaceName = marketplaceName ?? name
        };

        string targetType = contentType ?? currentTab;
        List<ContentItem> targetItems = targetType switch
        {
            "mod" => profile.Mods,
            "resourcepack" => profile.RP,
            "shader" or "shaderpack" => profile.Shaders,
            _ => null
        };

        if (targetItems != null)
        {
            if (existingItem != null)
            {
                targetItems.RemoveAll(existing =>
                    string.Equals(
                        existing.Name,
                        existingItem.Name,
                        StringComparison.OrdinalIgnoreCase));
            }

            targetItems.Add(item);
        }

        File.WriteAllText(
            filePath,
            JsonSerializer.Serialize(
                profiles,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }
    
    private string AddItem(string current, string newItem)
    {
        if (string.IsNullOrEmpty(current))
            return newItem;

        var items = current.Split(", ").ToList();

        if (!items.Contains(newItem))
            items.Add(newItem);

        return string.Join(", ", items);
    }
    public class ModrinthVersion
    {
        public string id { get; set; }
        public string project_id { get; set; }
        public string name { get; set; }
        public string version_number { get; set; }
        public string version_type { get; set; }
        public string date_published { get; set; }
        public List<string> game_versions { get; set; }
        public List<string> loaders { get; set; }
        public List<ModrinthFile> files { get; set; }
        public List<ModrinthDependency> dependencies { get; set; }
    }

    public class ModrinthDependency
    {
        public string version_id { get; set; }
        public string project_id { get; set; }
        public string dependency_type { get; set; }
    }

    public class JxcPackContentManifest
    {
        public List<JxcPackContentEntry> Items { get; set; } = new();
    }

    public class JxcPackContentEntry
    {
        public string Type { get; set; }
        public string Platform { get; set; }
        public string ProjectId { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
    }

private async Task<string> DownloadRawSkinByName(
    string playerName)
{
    string mpFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource",
            "mp");

    Directory.CreateDirectory(mpFolder);

    string outputFile =
        Path.Combine(
            mpFolder,
            $"{playerName}.png");

    using var client = new HttpClient();

    client.DefaultRequestHeaders.Add(
        "User-Agent",
        "JaxClient/1.0");

    // Retrieve the UUID.
    string profileJson =
        await client.GetStringAsync(
            $"https://api.mojang.com/users/profiles/minecraft/{playerName}");

    using JsonDocument profileDoc =
        JsonDocument.Parse(profileJson);

    string uuid =
        profileDoc.RootElement
            .GetProperty("id")
            .GetString();

    Console.WriteLine("[SKIN] UUID = " + uuid);

    // Retrieve the session profile.
    string sessionJson =
        await client.GetStringAsync(
            $"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");

    using JsonDocument sessionDoc =
        JsonDocument.Parse(sessionJson);

    string texturesBase64 =
        sessionDoc.RootElement
            .GetProperty("properties")[0]
            .GetProperty("value")
            .GetString();

    string texturesJson =
        Encoding.UTF8.GetString(
            Convert.FromBase64String(texturesBase64));

    using JsonDocument texturesDoc =
        JsonDocument.Parse(texturesJson);

    string skinUrl =
        texturesDoc.RootElement
            .GetProperty("textures")
            .GetProperty("SKIN")
            .GetProperty("url")
            .GetString();

    Console.WriteLine("[SKIN] URL = " + skinUrl);

    byte[] skinData =
        await client.GetByteArrayAsync(
            skinUrl);

    await File.WriteAllBytesAsync(
        outputFile,
        skinData);

    Console.WriteLine(
        "[SKIN] Saved: " +
        outputFile);

    return outputFile;
}
private async void MicrosoftLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Console.WriteLine("[LOGIN] Start");
            string redirectUri = "http://localhost:5000/callback";
            string clientId = "e16699bb-2aa8-46da-b5e3-45cbcce29091";

            string authorizeUrl =
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize" +
                $"?client_id={clientId}" +
                "&response_type=code" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&scope=XboxLive.signin offline_access" +
                "&prompt=select_account";
            Console.WriteLine("[LOGIN] Opening browser");

            Process.Start(new ProcessStartInfo
            {
                FileName = authorizeUrl,
                UseShellExecute = true
            });
            Console.WriteLine("[LOGIN] Waiting for OAuth callback");

            var code = await StartLocalServer();
            Console.WriteLine($"[LOGIN] Code received: {!string.IsNullOrEmpty(code)}");
            if (string.IsNullOrEmpty(code))
            {
                ShowToast("Login failed");
                return;
            }
            Console.WriteLine("[LOGIN] Getting Microsoft token");

            var msToken = await ExchangeCodeForToken(code, redirectUri);

            if (msToken == null)
            {
                Console.WriteLine("[LOGIN] Microsoft token NULL");
                ShowToast("Microsoft login failed");
                return;
            }
            Console.WriteLine("[LOGIN] Microsoft token OK");
            // Authenticate through Xbox, XSTS, and Minecraft.
            string xbox = await GetXboxToken(msToken.access_token);
            Console.WriteLine("[LOGIN] Xbox token OK");
            var xsts = await GetXstsToken(xbox);
            Console.WriteLine("[LOGIN] XSTS token OK");
            string mcToken = await GetMinecraftToken(xsts.token, xsts.uhs);
            Console.WriteLine("[LOGIN] Minecraft token OK");
            var profile = await GetProfile(mcToken);

            currentAccountData = new AccountData
            {
                username = profile.name,
                uuid = profile.uuid,
                accessToken = mcToken,
                refreshToken = msToken.refresh_token,
                selectedSkin = "",
                lastTokenRefreshUtc = DateTimeOffset.UtcNow
            };
            AccountNameText.Text = profile.name;

            SaveAccount(
                profile.name,
                profile.uuid,
                mcToken,
                msToken.refresh_token,
                "",
                currentAccountData.lastTokenRefreshUtc);

            LoadAccountsUI();
            UpdateWelcomeText();
            SaveLastAccount(profile.name);

            string renderedSkinFile = "";
            try
            {
                await DownloadBodySkin(profile.name, profile.uuid);
                Console.WriteLine("[LOGIN] Head downloaded");

                string rawSkinFile =
                    await DownloadRawSkinByName(profile.name);

                Console.WriteLine(
                    "[LOGIN] Raw Skin: " + rawSkinFile);

                string renderFile =
                    await SkinPreviewWindow.ShowPreview(
                        rawSkinFile,
                        profile.name);

                if (string.IsNullOrWhiteSpace(renderFile) ||
                    !File.Exists(renderFile))
                {
                    throw new InvalidOperationException(
                        "Skin renderer did not produce an image.");
                }

                Console.WriteLine(
                    "[LOGIN] Render result: " + renderFile);

                SaveSkinEntry(
                    profile.name,
                    Path.GetFileName(renderFile));

                SaveAccount(
                    profile.name,
                    profile.uuid,
                    mcToken,
                    msToken.refresh_token,
                    Path.GetFileName(renderFile),
                    currentAccountData.lastTokenRefreshUtc);

                currentAccountData.selectedSkin =
                    Path.GetFileName(renderFile);
                renderedSkinFile = renderFile;
            }
            catch (Exception renderException)
            {
                currentAccountData.selectedSkin = "";
                Console.WriteLine(
                    "[LOGIN] Skin render failed: " + renderException);
                ShowToast("Logged in without a rendered skin.");
            }

            if (!string.IsNullOrEmpty(renderedSkinFile))
            {
                try
                {
                    LoadSavedSkins();
                    LoadAccountHead(profile.name);
                    LoadSkinLocal();
                    LoadAccountsUI();
                }
                catch (Exception skinUiException)
                {
                    Console.WriteLine(
                        "[LOGIN] Rendered skin UI update failed: " + skinUiException);
                }
            }

            Console.WriteLine("[LOGIN] Finished successfully");

            
        }
        catch (Exception ex)
        {
            ShowToast("Login error: " + ex.Message);
            Console.WriteLine("Error:"+ex.Message);
        }
    }
    private bool nrcEnabledUI = false;

    private void NRC_Toggle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        nrcEnabledUI = !nrcEnabledUI;

        if (nrcEnabledUI)
        {
            NRC_Toggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));

            NRC_Knob.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            NRC_Toggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));

            NRC_Knob.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }
    private void LaunchOffline_Click(object sender, RoutedEventArgs e)
    {
        string username =
            OfflineUsernameBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowToast("Enter a username");
            return;
        }

        currentAccountData = new AccountData
        {
            username = username,
            uuid = Guid.NewGuid().ToString("N"),
            accessToken = "offline"
        };

        OfflineLoginOverlay.Visibility =
            Visibility.Collapsed;

        LaunchMinecraftOffline();
    }
    private void ThrowIfCancelled()
    {
        if (cancelLaunch)
            throw new OperationCanceledException();

        launchCts?.Token.ThrowIfCancellationRequested();
    }
    
    private void LaunchBtn_Click(object sender, RoutedEventArgs e)
    {
        
        if (isLaunching)
        {
            cancelLaunch = true;

            launchCts?.Cancel();

            try
            {
                currentMinecraftProcess?.Kill(true);
            }
            catch { }

            currentTasks.Clear();
            UpdateTaskUI();

            ResetLaunchUI();

            ShowToast("Launch cancelled");
            return;
        }
        
        if (currentProfile == null)
        {
            ShowToast("No profile selected");
            return;
        }

        if (IsOfflineMode)
            
        {
            
            OfflineLoginOverlay.Visibility = Visibility.Visible;
            
            return;
            
        }
        if (currentAccountData == null)
        {
            ShowToast("Not logged in");
            return;
        }

        cancelLaunch = false;
        isLaunching = true;


        LaunchBtn.Tag = "STOP";

        LaunchBtn.Background =
            new SolidColorBrush(Color.FromRgb(220, 38, 38));

        ProfileSwitchBtn.IsEnabled = false;
        ProfilesBtn.IsEnabled = false;
        CreateProfileBtn.IsEnabled = false;
        ProfileSwitchBtn.Visibility = Visibility.Collapsed;
        launchCts = new CancellationTokenSource();
        cancelLaunch = false;
        LaunchMinecraft();
    }
    private async Task DownloadNRCFiles()
    {
        if (cancelLaunch ||
            currentProfile == null ||
            !currentProfile.NrcEnabled)
        {
            return;
        }

        string? temporaryZipPath = null;
        try
        {
            string resourcePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource"
            );

            Directory.CreateDirectory(resourcePath);


            int matchingMods = Directory.GetFiles(resourcePath, "*.jar", SearchOption.AllDirectories)
                .Count(f => System.IO.Path.GetFileName(f).Contains(currentProfile.Version));


            if (matchingMods >= 5)
            {
                Console.WriteLine("✅ NRC already installed → skip download");
                return;
            }

            
            StartTask("Download NRC...");

            using HttpClient client = new HttpClient();


            string url = "https://www.dropbox.com/scl/fi/yomc0j742ave5sx29y37j/NRC-mods.zip?rlkey=4j4rb4ub14ghgoh0xc9q59tl2&dl=1";

            string zipPath = System.IO.Path.Combine(resourcePath, "nrc.zip");
            temporaryZipPath = zipPath + ".download";
            ThrowIfCancelled();
            using HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync();
            ThrowIfCancelled();
            await File.WriteAllBytesAsync(temporaryZipPath, data);

            using (ZipFile.OpenRead(temporaryZipPath))
            {
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(
                temporaryZipPath,
                resourcePath,
                true
            );

            File.Move(temporaryZipPath, zipPath, true);
            temporaryZipPath = null;

            
            StopTask("Download NRC...");
        }
        catch (Exception ex)
        
        {
            ShowToast("NRC download failed!");
            StopTask("Download NRC...");
            Console.WriteLine(ex);
        }
        finally
        {
            if (temporaryZipPath != null &&
                File.Exists(temporaryZipPath))
            {
                File.Delete(temporaryZipPath);
            }
        }
    }
    private void ApplyNRCMods()
    {
        try
        {
            if (cancelLaunch)
            {
                ResetLaunchUI();
                return;
            }

            if (currentProfile == null)
                return;

            if (!currentProfile.NrcEnabled)
                return;

            string resourcePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource"
            );

            string modsPath = Path.Combine(
                GetCurrentProfileDirectory(),
                "mods");

            Directory.CreateDirectory(modsPath);


            foreach (var file in Directory.GetFiles(resourcePath, "*.*", SearchOption.AllDirectories))
            {
                string fileName = System.IO.Path.GetFileName(file);


                if (!fileName.Contains(currentProfile.Version))
                    continue;

                string target = System.IO.Path.Combine(modsPath, fileName);

                File.Copy(file, target, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
    private async Task InstallStandardContent(
        Profile profile)
    {
        var settings =
            LoadSettingsObject();

        if (settings.StandardContent == null)
            return;

        foreach (var item in settings.StandardContent)
        {
            await InstallSingleStandardContent(
                profile,
                item);
        }
    }

    private void ApplyStandardOptions(Profile profile)
    {
        string? optionsContent =
            LoadSettingsObject().StandardOptionsContent;
        if (optionsContent == null)
            return;

        string targetPath = GetProfileOptionsPath(profile);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, optionsContent);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowToast($"Could not apply standard options: {ex.Message}");
        }
    }

private async Task InstallSingleStandardContent(
    Profile profile,
    StandardContentItem item)
{
    string oldTab = currentTab;
    Profile oldProfile = currentProfile;

    try
    {
        currentProfile = profile;


        currentTab = item.Type;

        using HttpClient client =
            new HttpClient();


        if (item.Platform == "Modrinth")
        {
            string searchUrl =
                $"https://api.modrinth.com/v2/search?limit=20&query={Uri.EscapeDataString(item.Name)}";

            string json =
                await client.GetStringAsync(searchUrl);

            var result =
                JsonSerializer.Deserialize<ModrinthResponse>(
                    json);

            ModrinthProject match = null;

            foreach (var mod in result?.hits ?? [])
            {
                if (NormalizeName(mod.title)
                    == NormalizeName(item.Name))
                {
                    match = mod;
                    break;
                }
            }

            if (match != null)
            {
                await InstallMod(
                    match.project_id,
                    match.title,
                    match.description);

                return;
            }
        }


        if (item.Platform == "CurseForge")
        {
            var curseForge =
                await LoadCurseForgeMods(
                    item.Name,
                    profile.Loader.ToLower(),
                    profile.Version);

            CurseForgeMod cfMatch =
                curseForge.FirstOrDefault(x =>
                    NormalizeName(x.name)
                    == NormalizeName(item.Name));

            if (cfMatch != null)
            {
                await InstallCurseForgeMod(
                    cfMatch);

                return;
            }
        }



        string fallbackUrl =
            $"https://api.modrinth.com/v2/search?limit=20&query={Uri.EscapeDataString(item.Name)}";

        string fallbackJson =
            await client.GetStringAsync(fallbackUrl);

        var fallbackResult =
            JsonSerializer.Deserialize<ModrinthResponse>(
                fallbackJson);

        var fallbackMod =
            fallbackResult?.hits?
                .FirstOrDefault(x =>
                    NormalizeName(x.title)
                    == NormalizeName(item.Name));

        if (fallbackMod != null)
        {
            await InstallMod(
                fallbackMod.project_id,
                fallbackMod.title,
                fallbackMod.description);

            return;
        }

        var cfFallback =
            await LoadCurseForgeMods(
                item.Name,
                profile.Loader.ToLower(),
                profile.Version);

        var cfMatchFallback =
            cfFallback.FirstOrDefault(x =>
                NormalizeName(x.name)
                == NormalizeName(item.Name));

        if (cfMatchFallback != null)
        {
            await InstallCurseForgeMod(
                cfMatchFallback);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"STANDARD CONTENT FAILED: {item.Name}");

        Console.WriteLine(ex);
    }
    finally
    {
        currentProfile = oldProfile;
        currentTab = oldTab;
    }
}


private void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            
            if (IsOfflineMode)
                
            {
                
                ShowToast("You must be online to create profiles.");
                
                return;
                
            }
            bool nrcEnabled = nrcEnabledUI;


        
        string name = ProfileNameBox.Text.Trim();


        if (string.IsNullOrWhiteSpace(name))
        {
            ShowToast("Name is required!");
            return;
        }


        string loader =
            (LoaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? string.Empty;
        string loaderVersion =
            loader?.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) == true
                ? ""
                : (LoaderVersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                    ?? string.Empty;
            

        string version =
            (VersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(loader) ||
            string.IsNullOrWhiteSpace(version) ||
            (!loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) &&
             (string.IsNullOrWhiteSpace(loaderVersion) ||
              loaderVersion is "Loading..." or "Error loading" or "No versions found")))
        {
            ShowToast("Select a valid Minecraft and loader version.");
            return;
        }

        

        string folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient"
        );

        Directory.CreateDirectory(folder);

        string filePath = System.IO.Path.Combine(folder, "profiles.json");


        var profile = new Profile
        {
            Name = name,
            Loader = loader,
            Version = version,
            LoaderVersion = loaderVersion,
            NrcEnabled = nrcEnabled,
            ProfilePicture = GetRandomProfilePicture()
        };


        List<Profile> profiles;

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);

            profiles = JsonSerializer.Deserialize<List<Profile>>(json)
                       ?? new List<Profile>();
        }
        else
        {
            profiles = new List<Profile>();
        }

        bool exists = profiles.Any(p => 
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );

        if (exists)
        {
            ShowToast("Profile already exists!");
            return;
        }
        

        profiles.Add(profile);


        string output = JsonSerializer.Serialize(
            profiles,
            new JsonSerializerOptions { WriteIndented = true }
        );

        File.WriteAllText(filePath, output);
        ProfileNameBox.Text = "";
        LoadProfiles();
        CloseCreateProfile_Click(null, null);
        Directory.CreateDirectory(Path.Combine(folder, "Profiles", name, "mods"));
        Directory.CreateDirectory(Path.Combine(folder, "Profiles", name, "resourcepacks"));
        Directory.CreateDirectory(Path.Combine(folder, "Profiles", name, "shaderpacks"));
        ApplyStandardOptions(profile);
        OpenProfile(profile);

        _ = CompleteCreatedProfileSetupAsync(profile);
    }

    private async Task CompleteCreatedProfileSetupAsync(Profile profile)
    {
        string? activeTask = null;

        try
        {
            activeTask = "Downloading Java...";
            StartTask(activeTask);
            await DownloadJavaVersions();
            ThrowIfCancelled();
            StopTask(activeTask);
            activeTask = null;

            activeTask = "Downloading Minecraft...";
            StartTask(activeTask);
            if (!await DownloadMinecraftClient(profile.Version))
                throw new IOException(
                    $"Minecraft {profile.Version} could not be downloaded.");
            ThrowIfCancelled();
            StopTask(activeTask);
            activeTask = null;

            activeTask = "Downloading Minecraft libraries...";
            StartTask(activeTask);
            if (!await DownloadLibraries(profile.Version))
                throw new IOException(
                    $"Minecraft libraries for {profile.Version} could not be downloaded.");
            ThrowIfCancelled();
            StopTask(activeTask);
            activeTask = null;

            if (profile.Loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
            {
                activeTask = "Installing Fabric...";
                StartTask(activeTask);
                await DownloadFabricProfile(profile.Version, profile.LoaderVersion);
                ThrowIfCancelled();
                if (!File.Exists(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".JAXSTORE",
                        "JaxClient",
                        "meta",
                        "versions",
                        profile.Version,
                        $"fabric-loader-{profile.LoaderVersion}-{profile.Version}.json")))
                {
                    throw new IOException(
                        $"Fabric {profile.LoaderVersion} could not be installed.");
                }
                StopTask(activeTask);
                activeTask = null;

                activeTask = "Downloading Fabric libraries...";
                StartTask(activeTask);
                if (!await DownloadFabricLibraries(
                        profile.Version,
                        profile.LoaderVersion))
                {
                    throw new IOException(
                        $"Fabric libraries for {profile.Version} could not be downloaded.");
                }
                ThrowIfCancelled();
                StopTask(activeTask);
                activeTask = null;
            }
            else if (profile.Loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
            {
                activeTask = "Installing Forge...";
                StartTask(activeTask);
                if (!await DownloadForgeProfile(profile.Version, profile.LoaderVersion))
                    throw new IOException(
                        $"Forge {profile.LoaderVersion} could not be installed.");
                ThrowIfCancelled();
                StopTask(activeTask);
                activeTask = null;

                activeTask = "Downloading Forge libraries...";
                StartTask(activeTask);
                if (!await DownloadForgeLibraries(profile.Version, profile.LoaderVersion))
                    throw new IOException(
                        $"Forge libraries for {profile.Version} could not be downloaded.");
                ThrowIfCancelled();
                StopTask(activeTask);
                activeTask = null;
            }
            else if (profile.Loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                activeTask = "Installing NeoForge...";
                StartTask(activeTask);
                if (!await DownloadNeoForgeProfile(
                        profile.Version,
                        profile.LoaderVersion))
                {
                    throw new IOException(
                        $"NeoForge {profile.LoaderVersion} could not be installed.");
                }
                ThrowIfCancelled();
                StopTask(activeTask);
                activeTask = null;
            }
            activeTask = "Downloading Minecraft assets...";
            StartTask(activeTask);
            if (!await DownloadAssets(profile.Version))
                throw new IOException(
                    $"Minecraft assets for {profile.Version} could not be downloaded.");
            ThrowIfCancelled();
            StopTask(activeTask);
            activeTask = null;

            activeTask = "Installing standard content...";
            StartTask(activeTask);
            await InstallStandardContent(profile);
            StopTask(activeTask);
            activeTask = null;
        }
        catch (Exception ex)
        {
            if (activeTask != null)
                StopTask(activeTask);

            Console.WriteLine(
                $"Background setup failed for profile '{profile.Name}': {ex}");
            ShowToast(
                $"Profile created, but setup failed: {ex.Message}");
        }
    }

    private async Task EnsureJxcResourcePack()
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return;
        }

        try
        {
            string resourcePath = Path.Combine(
                GetCurrentProfileDirectory(),
                "resourcepacks");

            Directory.CreateDirectory(resourcePath);

            string filePath = Path.Combine(resourcePath, "jxc_icon.zip");


            if (File.Exists(filePath))
            {
                Console.WriteLine("[JXC] Resourcepack already exists ✅");
                return;
            }

            StartTask("Downloading ResourcePack...");

            using HttpClient client = new HttpClient();


            string url = "https://www.dropbox.com/scl/fi/ah1rkrgditzf6yhsitca1/jxc_icon.zip?rlkey=x4i2pwa8liercb0jzgzl35tki&dl=1";
            ThrowIfCancelled();
            byte[] data = await client.GetByteArrayAsync(url);
            ThrowIfCancelled();
            await File.WriteAllBytesAsync(filePath, data);

            StopTask("Downloading ResourcePack...");

            Console.WriteLine("[JXC] Resourcepack downloaded ✅");
        }
        catch (Exception ex)
        {
            ShowToast("Resourcepack download failed!");
            StopTask("Downloading ResourcePack...");
            Console.WriteLine(ex);
        }
        
        
    }
    public class RunningInstance
    {
        public Process Process { get; set; }

        public string ProfileName { get; set; }

        public string Version { get; set; }
        public string Username { get; set; }
        public string Loader { get; set; }

        public ObservableCollection<string> ConsoleLines { get; set; } = new();
    }
    private string NormalizeCompareName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = NormalizeName(text);

        text = text.Replace(" ", "");
        text = text.Replace("-", "");
        text = text.Replace("_", "");
        text = text.Replace(".", "");
        text = text.Replace("+", "");

        return text.Trim().ToLowerInvariant();
    }
        
    private string NormalizeName(string text)
    {
        text = text.ToLowerInvariant();


        text = Regex.Replace(
            text,
            @"\bv?\d+(\.\d+)+\b",
            "",
            RegexOptions.IgnoreCase);

        // Match both mc1.20.1 and mc_1.20.1 version formats.
        text = Regex.Replace(
            text,
            @"mc[\s_\-]*\d+(\.\d+)+",
            "",
            RegexOptions.IgnoreCase);

        // Remove the loader suffix.
        text = Regex.Replace(
            text,
            @"\bfabric\b",
            "",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"\bforge\b",
            "",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"\bneoforge\b",
            "",
            RegexOptions.IgnoreCase);

// Match the mc1.20.1 version format.
        text = Regex.Replace(
            text,
            @"mc\d+(\.\d+)*",
            "",
            RegexOptions.IgnoreCase);

// Match 1.20.x versions.
        text = Regex.Replace(
            text,
            @"\d+\.\d+\.x",
            "",
            RegexOptions.IgnoreCase);

// Match individual version numbers.
        text = Regex.Replace(
            text,
            @"\b\d+(\.\d+)+\b",
            "",
            RegexOptions.IgnoreCase);


        text = text.Replace("+", "");
        text = text
            .Replace(":", "")
            .Replace("!", "")
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace(".jar", "")
            .Replace(".zip", "")
            .Replace(".disabled", "");


        text = Regex.Replace(
            text,
            @"\s+",
            " ");

        return text.Trim();
    }
    private void UpdateDiscordPresence()
    {
        bool ingame =
            runningMinecraftInstances.Count > 0;
    
        rpc.SetPresence(
            new DiscordRPC.RichPresence
            {
                Details = $"JaxClient {TopVersionText.Text}",
    
                State = ingame
                    ? $"Ingame ({currentProfile?.Loader} {currentProfile?.Version})"
                    : "Launcher",
    
                Assets = new DiscordRPC.Assets
                {
                    LargeImageKey = "jaxclient",
                    LargeImageText = "JaxClient"
                },
    
                Buttons = new[]
                {
                    new DiscordRPC.Button
                    {
                        Label = "Download",
                        Url = "https://jaxroxn.lan64.de/space/jaxclient"
                    }
                },
    
                Timestamps = ingame
                    ? new DiscordRPC.Timestamps(DateTime.UtcNow)
                    : null
            });
    }
    private async void LaunchMinecraftOffline()
{
    try
    {
            EnsureSupportedProfileLoader(currentProfile);
            ThrowIfCancelled();
                string? noRiskModsDirectory = null;
                if (currentProfile?.IsNoRiskProfile == true)
                {
                    StartTask("Preparing NoRisk profile mods...");
                    noRiskModsDirectory =
                        await PrepareNoRiskProfileModsAsync(currentProfile);
                    StopTask("Preparing NoRisk profile mods...");
                }

        cancelLaunch = false;
        isLaunching = true;


        LaunchBtn.Tag = "STOP";
        LaunchBtn.Background =
            new SolidColorBrush(Color.FromRgb(220, 38, 38));
            
        ProfileSwitchBtn.Visibility = Visibility.Collapsed;
        ProfileSwitchBtn.IsEnabled = false;
        ProfilesBtn.IsEnabled = false;
        CreateProfileBtn.IsEnabled = false;
        StartTask("Preparing launch...");

        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE", "JaxClient");

        string version = currentProfile.Version;
        string loaderVersion = currentProfile.LoaderVersion;


        string javaFolder =
            (currentProfile.Loader.Equals(
                 "Forge",
                 StringComparison.OrdinalIgnoreCase) ||
             currentProfile.Loader.Equals(
                 "NeoForge",
                 StringComparison.OrdinalIgnoreCase)) &&
            version.StartsWith("26.")
                ? "jdk-25"
                : version.StartsWith("1.21")
                    ? "jdk-21"
                    : "jdk-17";

        try
        {
            string localVersionJsonPath = Path.Combine(basePath, "meta", "versions", version, $"{version}.json");
            if (File.Exists(localVersionJsonPath))
            {
                using var vDoc = JsonDocument.Parse(File.ReadAllText(localVersionJsonPath));
                if (vDoc.RootElement.TryGetProperty("javaVersion", out var javaProp))
                {
                    int javaVer = javaProp.GetProperty("majorVersion").GetInt32();
                    javaFolder = $"jdk-{javaVer}";
                }
            }
        }
        catch (Exception ex)
        {
            javaFolder = "jdk-21"; // Fallback.
        }

        string javaPath = System.IO.Path.Combine(
            basePath, "meta", "Java", javaFolder, "bin", "java.exe");
        if (!File.Exists(javaPath))
        {
            ShowToast("Java is not installed!");
            StopTask("Preparing launch...");
            return;
        }


        try
        {
            var test = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = Process.Start(test);

            string stdOut = p.StandardOutput.ReadToEnd();
            string stdErr = p.StandardError.ReadToEnd();

            p.WaitForExit();

            if (javaFolder == "jdk-25" && !stdErr.Contains("\"25"))
            {
                throw new Exception("❌ WRONG JAVA VERSION! Expected 25!");
            }
        }
        catch (Exception ex)
        {
        }


        try
        {
            var sys = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p2 = Process.Start(sys);

            string stdOut2 = p2.StandardOutput.ReadToEnd();
            string stdErr2 = p2.StandardError.ReadToEnd();

            p2.WaitForExit();
        }
        catch
        {
        }

        if (currentProfile.Loader.Equals(
                "NeoForge",
                StringComparison.OrdinalIgnoreCase) &&
            !await DownloadNeoForgeProfile(version, loaderVersion))
        {
            StopTask("Preparing launch...");
            return;
        }


        string mainClass;
        string forgeJvmArgs = "";
        string forgeGameArgs = "";
        List<string> classpathList = new List<string>();

        if (currentProfile.Loader == "Fabric")
        {
            string fabricJsonPath = Path.Combine(
                basePath,
                "meta",
                "versions",
                version,
                $"fabric-loader-{loaderVersion}-{version}.json");
            ThrowIfCancelled();

            string fabricJson = File.ReadAllText(fabricJsonPath);

            using var doc = JsonDocument.Parse(fabricJson);
            ValidateFabricProfileMetadata(
                doc.RootElement,
                version,
                loaderVersion);

            mainClass =
                doc.RootElement.GetProperty("mainClass").GetString();
        }
        else if (currentProfile.Loader.Equals(
                     "Vanilla",
                     StringComparison.OrdinalIgnoreCase))
        {
            string vanillaJsonPath = Path.Combine(
                basePath,
                "meta",
                "versions",
                version,
                $"{version}.json");
            if (!File.Exists(vanillaJsonPath))
                throw new FileNotFoundException(
                    "Vanilla Minecraft metadata is not installed. Launch this profile once online first.",
                    vanillaJsonPath);
            using JsonDocument doc =
                JsonDocument.Parse(File.ReadAllText(vanillaJsonPath));
            mainClass =
                doc.RootElement.GetProperty("mainClass").GetString();
        }
        else if (currentProfile.Loader.Equals(
                     "NeoForge",
                     StringComparison.OrdinalIgnoreCase))
        {
            NeoForgeLaunchConfiguration neoForge =
                LoadNeoForgeLaunchConfiguration(
                    Path.Combine(basePath, "meta"),
                    version,
                    loaderVersion);
            mainClass = neoForge.MainClass;
            forgeJvmArgs = neoForge.JvmArguments;
            forgeGameArgs = neoForge.GameArguments;
            classpathList.AddRange(neoForge.Libraries);
        }
        else
        {
            string forgeVersion = currentProfile.LoaderVersion;

            // Extract the loader version from values such as 26.2-65.1.2.
            if (forgeVersion.StartsWith(currentProfile.Version + "-"))
            {
                forgeVersion =
                    forgeVersion.Substring(
                        currentProfile.Version.Length + 1);
            }

            string wantedForgeJson =
                $"{currentProfile.Version}-forge-{forgeVersion}.json";

            string forgeJson = Directory.GetFiles(
                    Path.Combine(basePath, "Profiles", "ForgeProfiles", "versions"),
                    "*.json",
                    SearchOption.AllDirectories)
                .FirstOrDefault(x =>
                    Path.GetFileName(x)
                        .Equals(
                            wantedForgeJson,
                            StringComparison.OrdinalIgnoreCase));

            if (forgeJson == null)
            {
                throw new Exception(
                    "Forge JSON not found: " +
                    wantedForgeJson);
            }

            Console.WriteLine("Expected Forge JSON:");
            Console.WriteLine(wantedForgeJson);

            Console.WriteLine("Selected Forge JSON:");
            Console.WriteLine(forgeJson);

            using var doc = JsonDocument.Parse(
                    File.ReadAllText(forgeJson));
            var root = doc.RootElement;
            string forgeLibraries =
                Path.Combine(
                    basePath,
                    "Profiles",
                    "ForgeProfiles",
                    "libraries");

            foreach (var lib in root.GetProperty("libraries").EnumerateArray())
            {
                if (!lib.TryGetProperty("downloads", out var downloads))
                    continue;

                if (!downloads.TryGetProperty("artifact", out var artifact))
                    continue;

                string rel =
                    artifact.GetProperty("path").GetString();

                string full =
                    Path.Combine(
                        forgeLibraries,
                        rel.Replace('/', '\\'));

                if (!File.Exists(full))
                    continue;

                string fileName =
                    Path.GetFileName(full).ToLower();

                if (fileName.Contains("bootstraplauncher"))
                    continue;

                if (fileName.Contains("securejarhandler"))
                    continue;

                classpathList.Add(full);
            }
            var jvmArgs =
                root.GetProperty("arguments")
                    .GetProperty("jvm");

            StringBuilder jvmBuilder =
                new StringBuilder();

            foreach (var arg in jvmArgs.EnumerateArray())
            {
                if (arg.ValueKind != JsonValueKind.String)
                    continue;

                string value = arg.GetString();

                value = value.Replace(
                    "${library_directory}",
                    forgeLibraries);

                value = value.Replace(
                    "${classpath_separator}",
                    ";");

                value = value.Replace(
                    "${version_name}",
                    root.GetProperty("id").GetString());

                jvmBuilder.Append(value);
                jvmBuilder.Append(" ");
            }
            var gameArgs =
                root.GetProperty("arguments")
                    .GetProperty("game");

            StringBuilder gameBuilder =
                new StringBuilder();

            foreach (var arg in gameArgs.EnumerateArray())
            {
                gameBuilder.Append(arg.GetString());
                gameBuilder.Append(" ");
            }

            forgeGameArgs = gameBuilder.ToString();
            forgeJvmArgs = jvmBuilder.ToString();
// Java 21 does not support this format.
            forgeJvmArgs = forgeJvmArgs.Replace(
                "-XX:+UseCompactObjectHeaders",
                "");

            mainClass =
                doc.RootElement
                    .GetProperty("mainClass")
                    .GetString();

            Console.WriteLine("FORGE JSON = " + forgeJson);
        }


        string libsFolder = System.IO.Path.Combine(basePath, "meta", "libraries", version);

        if (Directory.Exists(libsFolder))
        {
            Dictionary<string, string> bestLibs = new();
            Dictionary<string, Version> bestVersions = new();

            foreach (var file in Directory.GetFiles(libsFolder, "*.jar", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);


                if (name.Contains("natives-linux") || name.Contains("natives-macos"))
                    continue;


                if (currentProfile.Loader == "Fabric")
                {
                    if (name.StartsWith("fabric-loader"))
                    {
                        string expected =
                            $"fabric-loader-{loaderVersion}";

                        if (!name.StartsWith(expected))
                            continue;

                        classpathList.Add(file);
                        continue;
                    }
                }
                else if (currentProfile.Loader == "Forge")
                {
                    if (name.Contains("fabric"))
                        continue;

                    if (name.Contains("intermediary"))
                        continue;
                    if (name.StartsWith("asm"))
                        continue;
                    if (name.Contains("forge-"))
                        continue;

                    if (name.Contains("bootstraplauncher"))
                        continue;

                    if (name.Contains("securejarhandler"))
                        continue;

                    if (name.Contains("modlauncher"))
                        continue;
                }


                try
                {
                    int lastDash = name.LastIndexOf("-");
                    if (lastDash <= 0)
                    {
                        classpathList.Add(file);
                        continue;
                    }

                    string libName = name.Substring(0, lastDash);
                    string versionStr = name.Substring(lastDash + 1).Replace(".jar", "");

                    // Remove suffixes such as mixin.
                    versionStr = versionStr.Split('+')[0];

                    if (!Version.TryParse(versionStr, out Version ver))
                        ver = new Version(0, 0);


                    if (!bestVersions.ContainsKey(libName) || ver > bestVersions[libName])
                    {
                        bestVersions[libName] = ver;
                        bestLibs[libName] = file;
                    }
                }
                catch
                {
                    classpathList.Add(file); // Fallback.
                }
            }


            foreach (var lib in bestLibs.Values)
            {
                classpathList.Add(lib);
            }
        }

        string mcJar =
            Path.Combine(basePath,
                "meta",
                "versions",
                version,
                $"{version}.jar");

        if (!File.Exists(mcJar))
        {
            ShowToast("Minecraft version is not installed!");
            StopTask("Preparing launch...");
            return;
        }
        if (currentProfile.Loader != "Forge")
        {
            classpathList.Add(mcJar);
        }

        foreach (var x in classpathList)
        {
            if (x.Contains("mclanguage") ||
                x.Contains("javafmllanguage") ||
                x.Contains("lowcodelanguage"))
            {
                Console.WriteLine("DUPLICATE DEBUG -> " + x);
            }
        }
        string classpath = string.Join(";", classpathList);
        Console.WriteLine("=================================");
        Console.WriteLine("Loader: " + currentProfile.Loader);
        Console.WriteLine("MainClass: " + mainClass);
        Console.WriteLine("Classpath:");
        Console.WriteLine(classpath);
        Console.WriteLine("=================================");


        string nativesPath = System.IO.Path.Combine(
            basePath,
            "meta",
            "natives",
            version + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
        string gameDir = GetProfileDirectory(currentProfile);

        Directory.CreateDirectory(nativesPath);
        if (!currentProfile.IsExternalProfile)
        {
            Directory.CreateDirectory(gameDir);
        }
        ExtractMinecraftNatives(
            Path.Combine(basePath, "meta", "versions", version, $"{version}.json"),
            libsFolder,
            nativesPath);


        string assetIndex = null;
        string localMcVersionJson = System.IO.Path.Combine(basePath, "meta", "versions", version, $"{version}.json");

        if (File.Exists(localMcVersionJson))
        {
            using var vDoc = JsonDocument.Parse(File.ReadAllText(localMcVersionJson));
            if (vDoc.RootElement.TryGetProperty("assetIndex", out var aIdx) && aIdx.TryGetProperty("id", out var aId))
            {
                assetIndex = aId.GetString();
            }
        }
        if (string.IsNullOrWhiteSpace(assetIndex))
        {
            string assetsIndexes =
                Path.Combine(basePath, "meta", "assets", "indexes");

            string firstIndex =
                Directory.GetFiles(assetsIndexes, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .FirstOrDefault();

            assetIndex = firstIndex;
        }
        Console.WriteLine("ASSETINDEX=" + assetIndex);
        string assetsRoot = System.IO.Path.Combine(basePath, "meta", "assets");
        string offlineUuid = Guid.NewGuid().ToString("N");
        string customJvm =
            JvmArgumentsBox.Text?.Trim() ?? "";

        bool usesMinecraftVersionArguments =
            currentProfile.Loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase);
        string gameArguments = usesMinecraftVersionArguments
            ? BuildMinecraftGameArguments(
                Path.Combine(basePath, "meta", "versions", version, $"{version}.json"),
                version,
                currentAccountData,
                gameDir,
                assetsRoot,
                assetIndex,
                offline: true,
                offlineUuid: offlineUuid)
            : forgeGameArgs +
              $"--username \"{currentAccountData.username}\" " +
              $"--uuid {offlineUuid} " +
              $"--accessToken 0 " +
              $"--version \"{version}\" " +
              $"--gameDir \"{gameDir}\" " +
              $"--assetsDir \"{assetsRoot}\" " +
              $"--assetIndex \"{assetIndex}\" " +
              $"--userType legacy";
        string args =
            forgeJvmArgs +
            GetNoRiskModsFolderArgument(
                currentProfile,
                noRiskModsDirectory) +
            customJvm + " " +
            $"-Xmx{(int)RamSlider.Value}G " +
            $"-Djava.library.path=\"{nativesPath}\" " +
            $"-cp \"{classpath}\" " +
            $"{mainClass} " +
            gameArguments;

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = System.IO.Path.GetDirectoryName(javaPath)
        };


        psi.EnvironmentVariables["JAVA_HOME"] =
            System.IO.Path.Combine(basePath, "meta", "Java", javaFolder);

        psi.EnvironmentVariables["Path"] =
            System.IO.Path.Combine(basePath, "meta", "Java", javaFolder, "bin");

        Console.WriteLine("===== FORGE DEBUG =====");
        Console.WriteLine("MainClass:");
        Console.WriteLine(mainClass);

        Console.WriteLine("Classpath Count:");
        Console.WriteLine(classpathList.Count);

        foreach (string cp in classpathList)
        {
            Console.WriteLine(cp);
        }
        Console.WriteLine("=======================");

        Process process = new Process();
        process.StartInfo = psi;
       
        Console.WriteLine("CUSTOM JVM: " + JvmArgumentsBox.Text);
        ThrowIfCancelled();

        var instance = new RunningInstance
        {
            Process = process,
            ProfileName = currentProfile.Name,
            Version = currentProfile.Version,
            Loader = currentProfile.Loader,
            Username = currentAccountData?.username ?? "Unkown"
        };
        ConfigureMinecraftProcessLogging(process, instance, gameDir);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        ResetLaunchUI();
        runningInstances.Add(instance);
        if (OpenLogsAfterStartingCheckBox.IsChecked == true)
        {
            ShowLogsWindow(instance);
        }
        runningMinecraftInstances.Add(process);
        UpdateDiscordPresence();
        UpdateInstancesButton();
        currentMinecraftProcess = process;

        StopTask("Preparing launch...");
        ShowToast("Minecraft starting...");
        if (HideLauncherOnLaunchCheckBox.IsChecked == true)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
    catch (OperationCanceledException)
    {
        currentTasks.Clear();
        UpdateTaskUI();

        ResetLaunchUI();

        ShowToast("Launch cancelled");
    }
    catch (Exception ex)
    {
        currentTasks.Clear();
        UpdateTaskUI();

        ResetLaunchUI();

        ShowToast("Launch failed!" + ex);
        StopTask("Preparing launch...");
    }
}
private async void LaunchMinecraft()
{
    
    if (IsOfflineMode)
    {
        LaunchMinecraftOffline();
        return;
    }
    try
    {
        EnsureSupportedProfileLoader(currentProfile);
        string? noRiskModsDirectory = null;
        if (!await RefreshLoginIfNeededAsync(currentAccountData))
        {
            throw new InvalidOperationException(
                "Minecraft account session could not be refreshed. Sign in again.");
        }
        ThrowIfCancelled();
        if (currentProfile?.IsNoRiskProfile == true)
        {
            StartTask("Preparing NoRisk profile mods...");
            noRiskModsDirectory =
                await PrepareNoRiskProfileModsAsync(currentProfile);
            StopTask("Preparing NoRisk profile mods...");
            ThrowIfCancelled();
        }
        if (!currentProfile.IsExternalProfile)
        {
            
            await HandleSpecialVersion(currentProfile.Version);
            ThrowIfCancelled();
            await DownloadNRCFiles();
            ThrowIfCancelled();
            ApplyNRCMods();
        }

        StartTask("Preparing launch...");
        

        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE", "JaxClient");

        string version = currentProfile.Version;
        string loaderVersion = currentProfile.LoaderVersion;
       
       

        try
        {
            Console.WriteLine("=== VERIFY START ===");


            Console.WriteLine("STEP: Ensure Java");
            await DownloadJavaVersions();
            ThrowIfCancelled();
            string metaPath = Path.Combine(basePath, "meta");


            string mcJarPath = Path.Combine(metaPath, "versions", version, $"{version}.jar");

            if (!File.Exists(mcJarPath))
            {
                Console.WriteLine("Missing Minecraft JAR → downloading");

                StartTask("Downloading Minecraft...");
                await DownloadMinecraftClient(version);
                ThrowIfCancelled();
                StopTask("Downloading Minecraft...");
            }
            else
            {
                Console.WriteLine("✅ Minecraft JAR OK");
            }


            string libsPath = Path.Combine(metaPath, "libraries", version);
            string minecraftVersionJsonPath = Path.Combine(
                metaPath,
                "versions",
                version,
                $"{version}.json");

            if (!Directory.Exists(libsPath) ||
                Directory.GetFiles(libsPath, "*", SearchOption.AllDirectories).Length < 100 ||
                !File.Exists(minecraftVersionJsonPath))
            {
                Console.WriteLine("Libraries missing → downloading");

                StartTask("Downloading Libraries...");
                await DownloadLibraries(version);
                ThrowIfCancelled();
                StopTask("Downloading Libraries...");
            }
            else
            {
                Console.WriteLine("✅ Libraries OK");
            }


            if (currentProfile.Loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
            {
                string fabricJsonCheck = Path.Combine(
                    metaPath,
                    "versions",
                    version,
                    $"fabric-loader-{loaderVersion}-{version}.json"
                );

                if (!File.Exists(fabricJsonCheck))
                {
                    StartTask("Downloading Fabric...");

                        await DownloadFabricProfile(version, loaderVersion);
                        ThrowIfCancelled();
                        await DownloadFabricLibraries(version, loaderVersion);
                        ThrowIfCancelled();
                    
                        StopTask("Downloading Fabric...");
                    }
                    using JsonDocument fabricProfile = JsonDocument.Parse(
                        File.ReadAllText(fabricJsonCheck));
                    ValidateFabricProfileMetadata(
                        fabricProfile.RootElement,
                        version,
                        loaderVersion);
            }
            else if (currentProfile.Loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
            {
                
                string forgeJar = Path.Combine(
                    metaPath,
                    "versions",
                    version,
                    $"forge-{loaderVersion}-installer.jar"
                );

                if (!File.Exists(forgeJar))
                {
                    StartTask("Downloading Forge...");

                    await DownloadForgeProfile(version, loaderVersion);
                    ThrowIfCancelled();
                    await DownloadForgeLibraries(version, loaderVersion);
                    ThrowIfCancelled();

                    StopTask("Downloading Forge...");
                }
            }
            else if (currentProfile.Loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                StartTask("Installing NeoForge...");
                bool installed = await DownloadNeoForgeProfile(
                    currentProfile.Version,
                    currentProfile.LoaderVersion);
                ThrowIfCancelled();
                StopTask("Installing NeoForge...");
                if (!installed)
                    throw new IOException(
                        $"NeoForge {currentProfile.LoaderVersion} could not be installed.");
            }
            else if (!currentProfile.Loader.Equals(
                         "Vanilla",
                         StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Unsupported loader: {currentProfile.Loader}");
            }

            Console.WriteLine("=== VERIFY DONE ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ VERIFY ERROR:");
            Console.WriteLine(ex.ToString());

            if (currentProfile?.IsNoRiskProfile == true)
            {
                throw new InvalidOperationException(
                    "NoRisk profile preparation failed; Minecraft was not started.",
                    ex);
            }

            ShowToast("Fix download failed: " + ex.Message);
        }

        

        string javaFolder = "jdk-21";

        try
        {
            using var client = new HttpClient();


            string manifestJson = client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json").Result;
            using var manifestDoc = JsonDocument.Parse(manifestJson);

            string versionUrl = null;

            foreach (var v in manifestDoc.RootElement.GetProperty("versions").EnumerateArray())
            {
                if (v.GetProperty("id").GetString() == version)
                {
                    versionUrl = v.GetProperty("url").GetString();
                    break;
                }
            }

            string versionJson = client.GetStringAsync(versionUrl).Result;
            using var vDoc = JsonDocument.Parse(versionJson);

            int javaVer = 21; // Fallback.

            if (vDoc.RootElement.TryGetProperty("javaVersion", out var javaProp))
            {
                javaVer = javaProp.GetProperty("majorVersion").GetInt32();
            }

            javaFolder = $"jdk-{javaVer}";

            
        }
        catch (Exception ex)
        {
            
            javaFolder = "jdk-21"; // Fallback.
        }


        string javaPath = System.IO.Path.Combine(
            basePath, "meta", "Java", javaFolder, "bin", "java.exe");

        


        try
        {
            var test = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = Process.Start(test);

            string stdOut = p.StandardOutput.ReadToEnd();
            string stdErr = p.StandardError.ReadToEnd();

            p.WaitForExit();

         

            if (javaFolder == "jdk-25" && !stdErr.Contains("\"25"))
            {
                throw new Exception("❌ WRONG JAVA VERSION! Expected 25!");
            }
        }
        catch (Exception ex)
        {
           
        }


        try
        {
            var sys = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p2 = Process.Start(sys);

            string stdOut2 = p2.StandardOutput.ReadToEnd();
            string stdErr2 = p2.StandardError.ReadToEnd();

            p2.WaitForExit();

            
        }
        catch
        {
            
        }

        


        string mainClass;
        string forgeJvmArgs = "";
        string forgeGameArgs = "";
        List<string> classpathList = new List<string>();
        if (currentProfile.Loader == "Fabric")
        {
            string fabricJsonPath = Path.Combine(
                basePath,
                "meta",
                "versions",
                version,
                $"fabric-loader-{loaderVersion}-{version}.json");

            string fabricJson = File.ReadAllText(fabricJsonPath);
            if (!File.Exists(fabricJsonPath))
            {
                ShowToast("Fabric is not installed!");
                StopTask("Preparing launch...");
                return;
            }
            using var doc = JsonDocument.Parse(fabricJson);
            ValidateFabricProfileMetadata(
                doc.RootElement,
                version,
                loaderVersion);

            mainClass =
                doc.RootElement.GetProperty("mainClass").GetString();
        }
        else if (currentProfile.Loader.Equals(
                     "Vanilla",
                     StringComparison.OrdinalIgnoreCase))
        {
            string vanillaJsonPath = Path.Combine(
                basePath,
                "meta",
                "versions",
                version,
                $"{version}.json");
            if (!File.Exists(vanillaJsonPath))
                throw new FileNotFoundException(
                    "Vanilla Minecraft metadata is not installed. Launch this profile once online first.",
                    vanillaJsonPath);
            using JsonDocument doc =
                JsonDocument.Parse(File.ReadAllText(vanillaJsonPath));
            mainClass =
                doc.RootElement.GetProperty("mainClass").GetString();
        }
        else if (currentProfile.Loader.Equals(
                     "NeoForge",
                     StringComparison.OrdinalIgnoreCase))
        {
            NeoForgeLaunchConfiguration neoForge =
                LoadNeoForgeLaunchConfiguration(
                    Path.Combine(basePath, "meta"),
                    version,
                    loaderVersion);
            mainClass = neoForge.MainClass;
            forgeJvmArgs = neoForge.JvmArguments;
            forgeGameArgs = neoForge.GameArguments;
            classpathList.AddRange(neoForge.Libraries);
        }
        else
        {
            string forgeVersion = currentProfile.LoaderVersion;

// Extract the loader version from values such as 26.2-65.1.2.
            if (forgeVersion.StartsWith(currentProfile.Version + "-"))
            {
                forgeVersion =
                    forgeVersion.Substring(
                        currentProfile.Version.Length + 1);
            }

            string wantedForgeJson =
                $"{currentProfile.Version}-forge-{forgeVersion}.json";

            string forgeJson = Directory.GetFiles(
                    Path.Combine(basePath, "Profiles", "ForgeProfiles", "versions"),
                    "*.json",
                    SearchOption.AllDirectories)
                .FirstOrDefault(x =>
                    Path.GetFileName(x)
                        .Equals(
                            wantedForgeJson,
                            StringComparison.OrdinalIgnoreCase));

            if (forgeJson == null)
            {
                throw new Exception(
                    "Forge JSON not found: " +
                    wantedForgeJson);
            }

            Console.WriteLine("Expected Forge JSON:");
            Console.WriteLine(wantedForgeJson);

            Console.WriteLine("Selected Forge JSON:");
            Console.WriteLine(forgeJson);
           
            using var doc = JsonDocument.Parse(
                    File.ReadAllText(forgeJson));
            var root = doc.RootElement;
            string forgeLibraries =
                Path.Combine(
                    basePath,
                    "Profiles",
                    "ForgeProfiles",
                    "libraries");

            foreach (var lib in root.GetProperty("libraries").EnumerateArray())
            {
                if (!lib.TryGetProperty("downloads", out var downloads))
                    continue;

                if (!downloads.TryGetProperty("artifact", out var artifact))
                    continue;

                string rel =
                    artifact.GetProperty("path").GetString();

                string full =
                    Path.Combine(
                        forgeLibraries,
                        rel.Replace('/', '\\'));

               
                if (!File.Exists(full))
                    continue;

                string fileName =
                    Path.GetFileName(full).ToLower();

                if (fileName.Contains("bootstraplauncher"))
                    continue;

                if (fileName.Contains("securejarhandler"))
                    continue;

                classpathList.Add(full);
            }
            var jvmArgs =
                root.GetProperty("arguments")
                    .GetProperty("jvm");

            StringBuilder jvmBuilder =
                new StringBuilder();

          

            foreach (var arg in jvmArgs.EnumerateArray())
            {
                if (arg.ValueKind != JsonValueKind.String)
                    continue;

                string value = arg.GetString();

                value = value.Replace(
                    "${library_directory}",
                    forgeLibraries);

                value = value.Replace(
                    "${classpath_separator}",
                    ";");

                value = value.Replace(
                    "${version_name}",
                    root.GetProperty("id").GetString());

                jvmBuilder.Append(value);
                jvmBuilder.Append(" ");
            }
            var gameArgs =
                root.GetProperty("arguments")
                    .GetProperty("game");

            StringBuilder gameBuilder =
                new StringBuilder();

            foreach (var arg in gameArgs.EnumerateArray())
            {
                gameBuilder.Append(arg.GetString());
                gameBuilder.Append(" ");
            }

            forgeGameArgs = gameBuilder.ToString();
            forgeJvmArgs = jvmBuilder.ToString();

            mainClass =
                doc.RootElement
                    .GetProperty("mainClass")
                    .GetString();

            Console.WriteLine("FORGE JSON = " + forgeJson);
        }


      
        string libsFolder = System.IO.Path.Combine(basePath, "meta", "libraries", version);

        if (Directory.Exists(libsFolder))
        {
            Dictionary<string, string> bestLibs = new();
            Dictionary<string, Version> bestVersions = new();

            foreach (var file in Directory.GetFiles(libsFolder, "*.jar", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);


                if (name.Contains("natives-linux") || name.Contains("natives-macos"))
                    continue;


                if (currentProfile.Loader == "Fabric")
                {
                    if (name.StartsWith("fabric-loader"))
                    {
                        string expected =
                            $"fabric-loader-{loaderVersion}";

                        if (!name.StartsWith(expected))
                            continue;

                        classpathList.Add(file);
                        continue;
                    }
                }
                else if (currentProfile.Loader == "Forge")
                {
                    if (name.Contains("fabric"))
                        continue;

                    if (name.Contains("intermediary"))
                        continue;
                    if (name.StartsWith("asm"))
                        continue;
                    if (name.Contains("forge-"))
                        continue;

                    if (name.Contains("bootstraplauncher"))
                        continue;

                    if (name.Contains("securejarhandler"))
                        continue;

                    if (name.Contains("modlauncher"))
                        continue;
                }


                try
                {
                    int lastDash = name.LastIndexOf("-");
                    if (lastDash <= 0)
                    {
                        classpathList.Add(file);
                        continue;
                    }

                    string libName = name.Substring(0, lastDash);
                    string versionStr = name.Substring(lastDash + 1).Replace(".jar", "");

                    // Remove suffixes such as mixin.
                    versionStr = versionStr.Split('+')[0];

                    if (!Version.TryParse(versionStr, out Version ver))
                        ver = new Version(0, 0);


                    if (!bestVersions.ContainsKey(libName) || ver > bestVersions[libName])
                    {
                        bestVersions[libName] = ver;
                        bestLibs[libName] = file;
                    }
                }
                catch
                {
                    classpathList.Add(file); // Fallback.
                }
            }


            foreach (var lib in bestLibs.Values)
            {
                classpathList.Add(lib);
            }
                      
        }
        
        string mcJar =
            Path.Combine(basePath,
                "meta",
                "versions",
                version,
                $"{version}.jar");

        if (currentProfile.Loader != "Forge")
        {
            classpathList.Add(mcJar);
        }
       
        foreach (var x in classpathList)
        {
            if (x.Contains("mclanguage") ||
                x.Contains("javafmllanguage") ||
                x.Contains("lowcodelanguage"))
            {
                Console.WriteLine("DUPLICATE DEBUG -> " + x);
            }
        }
        string classpath = string.Join(";", classpathList);
        Console.WriteLine("=================================");
        Console.WriteLine("Loader: " + currentProfile.Loader);
        Console.WriteLine("MainClass: " + mainClass);
        Console.WriteLine("Classpath:");
        Console.WriteLine(classpath);
        Console.WriteLine("=================================");

        string nativesPath = System.IO.Path.Combine(
            basePath,
            "meta",
            "natives",
            version + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
        string gameDir = GetProfileDirectory(currentProfile);

        Directory.CreateDirectory(nativesPath);
        if (!currentProfile.IsExternalProfile)
        {
            Directory.CreateDirectory(gameDir);
        }
        ExtractMinecraftNatives(
            Path.Combine(basePath, "meta", "versions", version, $"{version}.json"),
            Path.Combine(basePath, "meta", "libraries", version),
            nativesPath);


        string assetIndex = null;

        using (var client = new HttpClient())
        {
            string manifestJson = client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json").Result;
            using var manifestDoc = JsonDocument.Parse(manifestJson);

            string versionUrl = null;

            foreach (var v in manifestDoc.RootElement.GetProperty("versions").EnumerateArray())
            {
                if (v.GetProperty("id").GetString() == version)
                {
                    versionUrl = v.GetProperty("url").GetString();
                    break;
                }
            }

            string versionJsonRemote = client.GetStringAsync(versionUrl).Result;
            using var vDoc = JsonDocument.Parse(versionJsonRemote);

            assetIndex = vDoc.RootElement.GetProperty("assetIndex").GetProperty("id").GetString();
        }

        

        string assetsRoot = System.IO.Path.Combine(basePath, "meta", "assets");


        string indexPath = System.IO.Path.Combine(assetsRoot, "indexes", $"{assetIndex}.json");

        if (!File.Exists(indexPath))
        {
            
            StartTask("Downloading Assets...");
            DownloadAssets(version).Wait();
            StopTask("Downloading Assets...");
        }
        else
        {
            using var indexDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
            var objects = indexDoc.RootElement.GetProperty("objects");

            using HttpClient client = new HttpClient();

            int fixedCount = 0;

            foreach (var asset in objects.EnumerateObject())
            {
                string hash = asset.Value.GetProperty("hash").GetString();
                string sub = hash.Substring(0, 2);

                string assetPath = System.IO.Path.Combine(assetsRoot, "objects", sub, hash);

                if (File.Exists(assetPath))
                    continue;

                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(assetPath));

                    string url = $"https://resources.download.minecraft.net/{sub}/{hash}";

                    byte[] data = client.GetByteArrayAsync(url).Result;
                    File.WriteAllBytes(assetPath, data);

                    fixedCount++;
                   
                }
                catch (Exception ex)
                {
                   
                }
            }

            
        }


        
        string customJvm =
            JvmArgumentsBox.Text?.Trim() ?? "";

        bool usesMinecraftVersionArguments =
            currentProfile.Loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase);
        string gameArguments = usesMinecraftVersionArguments
            ? BuildMinecraftGameArguments(
                Path.Combine(basePath, "meta", "versions", version, $"{version}.json"),
                version,
                currentAccountData,
                gameDir,
                assetsRoot,
                assetIndex,
                offline: false)
            : forgeGameArgs +
              $"--username \"{currentAccountData.username}\" " +
              $"--uuid {currentAccountData.uuid} " +
              $"--accessToken {currentAccountData.accessToken} " +
              $"--version \"{version}\" " +
              $"--gameDir \"{gameDir}\" " +
              $"--assetsDir \"{assetsRoot}\" " +
              $"--assetIndex \"{assetIndex}\" " +
              "--userType msa";
        string args =
            forgeJvmArgs +
            GetNoRiskModsFolderArgument(
                currentProfile,
                noRiskModsDirectory) +
            $"{customJvm} " +
            $"-Xmx{(int)RamSlider.Value}G " +
            $"-Djava.library.path=\"{nativesPath}\" " +
            $"-cp \"{classpath}\" " +
            $"{mainClass} " +
            gameArguments;

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = System.IO.Path.GetDirectoryName(javaPath)
        };


        psi.EnvironmentVariables["JAVA_HOME"] =
            System.IO.Path.Combine(basePath, "meta", "Java", javaFolder);

        psi.EnvironmentVariables["Path"] =
            System.IO.Path.Combine(basePath, "meta", "Java", javaFolder, "bin");

        Console.WriteLine("===== FORGE DEBUG =====");
        Console.WriteLine("MainClass:");
        Console.WriteLine(mainClass);

        Console.WriteLine("Classpath Count:");
        Console.WriteLine(classpathList.Count);

        foreach (string cp in classpathList)
        {
            Console.WriteLine(cp);
        }
        Console.WriteLine("=======================");
        Process process = new Process();
        process.StartInfo = psi;
        var instance = new RunningInstance
        {
            Process = process,
            ProfileName = currentProfile.Name,
            Version = currentProfile.Version,
            Loader = currentProfile.Loader,
            Username = currentAccountData?.username ?? "Unkown"
        };
        ConfigureMinecraftProcessLogging(process, instance, gameDir);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        runningInstances.Add(instance);
        if (OpenLogsAfterStartingCheckBox.IsChecked == true)
        {
            ShowLogsWindow(instance);
        }
        runningMinecraftInstances.Add(process);
        UpdateDiscordPresence();
        UpdateInstancesButton();

        StopTask("Preparing launch...");
        ShowToast("Minecraft starting...");
        if (HideLauncherOnLaunchCheckBox.IsChecked == true)
        {
            this.WindowState = WindowState.Minimized;
        }
        currentMinecraftProcess = process;
        
        isLaunching = false;

        LaunchBtn.Tag = "LAUNCH";
        ProfileSwitchBtn.Visibility = Visibility.Visible;
        LaunchBtn.Background =
            new SolidColorBrush(Color.FromRgb(37, 99, 235));

        ProfileSwitchBtn.IsEnabled = true;
        ProfilesBtn.IsEnabled = true;
        CreateProfileBtn.IsEnabled = true;
    }
    catch (OperationCanceledException)
    {
        isLaunching = false;

        LaunchBtn.Tag = "LAUNCH";
        ProfileSwitchBtn.Visibility = Visibility.Visible;
        LaunchBtn.Background =
            new SolidColorBrush(Color.FromRgb(37, 99, 235));

        ProfileSwitchBtn.IsEnabled = true;
        ProfilesBtn.IsEnabled = true;
        CreateProfileBtn.IsEnabled = true;

        currentTasks.Clear();
        UpdateTaskUI();

        StopTask("Preparing launch...");
        ShowToast("Launch cancelled");
    }
    catch (Exception ex)
    {
        isLaunching = false;

        LaunchBtn.Tag = "LAUNCH";
        ProfileSwitchBtn.Visibility = Visibility.Visible;
        LaunchBtn.Background =
            new SolidColorBrush(Color.FromRgb(37, 99, 235));

        ProfileSwitchBtn.IsEnabled = true;
        ProfilesBtn.IsEnabled = true;
        CreateProfileBtn.IsEnabled = true;

        ShowToast("Launch failed!" + ex);
        StopTask("Preparing launch...");
    }
}
    private async Task<string> StartLocalServer()
    {
        var listener = new System.Net.HttpListener();
        listener.Prefixes.Add("http://localhost:5000/callback/");
        listener.Start();

        var context = await listener.GetContextAsync();

        var request = context.Request;

        string code = request.QueryString["code"];

        string response = @"
<html>
<head>
    <meta charset='UTF-8'>
    <title>JaxClient Login</title>

    <style>
        body {
            margin: 0;
            height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            background: radial-gradient(circle at center, #1e3a8a, #020617);
            font-family: Segoe UI, Arial;
        }

        .card {
            background: #0f172a;
            padding: 30px;
            border-radius: 16px;
            text-align: center;
            width: 320px;
            box-shadow: 0 0 40px rgba(0,0,0,0.6);
        }

        .title {
            color: white;
            font-size: 22px;
            font-weight: bold;
        }

        .sub {
            color: #94a3b8;
            margin-top: 10px;
            font-size: 14px;
        }
    </style>
</head>

<body>

    <div class='card'>
        <div class='title'>✅ Login Successful</div>
        <div class='sub'>Returning to JaxClient...</div>
    </div>

    <script>
        setTimeout(() => {
            window.location.replace('about:blank');
        }, 3600);
    </script>

</body>
</html>";
            

            

       context.Response.ContentType = "text/html; charset=utf-8";
       
       byte[] buffer = Encoding.UTF8.GetBytes(response);
       await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
       context.Response.OutputStream.Close();

        listener.Stop();

        return code;
    }
    private async Task<OAuthToken> ExchangeCodeForToken(string code, string redirectUri)
    {
        using HttpClient client = new HttpClient();

        var values = new Dictionary<string, string>
        {
            {"client_id", "e16699bb-2aa8-46da-b5e3-45cbcce29091"},
            {"code", code},
            {"grant_type", "authorization_code"},
            {"redirect_uri", redirectUri}
        };

        var content = new FormUrlEncodedContent(values);

        var response = await client.PostAsync(
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
            content
        );

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<OAuthToken>(json);
    }
    private async Task<string> GetXboxToken(string accessToken)
    {
        using HttpClient client = new HttpClient();

        var body = new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={accessToken}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };

        var response = await client.PostAsync(
            "https://user.auth.xboxlive.com/user/authenticate",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        );

        string json = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(json);

        return doc.RootElement.GetProperty("Token").GetString();
    }
    private async Task<(string token, string uhs)> GetXstsToken(string xboxToken)
    {
        using HttpClient client = new HttpClient();

        var body = new
        {
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { xboxToken }
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        };

        var res = await client.PostAsync(
            "https://xsts.auth.xboxlive.com/xsts/authorize",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        );

        string json = await res.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(json);

        string token = doc.RootElement.GetProperty("Token").GetString();
        string uhs = doc.RootElement
            .GetProperty("DisplayClaims")
            .GetProperty("xui")[0]
            .GetProperty("uhs")
            .GetString();

        return (token, uhs);
    }
    private async Task<string> GetMinecraftToken(string xstsToken, string uhs)
    {
        using HttpClient client = new HttpClient();

        var body = new
        {
            identityToken = $"XBL3.0 x={uhs};{xstsToken}"
        };

        var res = await client.PostAsync(
            "https://api.minecraftservices.com/authentication/login_with_xbox",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        );

        string json = await res.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(json);

        return doc.RootElement.GetProperty("access_token").GetString();
    }
    private async Task<(string name, string uuid)> GetProfile(string mcToken)
    {
        using HttpClient client = new HttpClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcToken);

        var res = await client.GetAsync("https://api.minecraftservices.com/minecraft/profile");

        string json = await res.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(json);

        string name = doc.RootElement.GetProperty("name").GetString();
        string id = doc.RootElement.GetProperty("id").GetString();

        return (name, id);
    }
    
    public class OAuthToken
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
    }
    private AccountData currentAccountData;

    private void LoadAccounts()
    {
        string taskName = "Load Accounts...";
        StartTask(taskName);

        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "accounts.json"
            );

            if (!File.Exists(path))
                return;

            var data = JsonSerializer.Deserialize<AccountFile>(File.ReadAllText(path));

            if (data?.accounts == null || data.accounts.Count == 0)
                return;

            // Select the most recently used account.
            var acc = data.accounts.Last();

            currentAccountData = acc;


            AccountNameText.Text = acc.username;
            LoadAccountHead(acc.username);
            LoadSkinLocal();
            UpdateWelcomeText();

            
        }
        catch
        {
            ShowToast("Failed to load account");
        }
        finally
        {
            StopTask(taskName);
        }
    }
    
    
    private void SaveAccount(
        string username,
        string uuid,
        string token,
        string refreshToken,
        string selectedSkin,
        DateTimeOffset? lastTokenRefreshUtc)
    
    {
        string path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "accounts.json"
        );

        List<AccountData> accounts;


        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);

            var data = JsonSerializer.Deserialize<AccountFile>(json);

            accounts = data?.accounts ?? new List<AccountData>();
        }
        else
        {
            accounts = new List<AccountData>();
        }

        // Avoid adding a duplicate account.
        accounts.RemoveAll(a => a.uuid == uuid);


        accounts.Add(new AccountData
        {
            username = username,
            uuid = uuid,
            accessToken = token,
            refreshToken = refreshToken,
            selectedSkin = selectedSkin,
            lastTokenRefreshUtc = lastTokenRefreshUtc
        });

        var output = new AccountFile
        {
            accounts = accounts
        };

        File.WriteAllText(path,
            JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true })
        );
    }   
    private void ResetLaunchUI()
    {
        isLaunching = false;


        LaunchBtn.Tag = "LAUNCH";

        LaunchBtn.Background =
            new SolidColorBrush(Color.FromRgb(37, 99, 235));

        ProfileSwitchBtn.Visibility = Visibility.Visible;

        ProfileSwitchBtn.IsEnabled = true;
        ProfilesBtn.IsEnabled = true;
        CreateProfileBtn.IsEnabled = true;

        StopTask("Preparing launch...");
    }
    private async Task<bool> RefreshLoginIfNeededAsync(AccountData account)
    {
        await tokenRefreshSemaphore.WaitAsync();
        try
        {
            if (!IsTokenRefreshDue(account))
                return true;

            return await RefreshLoginCoreAsync(account);
        }
        finally
        {
            tokenRefreshSemaphore.Release();
        }
    }

    private static bool IsTokenRefreshDue(AccountData account)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return account.lastTokenRefreshUtc == null ||
               account.lastTokenRefreshUtc > now ||
               now - account.lastTokenRefreshUtc.Value >= TimeSpan.FromHours(24);
    }

    private async Task<bool> RefreshLoginCoreAsync(AccountData account)
    {
        try
        {
            using HttpClient client = new();
            var values = new Dictionary<string, string>
            {
                {"client_id", "e16699bb-2aa8-46da-b5e3-45cbcce29091"},
                {"refresh_token", account.refreshToken},
                {"grant_type", "refresh_token"}
            };

            using HttpResponseMessage response = await client.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                new FormUrlEncodedContent(values));
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            OAuthToken? token = JsonSerializer.Deserialize<OAuthToken>(json);
            if (token == null || string.IsNullOrWhiteSpace(token.access_token))
                return false;

            string xbox = await GetXboxToken(token.access_token);
            var xsts = await GetXstsToken(xbox);
            string minecraftToken = await GetMinecraftToken(xsts.token, xsts.uhs);

            account.accessToken = minecraftToken;
            if (!string.IsNullOrWhiteSpace(token.refresh_token))
                account.refreshToken = token.refresh_token;
            account.lastTokenRefreshUtc = DateTimeOffset.UtcNow;

            SaveAccountsToFile(account);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Account token refresh failed for {account.username}: {ex.Message}");
            return false;
        }
    }

    private void StartTokenRefreshMonitor()
    {
        tokenRefreshTimer.Interval = TimeSpan.FromMinutes(5);
        tokenRefreshTimer.Tick += async (_, _) =>
            await RefreshCurrentAccountTokenIfNeededAsync();
        tokenRefreshTimer.Start();

        Dispatcher.BeginInvoke(new Action(() =>
            _ = RefreshCurrentAccountTokenIfNeededAsync()));
    }

    private async Task RefreshCurrentAccountTokenIfNeededAsync()
    {
        AccountData? account = currentAccountData;
        if (IsOfflineMode ||
            OfflineModeCheckBox.IsChecked == true ||
            account == null ||
            string.IsNullOrWhiteSpace(account.refreshToken) ||
            !IsTokenRefreshDue(account))
        {
            return;
        }

        const string taskName = "Refreshing session...";
        StartTask(taskName);
        try
        {
            if (!await RefreshLoginIfNeededAsync(account))
                ShowToast("Could not refresh account session.");
        }
        finally
        {
            StopTask(taskName);
        }
    }

    private void SaveAccountsToFile(AccountData? accountToSave = null)
    {
        AccountData? account = accountToSave ?? currentAccountData;
        if (account == null)
            return;

        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "accounts.json"
        );

        List<AccountData> accounts = new List<AccountData>();

        if (File.Exists(path))
        {
            var existing = JsonSerializer.Deserialize<AccountFile>(File.ReadAllText(path));
            if (existing?.accounts != null)
                accounts = existing.accounts;
        }

        accounts.RemoveAll(a =>
            string.Equals(a.uuid, account.uuid, StringComparison.OrdinalIgnoreCase));
        accounts.Add(account);

        var data = new AccountFile
        {
            accounts = accounts
        };

        File.WriteAllText(path,
            JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })
        );
    }    public class AccountFile
    {
        public List<AccountData> accounts { get; set; }
    }

    public class AccountData
    {
        public string username { get; set; }
        public string uuid { get; set; }
        public string accessToken { get; set; }
        public string refreshToken { get; set; }

        public string selectedSkin { get; set; }
        public DateTimeOffset? lastTokenRefreshUtc { get; set; }
    }
    public class ModrinthFile
    {
        public string url { get; set; }
        public string filename { get; set; }
        public bool primary { get; set; }
    }

    public class ModrinthResponse
    {
        public List<ModrinthProject> hits { get; set; }
    }

    public class ModrinthProject
    {
        public string id { get; set; }
        public string slug { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string project_id { get; set; }
        public string icon_url { get; set; }
    }
    
    private async void OpenContentBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (IsOfflineMode)
        {
            ShowToast("Content Browser requires internet.");
            return;
        }
        ProfileDetailPage.Visibility = Visibility.Collapsed;
        ContentBrowserPage.Visibility = Visibility.Visible;

        // Reset the tab to Mods.
        currentTab = "mod";

        CB_ModsTab.Foreground = Brushes.White;
        CB_RPacksTab.Foreground = Brushes.Gray;
        CB_ShadersTab.Foreground = Brushes.Gray;

        CB_ModsLine.Visibility = Visibility.Visible;
        CB_RPacksLine.Visibility = Visibility.Collapsed;
        CB_ShadersLine.Visibility = Visibility.Collapsed;

        ContentList.Children.Clear();

        await LoadContent();
    }
    private NoriskAccount currentAccount;

    
    
    
    public class NoriskData
    {
        public List<NoriskAccount> accounts { get; set; }
    }

    public class NoriskAccount
    {
        public string username { get; set; }
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public bool active { get; set; }
    }
    public class VersionInfo
    {
        public string version { get; set; }
    }
    private void LoadLauncherVersion()
    {
        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "version.json"
            );

            if (!File.Exists(path))
            {
                CurrentVersionText.Text = "Unknown";
                return;
            }

            string json = File.ReadAllText(path);

            VersionInfo info =
                JsonSerializer.Deserialize<VersionInfo>(json);

            CurrentVersionText.Text =
                $"v{info.version}";
        }
        catch
        {
            CurrentVersionText.Text = "Error";
        }
    }
    private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string search = SettingsSearchBox.Text.ToLower().Trim();
    
        if (string.IsNullOrWhiteSpace(search))
            return;
    
        if (search.Contains("launcher") ||
            search.Contains("version") ||
            search.Contains("logs"))
        {
            LauncherSettingsBtn.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent));
        }
    
        else if (search.Contains("minecraft") ||
                 search.Contains("ram") ||
                 search.Contains("memory") ||
                 search.Contains("jvm") ||
                 search.Contains("content"))
        {
            MinecraftSettingsBtn.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent));
        }
    
        else if (search.Contains("storage") ||
                 search.Contains("folder") ||
                 search.Contains("cache") ||
                 search.Contains("profile"))
        {
            StorageSettingsBtn.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent));
        }
    
        else if (search.Contains("advanced") ||
                 search.Contains("reset") ||
                 search.Contains("danger") ||
                    search.Contains("offline"))
        {
            AdvancedSettingsBtn.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent));
        }
    }
    private void SettingsTab_Click(object sender, RoutedEventArgs e)
    {
        LauncherSettingsBtn.Background = Brushes.Transparent;
        MinecraftSettingsBtn.Background = Brushes.Transparent;
        StorageSettingsBtn.Background = Brushes.Transparent;
        AdvancedSettingsBtn.Background = Brushes.Transparent;

        LauncherSettingsBtn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        MinecraftSettingsBtn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        StorageSettingsBtn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        AdvancedSettingsBtn.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));

        Button btn = (Button)sender;

        btn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        btn.Foreground = Brushes.White;

        LauncherPanel.Visibility = Visibility.Collapsed;
        MinecraftPanel.Visibility = Visibility.Collapsed;
        StoragePanel.Visibility = Visibility.Collapsed;
        AdvancedPanel.Visibility = Visibility.Collapsed;

        if (btn == LauncherSettingsBtn)
            LauncherPanel.Visibility = Visibility.Visible;

        if (btn == MinecraftSettingsBtn)
            MinecraftPanel.Visibility = Visibility.Visible;

        if (btn == StorageSettingsBtn)
            StoragePanel.Visibility = Visibility.Visible;
        

        if (btn == AdvancedSettingsBtn)
            AdvancedPanel.Visibility = Visibility.Visible;
    }    
   
    public class ContentItem
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public string IconUrl { get; set; }
        public string VersionProvider { get; set; }
        public string ProjectId { get; set; }
        public string VersionId { get; set; }
        public string VersionName { get; set; }
        public string VersionFileName { get; set; }
        public string MarketplaceName { get; set; }
        public bool Enabled { get; set; } = true;
        public string? ModrinthRelativePath { get; set; }
    }
 
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void LoadLastProfile()
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        string dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "data.json"
        );

        if (!File.Exists(filePath))
            return;

        var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));

        if (profiles == null || profiles.Count == 0)
            return;

        Profile selected = null;

        if (File.Exists(dataPath))
        {
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(dataPath));

                if (data != null && data.TryGetValue(
                        "lastProfileId",
                        out string lastProfileId))
                {
                    selected = profiles.FirstOrDefault(p =>
                        string.Equals(
                            p.ModrinthInstanceId,
                            lastProfileId,
                            StringComparison.Ordinal));
                }

                if (selected == null &&
                    data != null &&
                    data.TryGetValue(
                        "lastNoRiskProfileId",
                        out string lastNoRiskProfileId))
                {
                    selected = profiles.FirstOrDefault(p =>
                        string.Equals(
                            p.NoRiskProfileId,
                            lastNoRiskProfileId,
                            StringComparison.Ordinal));
                }

                if (selected == null &&
                    data != null &&
                    data.ContainsKey("lastProfile"))
                {
                    string lastName = data["lastProfile"];


                    selected = profiles.FirstOrDefault(p =>
                        p.Name.Equals(lastName, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }
        }


        if (selected == null)
        {
            selected = profiles.Last();
            currentProfile = selected;
        }

        currentProfile = selected;
        SaveLastProfile(selected.Name);

        Dispatcher.InvokeAsync(() =>
        {
            LaunchBtn.ApplyTemplate();
            var txt = LaunchBtn.Template.FindName("SelectedProfileText", LaunchBtn) as TextBlock;

            if (txt != null)
            {
                txt.Text = $"{selected.Name} ({selected.Loader} {selected.Version})" +
                    (selected.IsModrinthProfile
                        ? " · Modrinth profile"
                        : selected.IsNoRiskProfile ? " · NoRisk profile" : "");
            }
        });
    }
    
    private void LoadAccountsUI()
{
    AccountsList.Children.Clear();

    string path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "accounts.json"
    );

    if (!File.Exists(path))
        return;

    var data = JsonSerializer.Deserialize<AccountFile>(File.ReadAllText(path));

    if (data?.accounts == null)
        return;

    foreach (var acc in data.accounts.AsEnumerable().Reverse())
        
    {

        Grid grid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 10)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });


        Button btn = new Button
        {
            Content = acc.username,
            Style = TryFindResource("AccountButtonStyle") as Style
        };

        // Highlight the active account.
        if (acc.uuid == currentAccountData?.uuid)
        {
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
        }

        btn.Click += (s, e) =>
        {
            SelectAccount(acc);
            LoadSkinLocal();
            UpdateWelcomeText();
            
        };

        Grid.SetColumn(btn, 0);
        grid.Children.Add(btn);

        // Delete button.
        Button deleteBtn = new Button
        {
            Content = "🗑",
            Width = 30,
            Height = 30,
            Background = Brushes.Transparent,
            Foreground = Brushes.Gray,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };


        deleteBtn.Template = new ControlTemplate(typeof(Button))
        {
            VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
        };

        deleteBtn.MouseEnter += (s, e) =>
        {
            deleteBtn.Foreground = Brushes.Red;
        };

        deleteBtn.MouseLeave += (s, e) =>
        {
            deleteBtn.Foreground = Brushes.Gray;
        };

        deleteBtn.Click += (s, e) =>
        {
            DeleteAccount(acc);
        };

        Grid.SetColumn(deleteBtn, 1);
        grid.Children.Add(deleteBtn);

        AccountsList.Children.Add(grid);
    }
}
private void DeleteAccount(AccountData acc)
{
    string path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "accounts.json"
    );

    if (!File.Exists(path))
        return;

    var data = JsonSerializer.Deserialize<AccountFile>(File.ReadAllText(path));

    if (data?.accounts == null)
        return;


    data.accounts.RemoveAll(a => a.uuid == acc.uuid);

    File.WriteAllText(path,
        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })
    );


    if (currentAccountData?.uuid == acc.uuid)
    {

        if (!string.IsNullOrEmpty(acc.username))
        {
            string skinFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource",
                "mp",
                $"{acc.username}.png");

            if (File.Exists(skinFile))
                File.Delete(skinFile);
        }


        if (File.Exists(_skinsJson))
        {
            var skins =
                JsonSerializer.Deserialize<List<SavedSkin>>(
                    File.ReadAllText(_skinsJson))
                ?? new();

            skins.RemoveAll(x =>
                x.Name.Equals(
                    acc.username,
                    StringComparison.OrdinalIgnoreCase));

            File.WriteAllText(
                _skinsJson,
                JsonSerializer.Serialize(
                    skins,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }

        currentAccountData = null;

        AccountNameText.Text =
            LocalizationManager.Instance.Translate("Not logged in");

        AccountHeadImage.Source = null;
        AccountImage.Source = null;
        LoadSavedSkins();
        LoadSkinLocal();
        UpdateWelcomeText();
    }    ShowToast($"Removed {acc.username}");

    LoadAccountsUI();
}

private async Task DownloadSkinHead(string username, string uuid)
{
    StartTask("Downloading Skin...");

    try
    {
        using HttpClient client = new HttpClient();


        string url = $"https://api.mineatar.io/head/{uuid}?scale=8&overlay=true";

        byte[] data = await client.GetByteArrayAsync(url);

        if (data == null || data.Length < 500)
            throw new Exception("Invalid image");

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource"
        );

        Directory.CreateDirectory(folder);

        // Keep the previous name.
        string filePath = Path.Combine(folder, $"{username.ToLower()}head.png");

        await File.WriteAllBytesAsync(filePath, data);

        StopTask("Downloading Skin...");
    }
    catch (Exception ex)
    {
        StopTask("Downloading Skin...");
        ShowToast("Failed to download head");
        Console.WriteLine(ex);
    }
}
private async void SelectAccount(AccountData acc)
{
    currentAccountData = acc;

    LoadSavedSkins();
    
    AccountNameText.Text = acc.username;
    SaveLastAccount(acc.username);

    
    LoadSkinLocal();

    LoadAccountHead(acc.username);

    ShowToast($"Switched to {acc.username}");
    _ = RefreshCurrentAccountTokenIfNeededAsync();
}
private void LoadAccountHead(string username)
{
    string basePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "resource");

    string headPath = Path.Combine(
        basePath,
        $"{username.ToLower()}head.png");


    if (currentAccountData != null &&
        !string.IsNullOrEmpty(currentAccountData.selectedSkin))
    {
        string skinPath = Path.Combine(
            basePath,
            "mp",
            currentAccountData.selectedSkin);

       
        if (File.Exists(skinPath))
        {
            
            try
            {
               
                using var skin =
                    SKBitmap.Decode(skinPath);

                using var headImage =
                    Skin3DHeadTypeA.MakeHeadImage(skin);

                using var data =
                    headImage.Encode(
                        SKEncodedImageFormat.Png,
                        100);

                File.WriteAllBytes(
                    headPath,
                    data.ToArray());
               
            }
            catch
            {
            }
        }
    }

    if (!File.Exists(headPath))
    {
        headPath = null;
        

    }


    
    
    BitmapImage img;

    using (var fs = new FileStream(
               headPath,
               FileMode.Open,
               FileAccess.Read,
               FileShare.ReadWrite))
    {
        img = new BitmapImage();

        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        
        img.StreamSource = fs;
        img.EndInit();

        img.Freeze();
    }

    AccountHeadImage.Source = null;
    AccountImage.Source = null;
    
    AccountHeadImage.Source = img;
    AccountImage.Source = img;
    
    UpdateOfflineIcon();

    
}
private async void EditProfile_Click(object sender, RoutedEventArgs e)
{
    if (currentProfile?.IsExternalProfile == true)
    {
        ShowToast("External profiles are managed by their source launcher.");
        return;
    }

    if (IsOfflineMode)
        
    {
        
        ShowToast("You must be online to edit profiles.");
        
        return;
        
    }
    if (currentProfile == null) return;

    editNrcEnabled = currentProfile.NrcEnabled;

    if (editNrcEnabled)
    {
        EditNRC_Toggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
        EditNRC_Knob.HorizontalAlignment = HorizontalAlignment.Right;
    }
    else
    {
        EditNRC_Toggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"));
        EditNRC_Knob.HorizontalAlignment = HorizontalAlignment.Left;
    }
    editProfileInitializing = true;
    EditNameBox.Text = currentProfile.Name;
    SetComboBoxValue(EditLoaderBox, currentProfile.Loader);
    SetComboBoxValue(EditVersionBox, currentProfile.Version);
    editProfileInitializing = false;
    EditProfileOverlay.Visibility = Visibility.Visible;

    if (currentProfile.Loader.Equals(
            "Vanilla",
            StringComparison.OrdinalIgnoreCase))
    {
        EditLoaderVersionBox.Items.Clear();
        EditLoaderVersionBox.IsEnabled = false;
    }
    else
    {
        await LoadLoaderVersionsAsync(
            EditLoaderVersionBox,
            currentProfile.Loader,
            currentProfile.Version,
            currentProfile.LoaderVersion);
    }
}
private void SetComboBoxValue(ComboBox box, string value)
{
    foreach (ComboBoxItem item in box.Items)
    {
        if (item.Content.ToString() == value)
        {
            box.SelectedItem = item;
            return;
        }
    }
}
private void CloseEditProfile_Click(object sender, RoutedEventArgs e)
{
    EditProfileOverlay.Visibility = Visibility.Collapsed;


    EditNameBox.Text = "";
    EditLoaderBox.SelectedIndex = -1;
    EditLoaderVersionBox.Items.Clear();
    EditVersionBox.SelectedIndex = -1;
}

private string? ChooseOptionsSourcePath()
{
    (Window dialog, StackPanel content) =
        CreatePackPopupWindow(
            "Import options",
            "Choose where to load the options.txt from.",
            470,
            300);

    Button jaxClientButton = CreatePackPopupActionButton(
        "JaxClient Profiles",
        "#2563EB");
    jaxClientButton.Height = 54;
    jaxClientButton.Margin = new Thickness(0, 0, 0, 12);
    jaxClientButton.Click += (_, _) =>
    {
        dialog.Tag = "jaxclient";
        dialog.Close();
    };
    content.Children.Add(jaxClientButton);

    Button otherProfileButton = CreatePackPopupActionButton(
        "Other Profiles",
        "#1E293B");
    otherProfileButton.Height = 54;
    otherProfileButton.Click += (_, _) =>
    {
        dialog.Tag = "other";
        dialog.Close();
    };
    content.Children.Add(otherProfileButton);
    dialog.ShowDialog();

    return (dialog.Tag as string) switch
    {
        "jaxclient" => SelectJaxClientOptionsProfile(),
        "other" => BrowseForOptionsFile(),
        _ => null
    };
}

private string? SelectJaxClientOptionsProfile()
{
    string profilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "profiles.json");

    if (!File.Exists(profilesPath))
    {
        ShowToast("No JaxClient profiles found.");
        return null;
    }

    List<Profile> profiles;
    try
    {
        profiles = JsonSerializer.Deserialize<List<Profile>>(
            File.ReadAllText(profilesPath)) ?? new List<Profile>();
    }
    catch (Exception ex) when (
        ex is IOException or UnauthorizedAccessException or JsonException)
    {
        ShowToast($"Could not read JaxClient profiles: {ex.Message}");
        return null;
    }

    List<Profile> profilesWithOptions = profiles
        .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
        .Where(profile => File.Exists(GetProfileOptionsPath(profile)))
        .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    if (profilesWithOptions.Count == 0)
    {
        ShowToast("No JaxClient profile has an options.txt file yet.");
        return null;
    }

    (Window dialog, StackPanel content) =
        CreatePackPopupWindow(
            "JaxClient profiles",
            "Select a profile that has an options.txt file.",
            500,
            500);

    ScrollViewer profileScroll = new()
    {
        MaxHeight = 330,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
    StackPanel profileList = new();
    profileScroll.Content = profileList;

    foreach (Profile profile in profilesWithOptions)
    {
        Button profileButton = CreatePackPopupActionButton(
            profile.Name,
            "#1E293B");
        profileButton.Height = 48;
        profileButton.Margin = new Thickness(0, 0, 0, 8);
        profileButton.Click += (_, _) =>
        {
            dialog.Tag = GetProfileOptionsPath(profile);
            dialog.Close();
        };
        profileList.Children.Add(profileButton);
    }

    content.Children.Add(profileScroll);
    dialog.ShowDialog();
    return dialog.Tag as string;
}

private string? BrowseForOptionsFile()
{
    OpenFileDialog dialog = new()
    {
        Title = "Select options.txt",
        Filter = "options.txt|options.txt|Text files (*.txt)|*.txt",
        CheckFileExists = true,
        Multiselect = false
    };

    if (dialog.ShowDialog(this) != true)
        return null;

    if (!Path.GetFileName(dialog.FileName).Equals(
            "options.txt",
            StringComparison.OrdinalIgnoreCase))
    {
        ShowToast("Please select a file named options.txt.");
        return null;
    }

    return dialog.FileName;
}

private string GetProfileOptionsPath(Profile profile)
{
    return Path.Combine(GetProfileDirectory(profile), "options.txt");
}

private void ImportProfileOptions_Click(object sender, RoutedEventArgs e)
{
    if (currentProfile == null)
    {
        ShowToast("Select a profile first.");
        return;
    }

    string? sourcePath = ChooseOptionsSourcePath();
    if (sourcePath == null)
        return;

    string targetPath = GetProfileOptionsPath(currentProfile);
    try
    {
        if (string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(targetPath),
                StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("This profile already uses that options.txt.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
        ShowToast($"Options imported into {currentProfile.Name}.");
    }
    catch (Exception ex) when (
        ex is IOException or UnauthorizedAccessException or ArgumentException)
    {
        ShowToast($"Could not import options.txt: {ex.Message}");
    }
}

private void SetStandardOptions_Click(object sender, RoutedEventArgs e)
{
    string? sourcePath = ChooseOptionsSourcePath();
    if (sourcePath == null)
        return;

    try
    {
        LauncherSettings settings = LoadSettingsObject();
        settings.StandardOptionsContent = File.ReadAllText(sourcePath);

        if (SaveSettingsObject(settings))
            ShowToast("Standard options saved for new profiles.");
    }
    catch (Exception ex) when (
        ex is IOException or UnauthorizedAccessException)
    {
        ShowToast($"Could not read options.txt: {ex.Message}");
    }
}

private async Task<string> GetIconForInstalledContent(string name)
{
    List<string> searchNames =
        GetImportedModSearchNames(name);

    if (searchNames.Count == 0)
        return null;

    string cacheKey = searchNames[0];

    if (iconCache.TryGetValue(cacheKey, out string cached))
        return string.IsNullOrWhiteSpace(cached) ? null : cached;

    using HttpClient client = new();
    bool searchSucceeded = false;

    foreach (string searchName in searchNames)
    {
        try
        {
            string url =
                $"https://api.modrinth.com/v2/search?limit=20" +
                $"&query={Uri.EscapeDataString(searchName)}" +
                $"&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

            string json =
                await client.GetStringAsync(url);

            ModrinthResponse result =
                JsonSerializer.Deserialize<ModrinthResponse>(json);

            searchSucceeded = true;

            string normalizedSearchName =
                NormalizeCompareName(searchName);

            ModrinthProject match =
                result?.hits?.FirstOrDefault(project =>
                    NormalizeCompareName(project.title) ==
                        normalizedSearchName ||
                    NormalizeCompareName(project.slug) ==
                        normalizedSearchName);

            if (!string.IsNullOrWhiteSpace(match?.icon_url))
            {
                iconCache[cacheKey] = match.icon_url;
                return match.icon_url;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine(
                $"Could not load icon metadata for {name} using '{searchName}': {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine(
                $"Icon metadata search timed out for {name} using '{searchName}': {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"Could not parse icon metadata for {name} using '{searchName}': {ex.Message}");
        }
    }

    if (searchSucceeded)
        iconCache[cacheKey] = "";

    return null;
}

private async void SaveProfileEdit_Click(object sender, RoutedEventArgs e)
{
    await SaveProfileEditAsync(reinstallProfile: false);
}

private async void ReinstallProfile_Click(object sender, RoutedEventArgs e)
{
    object originalContent = null;
    if (sender is Button button)
    {
        originalContent = button.Content;
        button.Content =
            LocalizationManager.Instance.Translate("Reinstalling...");
        button.IsEnabled = false;
    }
    EditProfileOverlay.IsEnabled = false;

    try
    {
        await SaveProfileEditAsync(reinstallProfile: true);
    }
    finally
    {
        EditProfileOverlay.IsEnabled = true;
        if (sender is Button reinstallButton)
        {
            reinstallButton.Content = originalContent;
            reinstallButton.IsEnabled = true;
        }
    }
}

private async Task SaveProfileEditAsync(bool reinstallProfile)
{
    if (currentProfile == null || !File.Exists(filePath))
    {
        ShowToast(
            LocalizationManager.Instance.Translate("Profile not found."));
        return;
    }

    if (currentProfile.IsExternalProfile)
    {
        ShowToast("External profiles are managed by their source launcher.");
        return;
    }

    if (reinstallProfile &&
        (IsOfflineMode ||
         isLaunching ||
         runningInstances.Any(instance =>
             string.Equals(
                 instance.ProfileName,
                 currentProfile.Name,
                 StringComparison.OrdinalIgnoreCase))))
    {
        string reason = IsOfflineMode
            ? "Profile reinstall requires an internet connection."
            : isLaunching
                ? "Wait until the current launch has finished before reinstalling."
                : "Close the running Minecraft instance before reinstalling this profile.";
        ShowToast(
            LocalizationManager.Instance.Translate(reason));
        return;
    }

    List<Profile> profiles =
        JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath))
        ?? new List<Profile>();
    Profile profile = profiles.FirstOrDefault(
        p => string.Equals(
            p.Name,
            currentProfile.Name,
            StringComparison.OrdinalIgnoreCase));

    if (profile != null)
    {
        string oldName = profile.Name;

        string newName = EditNameBox.Text?.Trim();
        string loader =
            (EditLoaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? string.Empty;
        string loaderVersion =
            loader?.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) == true
                ? ""
                : (EditLoaderVersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                    ?? string.Empty;
        string gameVersion =
            (EditVersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? string.Empty;


        if (string.IsNullOrWhiteSpace(newName))
        {
            ShowToast("Name cannot be empty!");
            return;
        }

        if (string.IsNullOrWhiteSpace(loader) ||
            (!loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrWhiteSpace(loaderVersion)) ||
            string.IsNullOrWhiteSpace(gameVersion) ||
            loaderVersion is "Loading..." or "Error loading" or "No versions found")
        {
            ShowToast(
                LocalizationManager.Instance.Translate(
                    "Select a valid Minecraft and loader version."));
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowToast("Invalid profile name!");
            return;
        }


        bool nameExists = profiles.Any(p => 
            p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
            p != profile
        );

        if (nameExists)
        {
            ShowToast("Profile name already exists!");
            return;
        }

        bool nrcDeleteFailed = false;

        try
        {
            if (profile.NrcEnabled && !editNrcEnabled)
            {
                string profilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".JAXSTORE", "JaxClient", "Profiles",
                    oldName
                );

                if (Directory.Exists(profilePath))
                {
                    var files = Directory.GetFiles(profilePath, "*.jar", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        string name = Path.GetFileName(file).ToLower();

                        if (name.Contains("nrc"))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception)
                            {
                                ShowToast("Failed to delete: " + name);
                                nrcDeleteFailed = true;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast("NRC cleanup error: " + ex.Message);
            nrcDeleteFailed = true;
        }


        if (nrcDeleteFailed)
        {
            editNrcEnabled = true;
            profile.NrcEnabled = true;


        }
        else
        {
            profile.NrcEnabled = editNrcEnabled;
        }


        profile.Name = newName;
        profile.Loader = loader;
        profile.LoaderVersion = loaderVersion;
        profile.Version = gameVersion;
        profile.NrcEnabled = editNrcEnabled;

        

        try
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE", "JaxClient", "Profiles"
            );

            string oldPath = Path.Combine(basePath, oldName);
            string newPath = Path.Combine(basePath, newName);

            if (Directory.Exists(oldPath) && oldName != newName)
            {

                if (Directory.Exists(newPath))
                {
                    ShowToast("Profile folder already exists!");
                    return;
                }


                string tempPath = oldPath + "_temp";

                Directory.Move(oldPath, tempPath);
                Directory.Move(tempPath, newPath);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Rename error: " + ex.Message);
            return;
        }

        try
        {
            File.WriteAllText(
                filePath,
                JsonSerializer.Serialize(
                    profiles,
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not save profile edits: {ex}");
            ShowToast(
                LocalizationManager.Instance.Translate(
                    "Could not save profile changes."));
            return;
        }

        currentProfile = profile;
        if (reinstallProfile)
        {
            try
            {
                await ReinstallProfileAsync(profile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profile reinstall failed: {ex}");
                MessageBox.Show(
                    LocalizationManager.Instance.Format(
                        "Profile reinstall failed: {0}",
                        ex.Message),
                    LocalizationManager.Instance.Translate(
                        "Reinstall profile"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        EditProfileOverlay.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Collapsed;
        ProfilesPage.Visibility = Visibility.Visible;
        LoadLastProfile();
        LoadProfiles();
    }
    else
    {
        ShowToast(
            LocalizationManager.Instance.Translate("Profile not found."));
    }
}

private async Task ReinstallProfileAsync(Profile profile)
{
    if (profile == null)
        throw new ArgumentNullException(nameof(profile));

    if (runningInstances.Any(instance =>
            string.Equals(
                instance.ProfileName,
                profile.Name,
                StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            "Close the running Minecraft instance before reinstalling this profile.");
    }

    const string taskName = "Reinstalling profile...";
    StartTask(taskName);
    try
    {
        await ReinstallProfileCoreAsync(profile);
    }
    finally
    {
        StopTask(taskName);
    }
}

private async Task RunReinstallTaskAsync(
    string taskName,
    Func<Task> operation)
{
    StartTask(taskName);
    try
    {
        await operation();
    }
    finally
    {
        StopTask(taskName);
    }
}

private async Task ReinstallProfileCoreAsync(Profile profile)
{
    string root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "meta");
    string vanillaJar = Path.Combine(
        root,
        "versions",
        profile.Version,
        $"{profile.Version}.jar");

    await RunReinstallTaskAsync(
        "Reinstalling Minecraft...",
        async () =>
    {
        if (!await DownloadMinecraftClient(
                profile.Version,
                forceDownload: true))
        {
            throw new IOException(
                $"Minecraft {profile.Version} could not be reinstalled.");
        }
        if (!File.Exists(vanillaJar) ||
            new FileInfo(vanillaJar).Length == 0)
        {
            throw new IOException(
                $"Minecraft {profile.Version} could not be installed.");
        }
    });

    await RunReinstallTaskAsync(
        "Reinstalling Minecraft libraries...",
        async () =>
    {
        if (!await DownloadLibraries(
                profile.Version,
                forceDownload: true))
        {
            throw new IOException(
                $"Minecraft libraries for {profile.Version} could not be reinstalled.");
        }
    });

    await RunReinstallTaskAsync(
        "Reinstalling loader...",
        async () =>
        {
            if (profile.Loader.Equals(
                    "Fabric",
                    StringComparison.OrdinalIgnoreCase))
            {
                await DownloadFabricProfile(
                    profile.Version,
                    profile.LoaderVersion);
                string loaderDirectory = Path.Combine(
                    root,
                    "versions",
                    profile.Version);
                string loaderJson = Path.Combine(
                    loaderDirectory,
                    $"fabric-loader-{profile.LoaderVersion}-{profile.Version}.json");
                string loaderJar = Path.Combine(
                    loaderDirectory,
                    $"fabric-loader-{profile.LoaderVersion}.jar");

                if (!File.Exists(loaderJson) ||
                    !File.Exists(loaderJar) ||
                    new FileInfo(loaderJson).Length == 0 ||
                    new FileInfo(loaderJar).Length == 0)
                {
                    throw new IOException(
                        $"Fabric {profile.LoaderVersion} could not be installed for Minecraft {profile.Version}.");
                }

                if (!await DownloadFabricLibraries(
                        profile.Version,
                        profile.LoaderVersion,
                        forceDownload: true))
                {
                    throw new IOException(
                        $"Fabric libraries for {profile.LoaderVersion} could not be reinstalled.");
                }
            }
            else if (profile.Loader.Equals(
                         "Forge",
                         StringComparison.OrdinalIgnoreCase))
            {
                string java21 = Path.Combine(
                    root,
                    "Java",
                    "jdk-21",
                    "bin",
                    "java.exe");
                if (!File.Exists(java21))
                    await DownloadJavaVersions();
                if (!File.Exists(java21))
                    throw new IOException(
                        "Java 21 is required to install Forge.");

                if (!await DownloadForgeProfile(
                        profile.Version,
                        profile.LoaderVersion))
                {
                    throw new IOException(
                        $"Forge {profile.LoaderVersion} could not be installed for Minecraft {profile.Version}.");
                }

                if (!await DownloadForgeLibraries(
                        profile.Version,
                        profile.LoaderVersion,
                        forceDownload: true))
                {
                    throw new IOException(
                        $"Forge libraries for {profile.LoaderVersion} could not be reinstalled.");
                }
                string forgeLibrary = Path.Combine(
                    root,
                    "libraries",
                    profile.Version,
                    $"forge-{profile.LoaderVersion}-universal.jar");
                if (!File.Exists(forgeLibrary) ||
                    new FileInfo(forgeLibrary).Length == 0)
                {
                    throw new IOException(
                        $"Forge libraries for {profile.LoaderVersion} are missing.");
                }
            }
            else if (profile.Loader.Equals(
                         "NeoForge",
                         StringComparison.OrdinalIgnoreCase))
            {
                if (!await DownloadNeoForgeProfile(
                        profile.Version,
                        profile.LoaderVersion,
                        forceDownload: true))
                {
                    throw new IOException(
                        $"NeoForge {profile.LoaderVersion} could not be reinstalled for Minecraft {profile.Version}.");
                }
            }
            else if (profile.Loader.Equals(
                         "Vanilla",
                         StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported loader: {profile.Loader}");
            }
        });

    await RunReinstallTaskAsync(
        "Reinstalling assets...",
        async () =>
        {
            if (!await DownloadAssets(profile.Version, strict: true))
            {
                throw new IOException(
                    $"Minecraft assets for {profile.Version} could not be reinstalled.");
            }
        });

    int installed = 0;
    List<string> failed = new();
    List<string> skipped = new();
    await RunReinstallTaskAsync(
        "Updating marketplace content...",
        async () =>
        {
            (installed, failed, skipped) =
                await ReinstallProfileContentAsync(profile);
        });
    List<string> summary = new()
    {
        LocalizationManager.Instance.Format(
            "Profile core reinstalled: Minecraft {0}, {1} {2}.",
            profile.Version,
            profile.Loader,
            profile.LoaderVersion),
        LocalizationManager.Instance.Format(
            "Marketplace content updated: {0}.",
            installed)
    };
    if (failed.Count > 0)
    {
        summary.Add(
            LocalizationManager.Instance.Format(
                "Failed: {0}",
                string.Join(", ", failed)));
    }
    if (skipped.Count > 0)
    {
        summary.Add(
            LocalizationManager.Instance.Format(
                "Kept as custom content (not found on either marketplace): {0}",
                string.Join(", ", skipped)));
    }

    
}

private async Task<(int installed, List<string> failed, List<string> skipped)>
    ReinstallProfileContentAsync(Profile profile)
{
    string previousTab = currentTab;
    bool previousSuppressContentInstallUi = suppressContentInstallUi;
    int installedCount = 0;
    List<string> failed = new();
    List<string> skipped = new();
    suppressContentInstallUi = true;
    try
    {
        var contentGroups = new[]
        {
            (Type: "mod", Items: profile.Mods ?? new List<ContentItem>()),
            (Type: "resourcepack", Items: profile.RP ?? new List<ContentItem>()),
            (Type: "shader", Items: profile.Shaders ?? new List<ContentItem>())
        };

        foreach ((string type, List<ContentItem> items) in contentGroups)
        {
            currentTab = type;
            foreach (ContentItem item in items.ToList())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Name))
                    continue;

                string taskName =
                    LocalizationManager.Instance.Format(
                        "Updating content: {0}",
                        item.Name);
                StartTask(taskName);
                try
                {
                    bool? installed =
                        await ReinstallProfileContentItemAsync(item, type);
                    if (installed == true)
                        installedCount++;
                    else if (installed == false)
                        failed.Add(item.Name);
                    else if (installed == null)
                        skipped.Add(item.Name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Could not reinstall {type} '{item.Name}': {ex}");
                    failed.Add(item.Name);
                }
                finally
                {
                    StopTask(taskName);
                }
            }
        }
    }
    finally
    {
        currentTab = previousTab;
        suppressContentInstallUi = previousSuppressContentInstallUi;
    }

    ReloadProfileData();
    return (installedCount, failed, skipped);
}

private async Task<bool?> ReinstallProfileContentItemAsync(
    ContentItem item,
    string contentType)
{
    bool marketplaceMatchFound =
        !string.IsNullOrWhiteSpace(item.ProjectId) &&
        !string.IsNullOrWhiteSpace(item.VersionProvider);
    bool lookupFailed = false;

    if (string.Equals(
            item.VersionProvider,
            "Modrinth",
            StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(item.ProjectId))
    {
        try
        {
            bool installed = await InstallMod(
                item.ProjectId,
                item.Name,
                item.Description,
                existingItem: item,
                iconUrl: item.IconUrl,
                contentType: contentType,
                stableReleaseOnly: true);
            if (installed)
                return true;
        }
        catch (Exception ex)
        {
            lookupFailed = true;
            Console.WriteLine(
                $"Modrinth reinstall failed for '{item.Name}': {ex.Message}");
        }
    }
    else if (string.Equals(
                 item.VersionProvider,
                 "CurseForge",
                 StringComparison.OrdinalIgnoreCase) &&
             int.TryParse(item.ProjectId, out int curseForgeProjectId))
    {
        try
        {
            CurseForgeMod curseForgeMod =
                await GetCurseForgeModAsync(curseForgeProjectId);
            bool installed = await InstallCurseForgeMod(
                curseForgeMod,
                existingItem: item,
                contentType: contentType,
                stableReleaseOnly: true);
            if (installed)
                return true;
        }
        catch (Exception ex)
        {
            lookupFailed = true;
            Console.WriteLine(
                $"CurseForge reinstall failed for '{item.Name}': {ex.Message}");
        }
    }

    string[] names = new[]
        {
            item.MarketplaceName,
            item.Name
        }
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    foreach (string name in names)
    {
        ModrinthProject project = null;
        try
        {
            project = await FindExactModrinthProjectAsync(
                name,
                contentType);
        }
        catch (Exception ex)
        {
            lookupFailed = true;
            Console.WriteLine(
                $"Modrinth lookup failed for '{name}': {ex.Message}");
        }

        string modrinthProjectId = project?.project_id ?? project?.id;
        if (!string.IsNullOrWhiteSpace(modrinthProjectId))
        {
            marketplaceMatchFound = true;
            try
            {
                string projectName =
                    project.title ?? project.slug ?? name;
                if (await InstallMod(
                        modrinthProjectId,
                        projectName,
                        project.description,
                        existingItem: item,
                        iconUrl: project.icon_url,
                        contentType: contentType,
                        stableReleaseOnly: true))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Modrinth reinstall failed for '{name}': {ex.Message}");
            }
        }

        try
        {
            List<CurseForgeMod> matches = await LoadCurseForgeMods(
                name,
                currentProfile.Loader,
                currentProfile.Version);
            CurseForgeMod exactMatch = matches.FirstOrDefault(candidate =>
                string.Equals(
                    NormalizeName(candidate.name),
                    NormalizeName(name),
                    StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                marketplaceMatchFound = true;
                if (await InstallCurseForgeMod(
                    exactMatch,
                    existingItem: item,
                    contentType: contentType,
                    stableReleaseOnly: true))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            lookupFailed = true;
            Console.WriteLine(
                $"CurseForge lookup/reinstall failed for '{name}': {ex.Message}");
        }
    }

    return marketplaceMatchFound || lookupFailed ? false : null;
}

private async Task<ModrinthProject> FindExactModrinthProjectAsync(
    string name,
    string contentType)
{
    string projectType = contentType switch
    {
        "resourcepack" => "resourcepack",
        "shader" or "shaderpack" => "shader",
        _ => "mod"
    };
    string facets = Uri.EscapeDataString(
        JsonSerializer.Serialize(
            new[] { new[] { $"project_type:{projectType}" } }));
    string url =
        $"https://api.modrinth.com/v2/search?limit=100" +
        $"&query={Uri.EscapeDataString(name)}&facets={facets}";

    using HttpClient client = new();
    string json = await client.GetStringAsync(url);
    ModrinthResponse response =
        JsonSerializer.Deserialize<ModrinthResponse>(json);
    string normalizedName = NormalizeName(name);
    return response?.hits?.FirstOrDefault(project =>
        project != null &&
        string.Equals(
            NormalizeName(project.title ?? string.Empty),
            normalizedName,
            StringComparison.OrdinalIgnoreCase) ||
        (project != null &&
         string.Equals(
             NormalizeName(project.slug ?? string.Empty),
             normalizedName,
             StringComparison.OrdinalIgnoreCase)));
}

private async Task<CurseForgeMod> GetCurseForgeModAsync(int projectId)
{
    using HttpClient client = new();
    client.DefaultRequestHeaders.Add("x-api-key", CurseForgeApiKey);
    string json = await client.GetStringAsync(
        $"https://api.curseforge.com/v1/mods/{projectId}");
    CurseForgeModDetailsResponse response =
        JsonSerializer.Deserialize<CurseForgeModDetailsResponse>(json);
    if (response?.data == null ||
        string.IsNullOrWhiteSpace(response.data.name))
    {
        throw new InvalidDataException(
            $"CurseForge did not return project {projectId}.");
    }

    return new CurseForgeMod
    {
        id = response.data.id,
        name = response.data.name,
        summary = response.data.summary,
        logo = response.data.logo
    };
}

private readonly Random profileIconRandom = new();

private readonly string[] profilePictures =
{
    "1.png",
    "2.png",
    "3.png"
   
};
    public class Profile
    {
        public string Name { get; set; }
        public string Loader { get; set; }
        public string LoaderVersion { get; set; }
        public string ProfilePicture { get; set; }
        public string Version { get; set; }
        public bool NrcEnabled { get; set; } = false;
        public string? ModrinthInstanceId { get; set; }
        public string? ModrinthRelativePath { get; set; }
        public string? ModrinthDirectory { get; set; }
        public string? ModrinthInstallStage { get; set; }
        public string? ModrinthUpdateChannel { get; set; }
        public string? ModrinthIconPath { get; set; }
        public long? ModrinthLastPlayed { get; set; }
        public long? ModrinthSubmittedTimePlayed { get; set; }
        public long? ModrinthRecentTimePlayed { get; set; }
        public string? ModrinthContentSetId { get; set; }
        public string? ModrinthContentSetName { get; set; }
        public string? ModrinthContentSetStatus { get; set; }
        public string? ModrinthSourceKind { get; set; }
        public long? ModrinthProtocolVersion { get; set; }
        public string? ModrinthLinkKind { get; set; }
        public string? ModrinthProjectId { get; set; }
        public string? ModrinthVersionId { get; set; }
        public long? ModrinthCreated { get; set; }
        public long? ModrinthModified { get; set; }
        public string? NoRiskProfileId { get; set; }
        public string? NoRiskRelativePath { get; set; }
        public string? NoRiskDirectory { get; set; }
        public List<NoRiskCachedModSnapshot> NoRiskCachedMods { get; set; } = new();
        public int NoRiskMissingCachedModCount { get; set; }
        public string? NoRiskCacheWarning { get; set; }

        [JsonIgnore]
        public bool IsModrinthProfile =>
            !string.IsNullOrWhiteSpace(ModrinthInstanceId);

        [JsonIgnore]
        public bool IsNoRiskProfile =>
            !string.IsNullOrWhiteSpace(NoRiskProfileId);

        [JsonIgnore]
        public bool IsExternalProfile =>
            IsModrinthProfile || IsNoRiskProfile;

        public List<ContentItem> Mods { get; set; } = new();
        public List<ContentItem> RP { get; set; } = new();
        public List<ContentItem> Shaders { get; set; } = new();
    }    
    private void UpdateWelcomeText()
    {
        if (currentAccountData != null)
        {
            WelcomeText.Text = LocalizationManager.Instance.Translate(
                $"Welcome back, {currentAccountData.username}!");
        }
        else
        {
            WelcomeText.Text =
                LocalizationManager.Instance.Translate("Welcome back!");
        }
    }
    private void ProfilesPage_DragOver(
        object sender,
        DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files =
                (string[])e.Data.GetData(
                    DataFormats.FileDrop);

            if (files.Length == 1 &&
                (
                    files[0].EndsWith(
                        ".noriskpack",
                        StringComparison.OrdinalIgnoreCase)

                    ||

                    files[0].EndsWith(
                        ".jxcpack",
                        StringComparison.OrdinalIgnoreCase)

                    ||

                    files[0].EndsWith(
                        ".mrpack",
                        StringComparison.OrdinalIgnoreCase)
                ))
            {
                e.Effects =
                    DragDropEffects.Copy;
            }
            else
            {
                e.Effects =
                    DragDropEffects.None;
            }
        }
        else
        {
            e.Effects =
                DragDropEffects.None;
        }

        e.Handled = true;
    }    private async void ProfilesPage_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);

        if (files.Length != 1)
        {
            ShowToast("Drop only one file!");
            return;
        }

        string file = files[0];
        string ext = Path.GetExtension(file).ToLower();

        if (ext == ".noriskpack")
        {
            await ImportNoriskProfile(file);
        }
        else if (ext == ".jxcpack")
        {
            await ImportJaxProfile(file);
        }
        else if (ext == ".mrpack")
        {
            await ImportMrPack(file);
        }

        else
        {
            ShowToast("Unsupported file type!");
        }
    }

    private async Task<string> PrepareNoRiskProfileModsAsync(Profile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.NoRiskCacheWarning))
        {
            throw new InvalidDataException(
                $"NoRisk profile mods could not be fully resolved: {profile.NoRiskCacheWarning}");
        }
        if (profile.NoRiskMissingCachedModCount > profile.NoRiskCachedMods.Count)
        {
            throw new InvalidDataException(
                "NoRisk mod download information is incomplete. Synchronize the NoRisk profiles again and retry.");
        }

        if (!Guid.TryParse(profile.NoRiskProfileId, out Guid profileId))
        {
            throw new InvalidDataException(
                $"NoRisk profile '{profile.Name}' has an invalid profile ID.");
        }

        string cacheDirectory = NoRiskProfileSynchronizer.GetModCacheDirectory();
        string installedModsDirectory = Path.Combine(
            GetProfileDirectory(profile),
            "mods");
        string runtimeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "NoRiskRuntime",
            profileId.ToString("N"),
            ValidatePathSegment(profile.Version),
            ValidatePathSegment(profile.LoaderVersion));
        string runtimeModsDirectory = Path.Combine(runtimeRoot, "mods");
        string stagingDirectory = Path.Combine(
            runtimeRoot,
            $"mods.{Guid.NewGuid():N}.tmp");

        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(2)
        };
        Directory.CreateDirectory(cacheDirectory);
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            foreach (NoRiskCachedModSnapshot mod in profile.NoRiskCachedMods)
            {
                ThrowIfCancelled();
                if (Path.IsPathRooted(mod.FileName) ||
                    !string.Equals(
                        Path.GetFileName(mod.FileName),
                        mod.FileName,
                        StringComparison.Ordinal) ||
                    !Path.GetExtension(mod.FileName).Equals(
                        ".jar",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"The NoRisk profile contains an invalid mod filename: '{mod.FileName}'.");
                }

                string stagingPath = Path.Combine(stagingDirectory, mod.FileName);
                string installedPath = Path.Combine(
                    installedModsDirectory,
                    mod.FileName);
                string cachedPath = Path.Combine(cacheDirectory, mod.FileName);
                string sourcePath;

                if (File.Exists(installedPath) &&
                    await IsValidNoRiskModArchiveAsync(
                        installedPath,
                        mod.Sha1,
                        launchCts?.Token ?? CancellationToken.None))
                {
                    sourcePath = installedPath;
                }
                else
                {
                    await EnsureNoRiskModCachedAsync(
                        client,
                        mod,
                        cachedPath,
                        launchCts?.Token ?? CancellationToken.None);
                    sourcePath = cachedPath;
                }

                if (IsNoRiskClientModArchive(sourcePath))
                {
                    Console.WriteLine(
                        $"Skipping NoRisk Client mod '{mod.Name}' ({mod.FileName}).");
                    continue;
                }

                File.Copy(sourcePath, stagingPath, overwrite: false);
            }

            if (Directory.Exists(runtimeModsDirectory))
            {
                Directory.Delete(runtimeModsDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, runtimeModsDirectory);
            return runtimeModsDirectory;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static bool IsNoRiskClientModArchive(string filePath)
    {
        using ZipArchive archive = ZipFile.OpenRead(filePath);
        ZipArchiveEntry? manifestEntry =
            archive.GetEntry("fabric.mod.json");
        if (manifestEntry == null)
        {
            return false;
        }

        using Stream manifestStream = manifestEntry.Open();
        using JsonDocument manifest = JsonDocument.Parse(manifestStream);
        return manifest.RootElement.TryGetProperty(
                   "id",
                   out JsonElement idElement) &&
               idElement.ValueKind == JsonValueKind.String &&
               idElement.GetString()!.StartsWith(
                   "nrc",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ValidatePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value is "." or ".." ||
            !string.Equals(
                Path.GetFileName(value),
                value,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Invalid NoRisk runtime path component: '{value}'.");
        }

        return value;
    }

    private static string GetNoRiskModsFolderArgument(
        Profile profile,
        string? modsDirectory)
    {
        if (!profile.IsNoRiskProfile ||
            !profile.Loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(modsDirectory))
        {
            return string.Empty;
        }

        return $"-Dfabric.modsFolder=\"{modsDirectory}\" ";
    }

    private static void ValidateFabricProfileMetadata(
        JsonElement metadata,
        string minecraftVersion,
        string loaderVersion)
    {
        string expectedId =
            $"fabric-loader-{loaderVersion}-{minecraftVersion}";
        string actualId = metadata.TryGetProperty(
                "id",
                out JsonElement idElement)
            ? idElement.GetString() ?? string.Empty
            : string.Empty;
        string inheritedVersion = metadata.TryGetProperty(
                "inheritsFrom",
                out JsonElement inheritedElement)
            ? inheritedElement.GetString() ?? string.Empty
            : string.Empty;
        bool containsLoaderLibrary =
            metadata.TryGetProperty(
                "libraries",
                out JsonElement libraries) &&
            libraries.ValueKind == JsonValueKind.Array &&
            libraries.EnumerateArray().Any(library =>
                library.TryGetProperty(
                    "name",
                    out JsonElement nameElement) &&
                string.Equals(
                    nameElement.GetString(),
                    $"net.fabricmc:fabric-loader:{loaderVersion}",
                    StringComparison.Ordinal));

        if (!string.Equals(actualId, expectedId, StringComparison.Ordinal) ||
            !string.Equals(
                inheritedVersion,
                minecraftVersion,
                StringComparison.Ordinal) ||
            !containsLoaderLibrary)
        {
            throw new InvalidDataException(
                $"Fabric metadata does not match Minecraft {minecraftVersion} / Fabric Loader {loaderVersion}. Reinstall the Fabric profile.");
        }
    }

    private static async Task EnsureNoRiskModCachedAsync(
        HttpClient client,
        NoRiskCachedModSnapshot mod,
        string cachePath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(cachePath) &&
            await IsValidNoRiskModArchiveAsync(
                cachePath,
                mod.Sha1,
                cancellationToken))
        {
            return;
        }

        string downloadUrl = GetNoRiskModDownloadUrl(mod);
        Uri uri = new(downloadUrl, UriKind.Absolute);
        if (uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException(
                $"The download URL for NoRisk mod '{mod.Name}' must use HTTP or HTTPS.");
        }

        Exception? lastDownloadError = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string temporaryCachePath = cachePath + "." +
                                        Guid.NewGuid().ToString("N") + ".download";
            try
            {
                using HttpResponseMessage response = await client.GetAsync(
                    uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                await using (FileStream output = new(
                                 temporaryCachePath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 useAsync: true))
                {
                    await response.Content.CopyToAsync(
                        output,
                        cancellationToken);
                }

                if (!await IsValidNoRiskModArchiveAsync(
                        temporaryCachePath,
                        mod.Sha1,
                        cancellationToken))
                {
                    throw new InvalidDataException(
                        $"The downloaded NoRisk mod '{mod.Name}' is invalid or failed SHA-1 verification.");
                }

                File.Move(temporaryCachePath, cachePath, overwrite: true);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or IOException)
            {
                lastDownloadError = exception;
                if (attempt == 3)
                {
                    break;
                }
            }
            finally
            {
                if (File.Exists(temporaryCachePath))
                {
                    File.Delete(temporaryCachePath);
                }
            }
        }

        throw new IOException(
            $"Could not download NoRisk mod '{mod.Name}' after 3 attempts.",
            lastDownloadError);
    }

    private static async Task<bool> IsValidNoRiskModArchiveAsync(
        string filePath,
        string? expectedSha1,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                if (archive.Entries.Count == 0)
                {
                    return false;
                }
            }
        }
        catch (InvalidDataException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(expectedSha1))
        {
            return true;
        }

        await using FileStream input = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);
        byte[] actualHash = await System.Security.Cryptography.SHA1.HashDataAsync(
            input,
            cancellationToken);
        return string.Equals(
            Convert.ToHexString(actualHash),
            expectedSha1,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetNoRiskModDownloadUrl(
        NoRiskCachedModSnapshot mod)
    {
        if (!string.IsNullOrWhiteSpace(mod.DownloadUrl))
        {
            return mod.DownloadUrl;
        }

        if (string.IsNullOrWhiteSpace(mod.RepositoryUrl) ||
            string.IsNullOrWhiteSpace(mod.GroupId) ||
            string.IsNullOrWhiteSpace(mod.ArtifactId) ||
            string.IsNullOrWhiteSpace(mod.Version))
        {
            throw new InvalidDataException(
                $"No download source is available for NoRisk mod '{mod.Name}' ({mod.FileName}). " +
                "Synchronize the NoRisk profiles again and retry.");
        }

        string groupPath = string.Join(
            "/",
            mod.GroupId.Split('.').Select(Uri.EscapeDataString));
        string artifact = Uri.EscapeDataString(mod.ArtifactId);
        string version = Uri.EscapeDataString(mod.Version);
        string fileName = Uri.EscapeDataString(mod.FileName);
        return $"{mod.RepositoryUrl.TrimEnd('/')}/{groupPath}/{artifact}/{version}/{fileName}";
    }

    private void Search_TextChangedd(object sender, TextChangedEventArgs e)
    {
        if (ProfilesList == null || SearchBox == null)
        {
            return;
        }

        LoadProfiles();
    }

    private async void LoadLastAccount()
    {
        string dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "data.json"
        );
    
        string accPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "accounts.json"
        );
    
        if (!File.Exists(dataPath) || !File.Exists(accPath))
            return;
    
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(dataPath)
        );
    
        if (data == null || !data.ContainsKey("lastAccount"))
            return;
    
        string lastUsername = data["lastAccount"];
    
        var accounts = JsonSerializer.Deserialize<AccountFile>(
            File.ReadAllText(accPath)
        );
    
        if (accounts?.accounts == null)
            return;
    
        var acc = accounts.accounts.FirstOrDefault(a => a.username == lastUsername);
    
        if (acc == null)
            return;
    

        currentAccountData = acc;
        

        AccountNameText.Text = acc.username;

       

        LoadAccountHead(acc.username);

        LoadSkinLocal();
        UpdateWelcomeText();
        AccountNameText.Text = acc.username;
        LoadAccountHead(acc.username);
        LoadSkinLocal();
        UpdateWelcomeText();
    
       
    }
    private void CancelOffline_Click(object sender, RoutedEventArgs e)
    {
        OfflineLoginOverlay.Visibility = Visibility.Collapsed;
    }
    
    private const int WM_GETMINMAXINFO = 0x0024;
    
    private void MainWindow_SourceInitialized(object sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        int style = GetWindowLong(hwnd, GWL_STYLE);

        style &= ~WS_MAXIMIZEBOX;
        style &= ~WS_THICKFRAME;

        SetWindowLong(hwnd, GWL_STYLE, style);
    }    
    private const int GWL_STYLE = -16;

    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_THICKFRAME = 0x00040000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(
        IntPtr hWnd,
        int nIndex,
        int dwNewLong);
    private async Task CheckInternetStatus()
    {
        bool googleOk = false;
        bool cloudflareOk = false;

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync("https://www.google.com");
            googleOk = response.IsSuccessStatusCode;
        }
        catch { }

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync("https://1.1.1.1");
            cloudflareOk = response.IsSuccessStatusCode;
        }
        catch { }

        bool offline = !googleOk && !cloudflareOk;

        bool wasOffline = IsOfflineMode;

        if (offline == IsOfflineMode)
            return;

        IsOfflineMode = offline;

        Dispatcher.Invoke(() =>
        {
            UpdateOfflineIcon();
        });


        if (wasOffline && !offline)
        {
            

            Dispatcher.Invoke(() =>
            {
                if (currentAccountData != null)
                {
                    
                    LoadAccountHead(currentAccountData.username);
                }
            });

            if (!IsOfflineMode &&
                currentAccountData != null &&
                !string.IsNullOrEmpty(currentAccountData.refreshToken))

            {
                try
                {
                    StartTask("Refreshing session...");

                    bool success =
                        await RefreshLoginIfNeededAsync(currentAccountData);

                    if (success)
                    {
                        ShowToast("Session refreshed");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    StopTask("Refreshing session...");
                }
            }
        }
    }
    private void StartInternetMonitor()
    {
        internetTimer = new DispatcherTimer();

        internetTimer.Interval = TimeSpan.FromMinutes(2);

        internetTimer.Tick += async (s, e) =>
        {
            await CheckInternetStatus();
        };

        internetTimer.Start();

        _ = CheckInternetStatus();
    }
    
    private void InitMatrix()
    {
        MatrixCanvas.Children.Clear();
        matrixDrops.Clear();
    
        int columns = (int)(ActualWidth / 18);
    
        if (columns <= 0)
            columns = 80;
    
        for (int x = 0; x < columns; x++)
        {
            TextBlock tb = new()
            {
                Text = matrixCharsSet[random.Next(matrixCharsSet.Length)].ToString(),
                Foreground = new SolidColorBrush(
                    Color.FromArgb(80, 77, 163, 255)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16
            };
    
            Canvas.SetLeft(tb, x * 18);
            Canvas.SetTop(tb, random.Next(-1000, 0));
    
            MatrixCanvas.Children.Add(tb);
    
            matrixDrops.Add(new MatrixDrop
            {
                Block = tb,
                Speed = random.Next(4, 12)
            });
        }
    
        matrixTimer.Interval = TimeSpan.FromMilliseconds(80);
        matrixTimer.Tick -= MatrixTick;
        matrixTimer.Tick += MatrixTick;
        matrixTimer.Start();
    }
    private void MatrixTick(object? sender, EventArgs e)
    {
        foreach (var drop in matrixDrops)
        {
            double y = Canvas.GetTop(drop.Block);

            y += drop.Speed;

            TextBlock tb = new()
            {
                Text = matrixCharsSet[random.Next(matrixCharsSet.Length)].ToString(),
                Foreground = new SolidColorBrush(
                    Color.FromArgb(80, 77, 163, 255)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16
            };

            if (y > ActualHeight + 50)
            {
                y = random.Next(-500, -50);
            }

            Canvas.SetTop(drop.Block, y);
        }
    }
    
    
    private DispatcherTimer instanceTimer = new();

    private void InitInstanceWatcher()
    {
        instanceTimer.Interval = TimeSpan.FromSeconds(1);
        instanceTimer.Tick += (_, _) => UpdateInstancesButton();
        instanceTimer.Start();
    }
    private void UpdateInstancesButton()
    {
        runningMinecraftInstances.RemoveAll(p => p.HasExited);

        int count = runningMinecraftInstances.Count;

        if (count > 0)
        {
            InstancesButton.Visibility = Visibility.Visible;
            InstancesButton.Content =
                count == 1
                    ? "1 Instance"
                    : $"{count} Instances";
        }
        else
        {
            InstancesButton.Visibility = Visibility.Collapsed;
        }
    }
    private bool isDragging = false;
    private Point startPoint;

    private void ShowLogsWindow(RunningInstance? selectedInstance = null)
    {
        if (logWindow == null)
        {
            logWindow = new LogWindow();
            logWindow.KillRequested += KillInstance;
            logWindow.CopyRequested += CopyLogs;
            logWindow.ClearRequested += ClearLogs;
            logWindow.Closed += (_, _) => logWindow = null;
        }

        logWindow.SetInstances(runningInstances, selectedInstance);
        if (!logWindow.IsVisible)
        {
            logWindow.Show();
        }

        logWindow.WindowState = WindowState.Normal;
        logWindow.Activate();
    }

    private void KillInstance(RunningInstance instance)
    {
        if (!instance.Process.HasExited)
        {
            try
            {
                instance.Process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        runningInstances.Remove(instance);
        runningMinecraftInstances.Remove(instance.Process);
        logWindow?.SetInstances(runningInstances);
        UpdateDiscordPresence();
        UpdateInstancesButton();
    }

    private void CopyLogs(RunningInstance instance)
    {
        string logText = ReferenceEquals(logWindow?.SelectedInstance, instance)
            ? logWindow.DisplayedLogText
            : string.Join(Environment.NewLine, instance.ConsoleLines);
        if (string.IsNullOrWhiteSpace(logText))
        {
            ShowToast("No log output available to copy.");
            return;
        }

        try
        {
            Clipboard.SetText(logText);
            ShowToast("Logs copied.");
        }
        catch (ExternalException ex)
        {
            Console.WriteLine($"[Instances] Could not copy logs: {ex}");
            ShowToast("Could not copy logs. Please try again.");
        }
    }

    private void ClearLogs(RunningInstance instance)
    {
        instance.ConsoleLines.Clear();
        logWindow?.RefreshInstance(instance);
    }

    private void KillInstance_Click(object sender, RoutedEventArgs e)
    {
        if (InstancesList.SelectedItem is RunningInstance instance)
        {
            KillInstance(instance);
        }
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        if (InstancesList.SelectedItem is RunningInstance instance)
        {
            CopyLogs(instance);
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        if (InstancesList.SelectedItem is RunningInstance instance)
        {
            ClearLogs(instance);
            InstanceConsole.Clear();
        }
    }

    private void InstancesList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (InstancesList.SelectedItem is RunningInstance instance)
        {
            InstanceConsole.Text =
                string.Join(Environment.NewLine, instance.ConsoleLines);
            InstanceConsole.ScrollToEnd();
        }
    }
    private Point dragStart;
    private double startX;
    private double startY;

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        isDragging = true;

        dragStart = e.GetPosition(this);

        startX = PanelTransform.X;
        startY = PanelTransform.Y;

        ((UIElement)sender).CaptureMouse();
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging)
            return;

        Point current = e.GetPosition(this);

        PanelTransform.X = startX + (current.X - dragStart.X);
        PanelTransform.Y = startY + (current.Y - dragStart.Y);
    }

    private void Header_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }
  
    private void InstancesButton_Click(object sender, RoutedEventArgs e)
    {
        ShowLogsWindow(runningInstances.FirstOrDefault(
            instance => !instance.Process.HasExited));
    }
    
    public class LauncherSettings
    {
        public string Language { get; set; } = "en";
        public bool HideLauncherOnLaunch { get; set; }

        public bool OpenLogsAfterStarting { get; set; }
        public bool OfflineMode { get; set; }
        public int RamAllocation { get; set; } = 6;

        public string JvmArguments { get; set; } = "";
        public string ProfileSourceFilter { get; set; } = "All";
        public List<StandardContentItem>
            StandardContent
        {
            get;
            set;
        }
            = new();
        public string? StandardOptionsContent { get; set; }
        public bool MatrixEnabled { get; set; } = true;
        public string PanoramaImage { get; set; } = "None";
    }
    public class StandardContentItem
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public string Platform { get; set; }
    }
    
    private void OfflineMode_Changed(
        object sender,
        RoutedEventArgs e)
    {
        IsOfflineMode =
            OfflineModeCheckBox.IsChecked == true;

        UpdateOfflineIcon();
        SaveSettings();


        if (!IsOfflineMode)
        {
            if (currentAccountData != null)
            {
                LoadAccountHead(currentAccountData.username);
                LoadSkinLocal();
            }

            LoadAccountsUI();
        }

        ShowToast(
            IsOfflineMode
                ? "Offline Mode enabled"
                : "Offline Mode disabled");
    }    private string SettingsFile =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "settings.json");
    
    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                UpdateProfileSourceFilterButtons();
                return;
            }

            string json = File.ReadAllText(SettingsFile);

            var settings =
                JsonSerializer.Deserialize<LauncherSettings>(json);
            string savedProfileSourceFilter =
                settings?.ProfileSourceFilter ?? "All";
            selectedProfileSourceFilter =
                savedProfileSourceFilter is "All" or "JaxClient" or "NRC" or "Modrinth"
                    ? savedProfileSourceFilter
                    : "All";
            UpdateProfileSourceFilterButtons();

            HideLauncherOnLaunchCheckBox.IsChecked =
                settings?.HideLauncherOnLaunch ?? false;
            OfflineModeCheckBox.IsChecked =
                settings?.OfflineMode ?? false;

            IsOfflineMode =
                settings?.OfflineMode ?? false;
            UpdateOfflineIcon();
            JvmArgumentsBox.Text =
                settings?.JvmArguments ?? "";
            OpenLogsAfterStartingCheckBox.IsChecked =
                settings?.OpenLogsAfterStarting ?? false;
            MatrixEnabledCheckBox.IsChecked =
                settings?.MatrixEnabled ?? true;
            RamSlider.Value =
                settings?.RamAllocation ?? 8;

            RamValueText.Text =
                $"{(int)RamSlider.Value} GB";
            string panorama =
                settings?.PanoramaImage ?? "None";
            SaveSettingsObject(settings);
            LoadPanoramaCards();

          

            

            MatrixEnabledCheckBox.IsChecked =
                settings?.MatrixEnabled ?? true;

            bool matrixEnabled =
                settings?.MatrixEnabled ?? true;

            MatrixCanvas.Visibility =
                matrixEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (matrixEnabled)
            {
                if (!matrixTimer.IsEnabled)
                    matrixTimer.Start();
            }
            else
            {
                matrixTimer.Stop();
            }
        }
        catch
        {
        }
    }
    private LauncherSettings LoadSettingsObject()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new LauncherSettings();

            string json =
                File.ReadAllText(SettingsFile);

            return JsonSerializer.Deserialize<LauncherSettings>(json)
                   ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    private bool SaveSettingsObject(
        LauncherSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(
                SettingsFile,
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.WriteLine($"Could not save launcher settings: {ex}");
            ShowToast($"Could not save settings: {ex.Message}");
            return false;
        }
    }
    private void RamSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (RamValueText != null)
        {
            RamValueText.Text =
                $"{(int)RamSlider.Value} GB";
        }
    
        SaveSettings();
    }
    private void OpenLauncherFolder_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient");

        Process.Start("explorer.exe", path);
    }

    private void CopyToOtherUser_Click(
        object sender,
        RoutedEventArgs e)
    {
        CopyToOtherUserWindow dialog = new()
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OpenProfilesFolder_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles");

        Process.Start("explorer.exe", path);
    }
    private void ResetSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            string settingsPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "settings.json");

            if (File.Exists(settingsPath))
                File.Delete(settingsPath);

            RamSlider.Value = 8;
            JvmArgumentsBox.Text = "";

            HideLauncherOnLaunchCheckBox.IsChecked = false;
            OpenLogsAfterStartingCheckBox.IsChecked = false;

            RamValueText.Text = "8 GB";

            // Clear the standard content list as well.
            var settings = new LauncherSettings
            {
                StandardContent =
                    new List<StandardContentItem>()
            };

            SaveSettingsObject(settings);

            ReloadStandardContentList();

            ShowToast("Settings reset!");
        }
        catch (Exception ex)
        {
            ShowToast("Failed: " + ex.Message);
        }
    }
    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string metaPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "meta");

            if (Directory.Exists(metaPath))
            {
                Directory.Delete(metaPath, true);
            }

            ShowToast("Cache cleared!");
        }
        catch (Exception ex)
        {
            ShowToast("Failed: " + ex.Message);
        }
    }
    private void JvmArgumentsBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        SaveSettings();
    }
    private void MatrixEnabled_Changed(
        object sender,
        RoutedEventArgs e)
    {
        bool enabled =
            MatrixEnabledCheckBox.IsChecked == true;

        MatrixCanvas.Visibility =
            enabled
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (enabled)
        {
            if (!matrixTimer.IsEnabled)
                matrixTimer.Start();
        }
        else
        {
            matrixTimer.Stop();
        }

        SaveSettings();
    }
   
    private void LoadPanoramaCards()
    {
        PanoramaList.Children.Clear();

        AddPanoramaCard(
            "None",
            null);

        AddPanoramaCard(
            "p1.png",
            "pack://application:,,,/Assets/panorama/p1.png");

        AddPanoramaCard(
            "p2.png",
            "pack://application:,,,/Assets/panorama/p2.png");

        AddPanoramaCard(
            "p3.png",
            "pack://application:,,,/Assets/panorama/p3.png");
    }
    
    private void ApplyPanorama()
    {
        try
        {
            var settings = LoadSettingsObject();

            if (string.IsNullOrWhiteSpace(settings.PanoramaImage) ||
                settings.PanoramaImage == "None")
            {
                PanoramaBackground.Source = null;
                
                return;
            }

            var bmp = new BitmapImage(
                new Uri(
                    $"pack://application:,,,/Assets/panorama/{settings.PanoramaImage}",
                    UriKind.Absolute));

            PanoramaBackground.Source = bmp;
            
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
    }    
    private void AddPanoramaCard(
    string name,
    string imagePath)
{
    var settings =
        LoadSettingsObject();

    bool selected =
        settings.PanoramaImage == name;

    Border card = new Border
    {
        Width = 180,
        Height = 120,
        Margin = new Thickness(0, 0, 15, 15),

        CornerRadius = new CornerRadius(14),

        BorderThickness = new Thickness(2),

        BorderBrush =
            selected
                ? Brushes.Orange
                : new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(
                        "#334155")),

        Cursor = Cursors.Hand,

        Background =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    "#111827"))
    };

    Grid grid = new Grid();



    if (imagePath == null)
    {
        TextBlock noneText = new TextBlock
        {
            Text = "NONE",
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment =
                HorizontalAlignment.Center,
            VerticalAlignment =
                VerticalAlignment.Center
        };

        grid.Children.Add(noneText);
    }
    else
    {
        Image image = new Image
        {
            Stretch = Stretch.UniformToFill
        };

        try
        {
            image.Source =
                new BitmapImage(
                    new Uri(imagePath));
        }
        catch
        {
        }

        grid.Children.Add(image);

        Border darkOverlay = new Border
        {
            Background =
                new SolidColorBrush(
                    Color.FromArgb(
                        80,
                        0,
                        0,
                        0))
        };

        grid.Children.Add(darkOverlay);


        
    }



    if (selected)
    {
        Border badge = new Border
        {
            Width = 24,
            Height = 24,

            CornerRadius =
                new CornerRadius(12),

            Background = Brushes.Orange,

            HorizontalAlignment =
                HorizontalAlignment.Right,

            VerticalAlignment =
                VerticalAlignment.Top,

            Margin = new Thickness(0,8,8,0)
        };

        badge.Child = new TextBlock
        {
            Text = "✓",

            Foreground = Brushes.White,

            FontWeight = FontWeights.Bold,

            HorizontalAlignment =
                HorizontalAlignment.Center,

            VerticalAlignment =
                VerticalAlignment.Center,

            TextAlignment =
                TextAlignment.Center
        };

        grid.Children.Add(badge);
    }

    card.Child = grid;

    card.MouseLeftButtonUp += (s, e) =>
    {
        var cfg =
            LoadSettingsObject();

        cfg.PanoramaImage = name;

        SaveSettingsObject(cfg);
        
        LoadPanoramaCards();

       ApplyPanorama();
    };

    PanoramaList.Children.Add(card);
}
    private void SaveSettings()
    {
        try
        {
            LauncherSettings settings = LoadSettingsObject();
            settings.Language = LocalizationManager.Instance.Language;
            settings.HideLauncherOnLaunch =
                HideLauncherOnLaunchCheckBox.IsChecked == true;
            settings.OpenLogsAfterStarting =
                OpenLogsAfterStartingCheckBox.IsChecked == true;
            settings.OfflineMode =
                OfflineModeCheckBox.IsChecked == true;
            settings.JvmArguments = JvmArgumentsBox.Text;
            settings.RamAllocation = (int)RamSlider.Value;
            settings.MatrixEnabled =
                MatrixEnabledCheckBox.IsChecked == true;
            SaveSettingsObject(settings);
        }
        catch
        {
        }
    }

    private void LanguageSelector_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (isInitializingLanguage ||
            LanguageSelector.SelectedValue is not string language)
            return;

        LocalizationManager.Instance.SetLanguage(language);
        LauncherSettings settings = LoadSettingsObject();
        settings.Language = language;
        SaveSettingsObject(settings);
    }
    private void OpenLogsAfterStarting_Changed(object sender, RoutedEventArgs e)
    {
        SaveSettings();
    }
    private void HideLauncherOnLaunch_Changed(
        object sender,
        RoutedEventArgs e)
    {
        SaveSettings();
    }
    
    private void CloseInstances_Click(object sender, RoutedEventArgs e)
    {
        InstancesPanel.Visibility = Visibility.Collapsed;
    }
    private async Task EnsureLatestUpdater()
    {
        try
        {
            string updaterUrl =
                "https://github.com/Jaxroxnyt/JaxClient-v3/releases/download/0.0.1/Updater-v2.5.exe";

            string updaterPath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "JXCUpdater.exe");

            string tempPath =
                updaterPath + ".tmp";

            using HttpClient client =
                new HttpClient();

            byte[] data =
                await client.GetByteArrayAsync(
                    updaterUrl);

            await File.WriteAllBytesAsync(
                tempPath,
                data);

            if (File.Exists(updaterPath))
            {
                File.Delete(updaterPath);
            }

            File.Move(
                tempPath,
                updaterPath);

            Console.WriteLine(
                "Updater updated");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Updater update failed: " +
                ex.Message);
        }
    }
    private void RegisterFileAssociations()
    {
        try
        {
            string exePath =
                Process.GetCurrentProcess()
                    .MainModule!
                    .FileName!;

            string iconPath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    "icon.ico");
            using var ext =
                Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\.jxcpack");

            ext?.SetValue("", "JaxClientPack");

            using var type =
                Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\JaxClientPack");

            type?.SetValue("", "JaxClient Pack");

           
            using var icon =
                Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\JaxClientPack\DefaultIcon");

            icon?.SetValue(
                "",
                $"\"{exePath}\",0");

            using var command =
                Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\JaxClientPack\shell\open\command");

            command?.SetValue(
                "",
                $"\"{exePath}\" --launcher --updaterJXC \"%1\"");
        }
        catch
        {
        }
    }
    private async void HandleStartupArguments()
    {
       
        try
        {
            string[] args =
                Environment.GetCommandLineArgs();

            if (args.Length < 2)
                return;

            string file = Environment.GetCommandLineArgs()
                .FirstOrDefault(x =>
                    File.Exists(x) &&
                    (
                        x.EndsWith(".jxcpack", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".noriskpack", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)
                    ));

            if (file == null)
                return;

            if (!File.Exists(file))
                return;

            string ext =
                Path.GetExtension(file)
                    .ToLower();

            if (ext == ".jxcpack")
            {
                await ImportJaxProfile(file);
            }
            else if (ext == ".noriskpack")
            {
                await ImportNoriskProfile(file);
            }
            else if (ext == ".mrpack")
            {
                await ImportMrPack(file);
            }
        }
        catch
        {
        }
    }
   

    private static readonly string[] RequiredFiles =
    {
        "D3DCompiler_47_cor3.dll",
        "glfw3.dll",
        "libSkiaSharp.dll",
        "Magick.Native-Q8-x64.dll",
        "PenImc_cor3.dll",
        "PresentationNative_cor3.dll",
        "vcruntime140_cor3.dll",
        "wpfgfx_cor3.dll"
    };

    public static async Task CheckAndInstallDependencies()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;

        bool missing = RequiredFiles.Any(file =>
            !File.Exists(Path.Combine(appDir, file)));

        if (!missing)
            return;

        string zipUrl = "https://deine-domain.de/runtime.zip";

        string tempZip = Path.Combine(
            Path.GetTempPath(),
            "jaxclient_runtime.zip"
        );

        using (HttpClient client = new HttpClient())
        {
            byte[] data = await client.GetByteArrayAsync(zipUrl);
            await File.WriteAllBytesAsync(tempZip, data);
        }

        ZipFile.ExtractToDirectory(
            tempZip,
            appDir,
            true
        );

        File.Delete(tempZip);

        var stillMissing = RequiredFiles
            .Where(file => !File.Exists(Path.Combine(appDir, file)))
            .ToList();

        if (stillMissing.Count > 0)
        {
            MessageBox.Show(
                "Fehlende Dateien:\n\n" +
                string.Join("\n", stillMissing),
                "JaxClient",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            Environment.Exit(1);
        }
    }
    public MainWindow()
    {
        
        SetCurrentProcessExplicitAppUserModelID(
        "JaxClient.Launcher");
        
        var args = Environment.GetCommandLineArgs();

        
        if (!Environment.GetCommandLineArgs().Contains("--launcher"))
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                string? updaterPath = Directory
                    .GetFiles(baseDir, "*.exe", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f =>
                        Path.GetFileName(f)
                            .Contains("updater", StringComparison.OrdinalIgnoreCase));

                if (updaterPath != null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
            }

          
            Application.Current.Shutdown();
            Environment.Exit(0);
            return;
        }
        
        InitializeComponent();
        
        string configuredLanguage =
            LoadSettingsObject().Language ?? "en";
        LocalizationManager.Instance.SetLanguage(configuredLanguage);
        LanguageSelector.SelectedValue = configuredLanguage;
        LocalizationManager.Instance.LanguageChanged += (_, _) =>
        {
            LocalizationManager.Instance.ApplyToVisualTree(this);
            UpdateTaskUI();
        };
        Loaded += (_, _) =>
            LocalizationManager.Instance.ApplyToVisualTree(this);
        Loaded += async (_, _) =>
        {
            await SynchronizeModrinthProfilesAsync();
            await SynchronizeNoRiskProfilesAsync();
        };
        isInitializingLanguage = false;
        UploadPackCheckBox.Checked += UploadPackCheckBox_Changed;
        UploadPackCheckBox.Unchecked += UploadPackCheckBox_Changed;
        HandleStartupArguments();
        AppRegistration.StartTaskAction1 = StartTask;
        AppRegistration.StopTaskAction1 = StopTask;
        SkinPreviewWindow2.StartTaskAction = StartTask;
        SkinPreviewWindow2.StopTaskAction = StopTask;
        InitInstanceWatcher();
        LoadLauncherVersion();
        _ = InitializeWebsiteAccountAsync();
        try
        {
            
            string versionFile = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "version.json"
            );
           

            if (File.Exists(versionFile))
            {
                string json =
                    File.ReadAllText(versionFile);

                using JsonDocument doc =
                    JsonDocument.Parse(json);

                string version = doc.RootElement.GetProperty("version").GetString();
                Title = $"JaxClient-v{version}";
                TopVersionText.Text = "v."+
                    doc.RootElement
                        .GetProperty("version")
                        .GetString();
            }
            else
            {
                TopVersionText.Text = "Unknown";
                Title = $"JaxClient";
            }
        }
        catch
        {
            TopVersionText.Text = "Unknown";
            Title = $"JaxClient";
        }
        
        string version2 = TopVersionText.Text;
        rpc = new DiscordRPC.DiscordRpcClient(
            "1546147394377228399");

        bool ingame =
            
        runningMinecraftInstances.Count > 0;
        rpc.Initialize();
        UpdateDiscordPresence();
        LoadSettings();
        bool forceOffline =
            OfflineModeCheckBox.IsChecked == true;
        Activated += (_, _) =>
            
        {
            
            if (!matrixTimer.IsEnabled)
                
            matrixTimer.Start();
            
        };
        
       
        
       

        Deactivated += (_, _) =>
        {
            
            if (matrixTimer.IsEnabled)
                
            matrixTimer.Stop();
            
        };
            
        if (forceOffline)
        {
            IsOfflineMode = true;
            UpdateOfflineIcon();
        }
        else
        {
            IsOfflineMode = false;
            UpdateOfflineIcon();
            StartInternetMonitor();
        }
        LoadPanoramaCards();
        ApplyPanorama();
        UpdateTaskUI();
        InitMatrix();
        SourceInitialized += MainWindow_SourceInitialized;

        
       
      
        
        
        
        Dispatcher.InvokeAsync(async () =>
        {
              
            LoadSkinLocal();       
            UpdateWelcomeText();
            
        });

        
        Dispatcher.InvokeAsync(() =>
        {
            LoaderBox.SelectedIndex = 0;
        });


        string folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient"
        );

        Directory.CreateDirectory(folder);

        filePath = System.IO.Path.Combine(folder, "profiles.json");


        LoadLastProfile();
        LoadAccounts(); 
        LoadLastAccount();
        LoadSavedSkins();
        StartTokenRefreshMonitor();

        
        // Load the UI safely.
        Dispatcher.InvokeAsync(() =>
        {
            LoadAccountsUI();
        });
        LoadSkinLocal();
       
        AssignMissingProfilePictures();

        LoadProfiles();
        RegisterFileAssociations();
        Console.WriteLine("Checking App Registration...");
        AppRegistration.CheckAndRegister();
        
      

       
    }
}