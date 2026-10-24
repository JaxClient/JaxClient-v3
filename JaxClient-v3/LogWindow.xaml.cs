using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JaxClient_v3;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source != null && !ReferenceEquals(source, sender))
        {
            if (source is Button)
            {
                return;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) =>
        Close();

    public event Action<MainWindow.RunningInstance>? KillRequested;
    public event Action<MainWindow.RunningInstance>? CopyRequested;
    public event Action<MainWindow.RunningInstance>? ClearRequested;

    public MainWindow.RunningInstance? SelectedInstance =>
        InstancesList.SelectedItem as MainWindow.RunningInstance;

    public string DisplayedLogText => InstanceConsole.Text;

    public void SetInstances(
        IEnumerable<MainWindow.RunningInstance> instances,
        MainWindow.RunningInstance? preferredInstance = null)
    {
        MainWindow.RunningInstance? selected =
            preferredInstance ?? SelectedInstance;
        InstancesList.ItemsSource = instances.ToList();
        InstancesList.SelectedItem = selected != null &&
                                     InstancesList.Items.Contains(selected)
            ? selected
            : InstancesList.Items.Count > 0
                ? InstancesList.Items[0]
                : null;
        UpdateLogView();
    }

    public void RefreshInstance(MainWindow.RunningInstance instance)
    {
        if (ReferenceEquals(SelectedInstance, instance))
        {
            UpdateLogView();
        }
    }

    private void UpdateLogView()
    {
        MainWindow.RunningInstance? instance = SelectedInstance;
        string windowTitle = instance == null
            ? "Logs"
            : $"Logs - {instance.ProfileName} - {instance.Username}";
        Title = windowTitle;
        WindowTitleText.Text = windowTitle;

        string query = SearchBox.Text.Trim();
        IEnumerable<string> lines = instance?.ConsoleLines ??
                                    Enumerable.Empty<string>();
        if (!string.IsNullOrEmpty(query))
        {
            lines = lines.Where(line =>
                line.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        InstanceConsole.Text = string.Join(Environment.NewLine, lines);
        InstanceConsole.ScrollToEnd();
    }

    private void InstancesList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateLogView();

    private void SearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e) =>
        UpdateLogView();

    private void KillInstance_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInstance is { } instance)
        {
            KillRequested?.Invoke(instance);
        }
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInstance is { } instance)
        {
            CopyRequested?.Invoke(instance);
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInstance is { } instance)
        {
            ClearRequested?.Invoke(instance);
        }
    }
}
