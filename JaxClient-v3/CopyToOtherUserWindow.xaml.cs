using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JaxClient_v3;

public partial class CopyToOtherUserWindow : Window
{
    private bool isBusy;

    public CopyToOtherUserWindow()
    {
        InitializeComponent();
    }

    private void CopyWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<WindowsUserProfile> profiles =
                WindowsUserProfileTransfer.GetOtherProfiles();
            ProfileSelector.ItemsSource = profiles;
            if (profiles.Count > 0)
            {
                ProfileSelector.SelectedIndex = 0;
                StatusTextBlock.Text =
                    LocalizationManager.Instance.Translate(
                        "The password is checked only for this copy and is not saved.");
            }
            else
            {
                CopyButton.IsEnabled = false;
                StatusTextBlock.Text =
                    LocalizationManager.Instance.Translate(
                        "No other Windows user profiles were found.");
            }
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is System.Security.SecurityException ||
            ex is InvalidOperationException ||
            ex is ArgumentException)
        {
            CopyButton.IsEnabled = false;
            StatusTextBlock.Text =
                LocalizationManager.Instance.Format(
                    "Could not list Windows user profiles: {0}",
                    ex.Message);
        }
    }

    private async void CopyButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ProfileSelector.SelectedItem is not WindowsUserProfile profile)
        {
            StatusTextBlock.Text =
                LocalizationManager.Instance.Translate(
                    "Select a Windows user.");
            return;
        }

        SecureString password = AccountPasswordBox.SecurePassword.Copy();
        AccountPasswordBox.Clear();

        SetBusy(true);
        Debug.WriteLine("Starting copy-to-other-user operation.");
        StatusTextBlock.Text =
            LocalizationManager.Instance.Translate(
                "Checking account...");

        try
        {
            using SafeAccessTokenHandle _ =
                await Task.Run(
                    () => WindowsUserProfileTransfer.Authenticate(
                        profile,
                        password));
            Debug.WriteLine("Target Windows account authenticated.");

            StatusTextBlock.Text =
                LocalizationManager.Instance.Translate(
                    "Copying .JAXSTORE...");
            await Task.Run(
                () => WindowsUserProfileTransfer.CopyJaxStore(
                    profile));

            StatusTextBlock.Text =
                LocalizationManager.Instance.Translate("Copy completed.");
            Debug.WriteLine("Copy-to-other-user operation completed.");
        }
        catch (OperationCanceledException ex)
        {
            Debug.WriteLine($"Elevated account copy was cancelled: {ex}");
            StatusTextBlock.Text =
                LocalizationManager.Instance.Translate(
                    "Administrator approval was cancelled; the copy was not performed.");
        }
        catch (Win32Exception ex)
        {
            Debug.WriteLine($"Target Windows account authentication failed: {ex}");
            StatusTextBlock.Text =
                LocalizationManager.Instance.Format(
                    "Could not verify the Windows account password: {0}",
                    ex.Message);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is System.Security.SecurityException ||
            ex is InvalidOperationException ||
            ex is ArgumentException)
        {
            Debug.WriteLine($"Copy-to-other-user operation failed: {ex}");
            StatusTextBlock.Text =
                LocalizationManager.Instance.Format(
                    "Could not copy .JAXSTORE: {0}",
                    ex.Message);
        }
        finally
        {
            password.Dispose();
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        ProfileSelector.IsEnabled = !busy;
        AccountPasswordBox.IsEnabled = !busy;
        CopyButton.IsEnabled = !busy;
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed &&
            e.OriginalSource is TextBlock)
        {
            DragMove();
        }
    }

    private void CopyWindow_Closing(
        object? sender,
        CancelEventArgs e)
    {
        if (isBusy)
            e.Cancel = true;
    }
}
