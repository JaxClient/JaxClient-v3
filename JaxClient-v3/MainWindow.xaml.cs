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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace JaxClient_v3;


public partial class MainWindow : Window
{
    private readonly List<RunningInstance> runningInstances = new();
    private readonly List<Process> runningMinecraftInstances = new();
    private string currentTab = "mod"; // default Mods
    private Profile currentProfile;
    private Profile profileToDelete;
    private string filePath;
    private Dictionary<string, string> iconCache = new();
    private string installedModsSearch = "";
    private string installedRPacksSearch = "";
    private string installedShadersSearch = "";
    private bool openedFromInstalledContent = false;
    private bool IsOfflineMode = false;
    private bool ForceOfflineMode = false;
    private DispatcherTimer internetTimer;
    private readonly Random random = new();
    private readonly DispatcherTimer matrixTimer = new();
    private bool isLaunching = false;
    private Process minecraftProcess;
    private Process? currentMinecraftProcess;
    private readonly string matrixCharsSet =
        "01ABCDEFGHIJKLMNOPQRSTUVWXYZ{}[]";
    private bool cancelLaunch = false;
    private CancellationTokenSource? launchCts;
    private readonly List<MatrixDrop> matrixDrops = new();
    private const string CurseForgeApiKey = "COPYRIGHT";
    private string currentSCTab = "content";
    private string currentSCType = "mod";
    private bool taskWidgetManuallyClosed = false;
    private string currentSCSearch = "";
    private DiscordRPC.DiscordRpcClient rpc;
    
    private bool taskWidgetHiddenForSession = false;
    private List<StandardContentEntry>
        standardBrowseContent =
            new();
    
   

    [DllImport("shell32.dll")]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        string AppID);
    public class StandardContentEntry
    {
        public string Name { get; set; }

        public string Platform { get; set; }

        public string Type { get; set; }

        public string IconUrl { get; set; }
    }
    private async void AddStandardContent_Click(
        object sender,
        RoutedEventArgs e)
    {
        AddStandardContentOverlay.Visibility =
            Visibility.Visible;

        // ✅ Immer Mods als Starttab
        currentSCType = "mod";

        // ✅ UI zurücksetzen
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

        // ✅ Suche leeren
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

        // ✅ MODRINTH

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

        // ✅ CURSEFORGE

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

        // ✅ Aktivierte Liste aktualisieren
        ReloadStandardContentList();

        // ✅ Browse-Liste neu laden
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

        // ✅ ICON LADEN

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
    private bool CheckIfInstalled(CurseForgeMod mod)
    {
        string folder = GetCurrentFolder();

        string basePath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            currentProfile.Name,
            folder);

        if (!Directory.Exists(basePath))
            return false;

        foreach (var file in Directory.GetFiles(basePath))
        {
            string installedName =
                NormalizeName(
                    Path.GetFileNameWithoutExtension(file));

            string modName =
                NormalizeName(mod.name);

           

            if (installedName == modName)
                return true;
        }

        return false;
    }
private async Task InstallCurseForgeMod(
    CurseForgeMod mod)
{
    if (currentProfile == null)
        return;

    using HttpClient client = new HttpClient();

    client.DefaultRequestHeaders.Add(
        "x-api-key",
        CurseForgeApiKey);

    Console.WriteLine("========== CURSEFORGE INSTALL ==========");
    Console.WriteLine("MOD NAME: " + mod.name);
    Console.WriteLine("MOD ID: " + mod.id);

    string fileUrl =
        $"https://api.curseforge.com/v1/mods/{mod.id}/files";

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

        return;
    }

    var result =
        JsonSerializer.Deserialize<CurseForgeFilesResponse>(json);

    if (result?.data == null ||
        result.data.Count == 0)
    {
        ShowToast("No files found");
        return;
    }

    Console.WriteLine("FILES FOUND: " + result.data.Count);

    string loader = currentProfile.Loader.ToLower();
    string mcVersion = currentProfile.Version;

    CurseForgeFile file;

    if (currentTab == "mod")
    {
        file = result.data.FirstOrDefault(f =>
        {
            if (f.gameVersions == null)
                return false;

            bool versionMatch =
                f.gameVersions.Any(v =>
                    v.Equals(
                        mcVersion,
                        StringComparison.OrdinalIgnoreCase));

            bool loaderMatch =
                f.gameVersions.Any(v =>
                    v.Contains(
                        loader,
                        StringComparison.OrdinalIgnoreCase));

            return versionMatch && loaderMatch;
        });
    }
    else
    {
        file = result.data.FirstOrDefault(f =>
        {
            if (f.gameVersions == null)
                return false;

            return f.gameVersions.Any(v =>
                v.Equals(
                    mcVersion,
                    StringComparison.OrdinalIgnoreCase));
        });
    }

    if (file == null)
    {
        file = result.data.FirstOrDefault(f =>
            f.gameVersions != null &&
            f.gameVersions.Any(v =>
                v.Equals(
                    mcVersion,
                    StringComparison.OrdinalIgnoreCase)));
    }

    if (file == null)
    {
        ShowToast("No compatible file");
        return;
    }

    Console.WriteLine("SELECTED FILE:");
    Console.WriteLine(file.fileName);

    Console.WriteLine("DOWNLOAD URL:");
    Console.WriteLine(file.downloadUrl);

    if (string.IsNullOrWhiteSpace(file.downloadUrl))
    {
        ShowToast("downloadUrl is null");

        Console.WriteLine("downloadUrl = NULL");

        return;
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

                return;
            }
        }
    }

    string folder = currentTab switch
    {
        "mod" => "mods",
        "resourcepack" => "resourcepacks",
        "shader" => "shaderpacks",
        _ => "mods"
    };

    string modsFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            currentProfile.Name,
            folder);

    Directory.CreateDirectory(modsFolder);

    string ext = currentTab switch
    {
        "mod" => ".jar",
        "resourcepack" => ".zip",
        "shader" => ".zip",
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

        if (modrinthMod != null)
        {
            await InstallMod(
                modrinthMod.project_id,
                modrinthMod.title,
                modrinthMod.description);
        }

        return;
    }
    Console.WriteLine("FILE SAVED");
    AddToProfileJson(
        mod.name,
        mod.summary,
        mod.logo?.url);
    ReloadProfileData();

    await LoadContent();

    ShowToast($"Installed {mod.name}");
}public class CurseForgeFile
{
    public int id { get; set; }
    public string fileName { get; set; }
    public string downloadUrl { get; set; }

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
        using HttpClient client = new HttpClient();

        client.DefaultRequestHeaders.Add(
            "x-api-key",
            CurseForgeApiKey);

        int modLoaderType = 0;

        if (loader.Equals("fabric", StringComparison.OrdinalIgnoreCase))
            modLoaderType = 4;
        else if (loader.Equals("forge", StringComparison.OrdinalIgnoreCase))
            modLoaderType = 1;

        int classId = 6; // Mods

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
                Console.WriteLine(
                    $"CURSEFORGE REQUEST TRY {attempt}/3");

                json = await client.GetStringAsync(url);

                Console.WriteLine(
                    $"CURSEFORGE REQUEST OK (TRY {attempt})");

                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"CURSEFORGE REQUEST FAILED (TRY {attempt})");

                Console.WriteLine(ex);

                if (attempt < 3)
                {
                    Console.WriteLine(
                        "Retrying in 1 second...");

                    await Task.Delay(1000);
                }
                else
                {
                    Console.WriteLine(
                        "CURSEFORGE GAVE UP AFTER 3 TRIES");

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
    private async void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "All Packs (*.jxcpack;*.noriskpack)|*.jxcpack;*.noriskpack"
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
        else
        {
            ShowToast("Unsupported file type.");
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
            StartTask("Importing Jax Pack...");
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

        // ✅ ENTZIP
        System.IO.Compression.ZipFile.ExtractToDirectory(filePath, targetPath);

        Application.Current.Dispatcher.Invoke(() =>
        {
            StopTask("Importing Jax Pack...");
        });
    });

    // ✅ ✅ ✅ METADATA LESEN
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

            var meta = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (meta != null)
            {
                if (meta.ContainsKey("Loader"))
                    loader = meta["Loader"]?.ToString();

                if (meta.ContainsKey("Version"))
                    version = meta["Version"]?.ToString();

                if (meta.ContainsKey("LoaderVersion"))
                    loaderVersion = meta["LoaderVersion"]?.ToString();

                if (meta.ContainsKey("NrcEnabled"))
                    nrcEnabled = Convert.ToBoolean(meta["NrcEnabled"]);
            }
        }
        catch { }
    }

    // ✅ ✅ ✅ MODS → ContentItem
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

    // ✅ ✅ ✅ RESOURCEPACKS → ContentItem
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

    // ✅ ✅ ✅ SHADERPACKS → ContentItem
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

    // ✅ profiles.json Pfad
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

    // ✅ ✅ ✅ PROFILE MIT ALLEM (DEIN MODEL!)
    var profile = new Profile
    {
        Name = Path.GetFileName(targetPath),
        Loader = loader,
        Version = version,
        LoaderVersion = loaderVersion,
        NrcEnabled = nrcEnabled,
        ProfilePicture = GetRandomProfilePicture(),
        Mods = mods,
        RP = resourcepacks,          // ✅ wichtig: RP (nicht Resourcepacks!)
        Shaders = shaderpacks        // ✅ wichtig: Shaders (nicht Shaderpacks!)
    };

    profiles.Add(profile);

    string output = JsonSerializer.Serialize(
        profiles,
        new JsonSerializerOptions { WriteIndented = true }
    );

    File.WriteAllText(jsonPath, output);

    // ✅ UI Refresh
    LoadProfiles();

    ShowToast($"Imported ({mods.Count} mods)");
}    List<ContentItem> installedMods = new List<ContentItem>();

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        if (currentProfile == null)
            return;

        LoadExportItems(currentProfile.Name);
        ExportOverlay.Visibility = Visibility.Visible;
    }
    
    private void LoadExportItems(string profileName)
    {
        ExportList.Children.Clear();

        string basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            profileName
        );

        if (!Directory.Exists(basePath))
            return;

        // ✅ ORDNER
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

            // ✅ WICHTIG: STATE UPDATEN
            cb.Checked += (_, __) => UpdateExportButtonState();
            cb.Unchecked += (_, __) => UpdateExportButtonState();

            ExportList.Children.Add(cb);
        }

        // ✅ DATEIEN
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

            // ✅ WICHTIG: STATE UPDATEN
            cb.Checked += (_, __) => UpdateExportButtonState();
            cb.Unchecked += (_, __) => UpdateExportButtonState();

            ExportList.Children.Add(cb);
        }

        // ✅ INITIAL STATE (wichtig für Toggle Button)
        allSelected = true;
        SelectAllBtn.Content = "Deselect All";

        UpdateExportButtonState();
    }    
    
    private void ExportCancel_Click(object sender, RoutedEventArgs e)
    {
        ExportOverlay.Visibility = Visibility.Collapsed;
    }
    private async void ExportConfirm_Click(object sender, RoutedEventArgs e)
    {
        ExportOverlay.Visibility = Visibility.Collapsed;

        if (currentProfile == null)
            return;

        // ✅ UI DATEN SICHER KOPIEREN
        var selectedItems = new List<string>();

        foreach (CheckBox cb in ExportList.Children)
        {
            if (cb.IsChecked == true)
                selectedItems.Add(cb.Tag.ToString());
        }

        // ✅ THREAD → KEIN UI ZUGRIFF MEHR
        await Task.Run(() =>
        {
            
            ExportSelectedItems(currentProfile.Name, selectedItems);
            
        });

        ShowToast("Export done!");
    }
    private bool allSelected = true;

    private void SelectAllToggle_Click(object sender, RoutedEventArgs e)
    {
        allSelected = !allSelected;

        foreach (CheckBox cb in ExportList.Children)
            cb.IsChecked = allSelected;

        SelectAllBtn.Content = allSelected ? "Deselect All" : "Select All";

        UpdateExportButtonState();
    }
    private void UpdateExportButtonState()
    {
        bool anyChecked = false;

        foreach (CheckBox cb in ExportList.Children)
        {
            if (cb.IsChecked == true)
            {
                anyChecked = true;
                break;
            }
        }

        ExportBtn.IsEnabled = anyChecked;
        ExportBtn.Opacity = anyChecked ? 1.0 : 0.5;
    }
private void ExportSelectedItems(string profileName, List<string> selectedItems)
{
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

    Directory.CreateDirectory(exportFolderDefault);

    string exportPath = null;

    // ✅ UI THREAD (Dialog + StartTask)
    Application.Current.Dispatcher.Invoke(() =>
    {
        StartTask("Export Profile...");

        SaveFileDialog dlg = new SaveFileDialog();
        dlg.Filter = "JXC Pack (*.jxcpack)|*.jxcpack";
        dlg.FileName = profileName + ".jxcpack";
        dlg.InitialDirectory = exportFolderDefault;

        if (dlg.ShowDialog() == true)
        {
            exportPath = dlg.FileName;
        }
    });

    // ❌ Abbruch
    if (string.IsNullOrEmpty(exportPath))
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StopTask("Export Profile...");
        });
        return;
    }

    if (File.Exists(exportPath))
        File.Delete(exportPath);

    using (var archive = System.IO.Compression.ZipFile.Open(exportPath, System.IO.Compression.ZipArchiveMode.Create))
    {
        foreach (var path in selectedItems)
        {
            if (Directory.Exists(path))
            {
                // ✅ Dateien
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(basePath, file);
                    archive.CreateEntryFromFile(file, rel);
                }

                // ✅ 🔥 ALLE ORDNER (auch leere)
                foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(basePath, dir) + "/";
                    archive.CreateEntry(rel);
                }
            }
            else if (File.Exists(path))
            {
                string rel = Path.GetRelativePath(basePath, path);
                archive.CreateEntryFromFile(path, rel);
            }
        }
        

        // ✅ ✅ ✅ METADATA EINMAL AM ENDE

        var meta = new
        {
            Name = profileName,
            Loader = currentProfile.Loader,
            Version = currentProfile.Version,
            LoaderVersion = currentProfile.LoaderVersion,
            NrcEnabled = currentProfile.NrcEnabled  // 🔥 NEU
        };

        string metaJson = JsonSerializer.Serialize(meta, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var entry = archive.CreateEntry("profilemeta.json");

        using (var writer = new StreamWriter(entry.Open()))
        {
            writer.Write(metaJson);
        }
    }

    // ✅ UI THREAD STOP
    Application.Current.Dispatcher.Invoke(() =>
    {
        StopTask("Export Profile...");
    });
}    
private async Task ImportNoriskProfile(string filePath)
{
    try
    {
        await Task.Run(() =>
        {
            Console.WriteLine("=== IMPORT START ===");
            Console.WriteLine("File: " + filePath);

            // ✅ RESET MODS
            installedMods = new List<ContentItem>();

            // ✅ SHADER LISTE
            List<ContentItem> shaders = new List<ContentItem>();

            // ✅ ✅ NEW: RESOURCEPACK LISTE
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

            if (!isFabric && !isForge)
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

            StopTask("Reading profile (2/6)...");
            StartTask("Copying files (3/6)...");

            // ✅ COPY OVERRIDES + REMOVE .disabled
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

            // ✅ SHADER FIX
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

            // ✅ ✅ ✅ NEW: RESOURCEPACK FIX (EXAKT WIE SHADER)
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

// ✅ CurseForge Fallback
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

// zusätzlich
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

                        installedMods.Add(new ContentItem
                        {
                            Name = cleanName,
                            Description = mod.TryGetProperty("version", out var verProp)
                                ? verProp.GetString()
                                : "Imported"
                        });
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

            // ✅ ✅ ✅ HIER IST DER JSON FIX
            profiles.Add(new Profile
            {
                Name = name,
                Loader = isForge ? "Forge" : "Fabric",
                Version = version,
                LoaderVersion = loaderVersion,
                NrcEnabled = true,
                Mods = installedMods,
                ProfilePicture = GetRandomProfilePicture(),
                Shaders = shaders,
                RP = resourcepacks // ✅ NEU
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
        // ✅ FIX: richtige Version
        if (!version.StartsWith("1.21.11"))
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

        Directory.CreateDirectory(modsPath);

        // ✅ TEMP ZIP
        string tempZip = System.IO.Path.Combine(modsPath, "temp_special.zip");

        using HttpClient client = new HttpClient();

        // ✅ FIX: & statt &amp; + zip erzwingen
        string url = "https://www.dropbox.com/scl/fi/rj66n7du1epjynhqmxzem/JaxClient-Mods.zip?rlkey=u1jy3jviude3nzp52dpyb63h5&dl=1";

        // ✅ DOWNLOAD
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

        // ✅ TEMP EXTRACT FOLDER (WICHTIG)
        string tempExtract = System.IO.Path.Combine(modsPath, "_temp_extract");

        if (Directory.Exists(tempExtract))
            Directory.Delete(tempExtract, true);

        Directory.CreateDirectory(tempExtract);

        // ✅ ZIP ENT PACKEN
        System.IO.Compression.ZipFile.ExtractToDirectory(
            tempZip,
            tempExtract,
            true
        );

        // ✅ ALLE DATEIEN INS mods/ ROOT SCHIEBEN
        foreach (var file in Directory.GetFiles(tempExtract, "*.*", SearchOption.AllDirectories))
        {
            string fileName = System.IO.Path.GetFileName(file);
            string target = System.IO.Path.Combine(modsPath, fileName);

            File.Copy(file, target, true); // overwrite ✅
        }

        // ✅ CLEANUP
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
            taskWidgetManuallyClosed = false;

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
        taskWidgetManuallyClosed = false;
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
        Dispatcher.Invoke(() =>
        {
            currentTasks.Remove(name);

            if (currentTasks.Count > 0)
            {
                taskWidgetManuallyClosed = false;
                UpdateTaskUI();
                return;
            }

            TaskStatusWidget.Visibility =
                Visibility.Visible;

            TaskStatusText.Text =
                "✓ Completed";

            TaskProgressBar.IsIndeterminate =
                false;

            TaskProgressBar.Value = 100;
        });

        await Task.Delay(2000);

        Dispatcher.Invoke(() =>
        {
            TaskStatusWidget.Visibility =
                Visibility.Collapsed;

            TaskIdleIcon.Visibility =
                Visibility.Visible;

            UpdateTaskUI();
        });
    }
    private void CloseTaskWidget_Click(
        object sender,
        RoutedEventArgs e)
    {
        taskWidgetManuallyClosed = true;

        
        TaskStatusWidget.Visibility =
            Visibility.Collapsed;

        TaskIdleIcon.Visibility =
            Visibility.Visible;
    }

    private void UpdateTaskUI()
    {
        if (!taskWidgetManuallyClosed)
        {
            TaskStatusWidget.Visibility =
                Visibility.Visible;
        }
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
            currentTasks.Last();

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

            // ✅ Check → verhindert Cloudflare Error HTML
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

        // ✅ FIX: User-Agent setzen
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
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "resource"
        );

        Directory.CreateDirectory(folder);

        string userBody = currentAccountData != null
            ? Path.Combine(folder, $"{currentAccountData.username.ToLower()}_skin.png")
            : null;

        string userOld = currentAccountData != null
            ? Path.Combine(folder, $"{currentAccountData.username.ToLower()}.png")
            : null;

        string stevePath = Path.Combine(folder, "steve.png");

        string pathToLoad = null;

        // ✅ 1. PRIMARY (alter Body)
        if (userOld != null && File.Exists(userOld))
        {
            pathToLoad = userOld;
        }
        // ✅ 2. MINEATAR
        else if (currentAccountData != null)
        {
            try
            {
                if (!File.Exists(userBody))
                {
                    await DownloadBodySkin(
                        currentAccountData.username,
                        currentAccountData.uuid
                    );
                }

                if (File.Exists(userBody))
                    pathToLoad = userBody;
            }
            catch
            {
                // ignorieren → fallback greift
            }
        }

        // ✅ 3. FALLBACK STEVE
        if (pathToLoad == null)
        {
            if (!File.Exists(stevePath))
                await EnsureSteveSkin();

            pathToLoad = stevePath;
        }

        // ✅ FINAL SAFETY (falls alles schief geht)
        if (string.IsNullOrEmpty(pathToLoad) || !File.Exists(pathToLoad))
        {
            SkinImage.Source = null;
            return;
        }

        // ✅ LOAD IMAGE (OHNE CACHE BUG + OHNE ?t= 💀)
        var img = new BitmapImage();
        img.BeginInit();

        img.CacheOption = BitmapCacheOption.OnLoad;
        img.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // ✅ wichtig
        img.UriSource = new Uri(pathToLoad, UriKind.Absolute);

        img.EndInit();
        img.Freeze();

        SkinImage.Source = null;
        SkinImage.Source = img;
    }
    catch (Exception ex)
    {
        Console.WriteLine("Skin load error: " + ex.Message);

        // ✅ HARD FALLBACK → STEVE
        try
        {
            string stevePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource",
                "steve.png"
            );

            if (File.Exists(stevePath))
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(stevePath, UriKind.Absolute);
                img.EndInit();
                img.Freeze();

                SkinImage.Source = img;
            }
            else
            {
                SkinImage.Source = null;
            }
        }
        catch
        {
            SkinImage.Source = null;
        }
    }
}
    
   
    private async Task StartUpdater()
    {
        try
        
        {
            
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string updaterPath = Path.Combine(baseDir, "Updater.exe");
            string versionFile = Path.Combine(baseDir, "version.json");

            bool startUpdater = false;

            if (File.Exists(versionFile))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(versionFile);

                    using JsonDocument doc = JsonDocument.Parse(json);

                    string version =
                        doc.RootElement.GetProperty("version")
                            .GetString() ?? "0.0.0";

                    if (version == "3.1.15")
                    {
                        startUpdater = true;
                    }
                    // 3.5, 3.14, 4.8 usw.
                    if (version.Split('.').Length == 2)
                    {
                        startUpdater = true;
                    }
                }
                catch
                {
                    startUpdater = true;
                }
            }

           
            if (startUpdater)
            {
                StartTask("Downloading updater...");

                using HttpClient client = new();

                string updaterUrl =
                    "https://github.com/Jaxroxnyt/JaxClient-v3/releases/download/0.0.1/Updater-v2.exe";

                byte[] data = await client.GetByteArrayAsync(updaterUrl);

                if (File.Exists(updaterPath))
                {
                    try
                    {
                        File.Delete(updaterPath);
                    }
                    catch
                    {
                    }
                }
                await File.WriteAllBytesAsync(updaterPath, data);

                StopTask("Downloading updater...");

                try
                {
                    string desktop =
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.DesktopDirectory);

                   

                    foreach (string shortcut in Directory.GetFiles(desktop, "*.lnk"))
                    {
                        string name = Path.GetFileNameWithoutExtension(shortcut);

                        if (!name.Contains("JaxClient",
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments =
                                $"-Command \"$s=(New-Object -COM WScript.Shell).CreateShortcut('{shortcut}');$s.TargetPath='{updaterPath}';$s.WorkingDirectory='{baseDir}';$s.Save()\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }

// TASKLEISTE PINS ENTFERNEN
                    // TASKLEISTE PINS ENTFERNEN
                    try
                    {
                        string pinnedPath =
                            Path.Combine(
                                Environment.GetFolderPath(
                                    Environment.SpecialFolder.ApplicationData),
                                @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

                        if (Directory.Exists(pinnedPath))
                        {
                            foreach (string pinned in Directory.GetFiles(pinnedPath, "*.lnk"))
                            {
                                try
                                {
                                    dynamic shell =
                                        Activator.CreateInstance(
                                            Type.GetTypeFromProgID("WScript.Shell")!);

                                    dynamic link =
                                        shell.CreateShortcut(pinned);

                                    string target =
                                        link.TargetPath ?? "";

                                    if (!target.Contains("JaxClient",
                                            StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    File.Delete(pinned);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }   
    
    private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
    {
        if (profileToDelete == null)
            return;

        if (!File.Exists(filePath))
            return;

        // ✅ JSON laden
        string json = File.ReadAllText(filePath);
        var profiles = JsonSerializer.Deserialize<List<Profile>>(json) ?? new List<Profile>();

        profiles.RemoveAll(p => p.Name == profileToDelete.Name);

        string output = JsonSerializer.Serialize(
            profiles,
            new JsonSerializerOptions { WriteIndented = true }
        );

        File.WriteAllText(filePath, output);

        // ✅ ✅ ✅ NEU: ORDNER LÖSCHEN
        try
        {
            string profileFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "Profiles",
                profileToDelete.Name
            );

            if (Directory.Exists(profileFolder))
            {
                Directory.Delete(profileFolder, true); // 🔥 true = alles rekursiv löschen
            }
        }
        catch (Exception ex)
        {
            ShowToast("Folder delete failed!");
        }

        // ✅ UI
        ConfirmOverlay.Visibility = Visibility.Collapsed;
        profileToDelete = null;
        LoadLastProfile();
        LoadProfiles();

        ShowToast("Profile deleted!");
    }

      
    private bool editNrcEnabled;

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
private async void LoaderBox_Changed(object sender, SelectionChangedEventArgs e)
{
    ComboBox loaderBox = sender as ComboBox;
    if (loaderBox == null)
        return;

    ComboBox targetVersionBox = null;

    // ✅ CHECK WELCHER COMBOBOX
    if (loaderBox == LoaderBox)
        targetVersionBox = LoaderVersionBox;
    else if (loaderBox == EditLoaderBox)
        targetVersionBox = EditLoaderVersionBox;

    if (targetVersionBox == null)
        return;

    if (loaderBox.SelectedItem is not ComboBoxItem item)
        return;

    string loader = item.Content?.ToString();

    if (string.IsNullOrWhiteSpace(loader))
        return;

    targetVersionBox.Items.Clear();
    targetVersionBox.Items.Add(new ComboBoxItem { Content = "Loading..." });

    try
    {
        using HttpClient client = new HttpClient();

        string json;

        if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
        {
            json = await client.GetStringAsync(
                "https://meta.fabricmc.net/v2/versions/loader");
        }
        else if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
        {
            json = await client.GetStringAsync(
                "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
        }
        else
        {
            return;
        }

        targetVersionBox.Items.Clear();

        if (loader == "Fabric")
        {
            using JsonDocument doc = JsonDocument.Parse(json);

            foreach (var entry in doc.RootElement.EnumerateArray().Take(20))
            {
                targetVersionBox.Items.Add(new ComboBoxItem
                {
                    Content = entry.GetProperty("version").GetString()
                });
            }
        }
        else
        {
            using JsonDocument doc = JsonDocument.Parse(json);

            foreach (var forge in doc.RootElement
                         .GetProperty("promos")
                         .EnumerateObject())
            {
                targetVersionBox.Items.Add(new ComboBoxItem
                {
                    Content = forge.Value.GetString()
                });
            }
        }

        targetVersionBox.SelectedIndex = 0;
    }
    catch
    {
        targetVersionBox.Items.Clear();

        targetVersionBox.Items.Add(new ComboBoxItem
        {
            Content = "Error loading"
        });
    }

    // ✅ RESET
    targetVersionBox.Items.Clear();
    targetVersionBox.Items.Add(new ComboBoxItem { Content = "Loading..." });

try
{
    using HttpClient client = new HttpClient();

    targetVersionBox.Items.Clear();

    targetVersionBox.Items.Add(new ComboBoxItem
    {
        Content = "Loading..."
    });

    string loaderName =
        (loaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

    ComboBox versionBox =
        loaderBox == LoaderBox
            ? VersionBox
            : EditVersionBox;

    string mcVersion =
        (versionBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

    targetVersionBox.Items.Clear();

    if (loaderName == "Fabric")
    {
        string json = await client.GetStringAsync(
            "https://meta.fabricmc.net/v2/versions/loader");

        using JsonDocument doc = JsonDocument.Parse(json);

        int count = 0;

        foreach (JsonElement entry in doc.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("version", out JsonElement versionProp))
                continue;

            string version = versionProp.GetString();

            if (string.IsNullOrWhiteSpace(version))
                continue;

            targetVersionBox.Items.Add(new ComboBoxItem
            {
                Content = version
            });

            count++;

            if (count >= 20)
                break;
        }
    }
    else if (loaderName == "Forge")
    {
        string xml = await client.GetStringAsync(
            "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");

        var matches = Regex.Matches(
            xml,
            @"<version>(.*?)</version>");

        HashSet<string> added = new();

        foreach (Match match in matches.Cast<Match>().Reverse())
        {
            string forgeVersion = match.Groups[1].Value.Trim();

            if (string.IsNullOrWhiteSpace(forgeVersion))
                continue;

            // Nur passende MC Version
            if (!string.IsNullOrWhiteSpace(mcVersion))
            {
                if (!forgeVersion.StartsWith(mcVersion + "-"))
                    continue;
            }

            string lower = forgeVersion.ToLower();

            // Keine Pre/Beta/RC
            if (lower.Contains("pre") ||
                lower.Contains("rc") ||
                lower.Contains("beta"))
                continue;

            if (!added.Add(forgeVersion))
                continue;

            targetVersionBox.Items.Add(new ComboBoxItem
            {
                Content = forgeVersion
            });
        }
    }

    if (targetVersionBox.Items.Count > 0)
    {
        targetVersionBox.SelectedIndex = 0;
    }
    else
    {
        targetVersionBox.Items.Add(new ComboBoxItem
        {
            Content = "No versions found"
        });

        targetVersionBox.SelectedIndex = 0;
    }
}
catch (Exception ex)
{
    targetVersionBox.Items.Clear();

    targetVersionBox.Items.Add(new ComboBoxItem
    {
        Content = "Error loading"
    });

   
}
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
    
    private void LoadProfileSelector()
    {
        ProfileSelectList.Children.Clear();

        if (!File.Exists(filePath))
            return;

        var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));

        if (profiles == null)
            return;

        foreach (var p in profiles)
        {
            Button btn = new Button
            {
                Content = $"{p.Name} ({p.Loader} {p.Version})",
                Margin = new Thickness(0, 5, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Height = 40,
                Cursor = Cursors.Hand
            };

// ✅ 👉 DAS ENTFERNT DEN WEISSEN HOVER KOMPLETT
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

        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }    private void SelectProfile(Profile profile)
    {
        currentProfile = profile;

        // ✅ Text im Launch Button aktualisieren
        var txt = (TextBlock)LaunchBtn.Template.FindName("SelectedProfileText", LaunchBtn);

        if (txt != null)
        {
            txt.Text = $"{profile.Name} ({profile.Loader} {profile.Version})";
        }

        ProfileSelectOverlay.Visibility = Visibility.Collapsed;
        SaveLastProfile(profile.Name);
        
    }

    private void OpenProfile(Profile profile)
    {
        
        // Ordner erstellen (wie oben)
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

// ✅ DEFAULT TAB IMMER MOD
        currentTab = "mod";

// ✅ VISUAL RESET
        ModsTab.Foreground = Brushes.White;
        RPacksTab.Foreground = Brushes.Gray;
        ShadersTab.Foreground = Brushes.Gray;

        ModsLine.Visibility = Visibility.Visible;
        RPacksLine.Visibility = Visibility.Collapsed;
        ShadersLine.Visibility = Visibility.Collapsed;

// ✅ LADEN
        
        // Pages wechseln
        ProfilesPage.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Visible;

        ProfileTitle.Text = profile.Name;
        ProfileSubInfo.Text = $"{profile.Loader} {profile.LoaderVersion} • {profile.Version}";
        ReloadProfileData();
       
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        ProfileDetailPage.Visibility = Visibility.Collapsed;
        ProfilesPage.Visibility = Visibility.Visible;
    }
    private void CB_TabSwitch_Click(object sender, RoutedEventArgs e)
    {
        // RESET
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
        // ✅ RESET VISUAL
        ModsTab.Foreground = Brushes.Gray;
        RPacksTab.Foreground = Brushes.Gray;
        ShadersTab.Foreground = Brushes.Gray;

        ModsLine.Visibility = Visibility.Collapsed;
        RPacksLine.Visibility = Visibility.Collapsed;
        ShadersLine.Visibility = Visibility.Collapsed;

        Button clicked = (Button)sender;

        // ✅ TAB SWITCH + STATE
        if (clicked == ModsTab)
        {
            currentTab = "mod";
            ModsTab.Foreground = Brushes.White;
            ModsLine.Visibility = Visibility.Visible;

            // 🔍 Suche wiederherstellen
            InstalledSearchBox.Text = installedModsSearch;
        }
        else if (clicked == RPacksTab)
        {
            currentTab = "resourcepack";
            RPacksTab.Foreground = Brushes.White;
            RPacksLine.Visibility = Visibility.Visible;

            // 🔍 Suche wiederherstellen
            InstalledSearchBox.Text = installedRPacksSearch;
        }
        else if (clicked == ShadersTab)
        {
            currentTab = "shader";
            ShadersTab.Foreground = Brushes.White;
            ShadersLine.Visibility = Visibility.Visible;

            // 🔍 Suche wiederherstellen
            InstalledSearchBox.Text = installedShadersSearch;
        }

        // ✅ OPTIONAL: Placeholder ändern
        InstalledSearchBox.ToolTip = currentTab switch
        {
            "mod" => "Search Mods...",
            "resourcepack" => "Search Resourcepacks...",
            "shader" => "Search Shaderpacks...",
            _ => "Search..."
        };

        // ✅ reload content
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
    currentProfile = profiles.FirstOrDefault(p => p.Name == currentProfile.Name);

    if (currentProfile == null)
        return;

    // ✅ ITEMS PRO TAB
    List<ContentItem> items = currentTab switch
    {
        "mod" => currentProfile.Mods,
        "resourcepack" => currentProfile.RP,
        "shader" => currentProfile.Shaders,
        _ => null
    };

    if (items == null)
        return;

    // ✅ 🔍 SEARCH AUS TAB HOLEN
    string search = currentTab switch
    {
        "mod" => installedModsSearch,
        "resourcepack" => installedRPacksSearch,
        "shader" => installedShadersSearch,
        _ => ""
    };

    foreach (var item in items)
    {
        // ✅ 🔥 FILTER
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

        // ✅ GRID
        Grid grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

        Image icon = new Image
        {
            Width = 48,
            Height = 48,
            Stretch = Stretch.UniformToFill,
            Margin = new Thickness(0,0,12,0)
        };
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
        //grid.Children.Add(icon);
        StackPanel textStack = new StackPanel();

        bool isDisabled = IsFileDisabled(item.Name);

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

        textStack.Children.Add(title);
        textStack.Children.Add(desc);

        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        // ✅ TOGGLE BUTTON
        Button toggleBtn = new Button
        {
            Content = isDisabled ? "Enable" : "Disable",
            Width = 75,
            Height = 30,
            Background = (Brush)new BrushConverter().ConvertFromString(
                isDisabled ? "#22C55E" : "#F59E0B"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };

        MakeRounded(toggleBtn);

        toggleBtn.Click += (s, e) =>
        {
            ToggleMod(item);
        };

        toggleBtn.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(toggleBtn, 2);
        grid.Children.Add(toggleBtn);

        // ✅ DELETE BUTTON
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

        deleteBtn.Click += (s, e) =>
        {
            DeleteInstalledItem(item);
        };

        Grid.SetColumn(deleteBtn, 3);
        grid.Children.Add(deleteBtn);

        card.Child = grid;
        InstalledContentList.Items.Add(card);
        card.Cursor = Cursors.Hand;

        card.MouseLeftButtonUp += async (s, e) =>
        {
            // Klicks auf Buttons ignorieren
            if (e.OriginalSource is Button)
                return;

            await OpenInstalledContentDetails(item.Name);
        };
    }

    // ✅ EMPTY STATE
    if (InstalledContentList.Items.Count == 0)
    {
        InstalledScroll.Visibility = Visibility.Collapsed;
        NoContentCard.Visibility = Visibility.Visible;

        if (currentTab == "mod")
            NoContentText.Text = string.IsNullOrWhiteSpace(installedModsSearch)
                ? "No mods installed"
                : "No results found";

        else if (currentTab == "resourcepack")
            NoContentText.Text = string.IsNullOrWhiteSpace(installedRPacksSearch)
                ? "No resourcepacks installed"
                : "No results found";

        else if (currentTab == "shader")
            NoContentText.Text = string.IsNullOrWhiteSpace(installedShadersSearch)
                ? "No shaderpacks installed"
                : "No results found";
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

    private void OpenFileAndInstall(string type)
    {
        CustomPopup.IsOpen = false;

        var dialog = new OpenFileDialog
        {
            Filter = "Supported Files|*.jar;*.zip"
        };

        if (dialog.ShowDialog() != true)
            return;

        InstallCustomContent(dialog.FileName, type);
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
   
    private void InstallCustomContent(string filePath, string type)
    {
        if (currentProfile == null)
            return;

        try
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "Profiles",
                currentProfile.Name
            );

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
            string cleanName =
                NormalizeName(
                    Path.GetFileNameWithoutExtension(
                        fileName));
            string destFile = Path.Combine(targetPath, fileName);

            // ✅ Datei kopieren
            File.Copy(filePath, destFile, true);

            // ✅ IN JSON SPEICHERN (super wichtig!!)
            AddToProfileJson(
                cleanName,
                "Custom Content");

            // ✅ UI refresh
            ReloadProfileData();
            LoadInstalledContent();

            ShowToast($"Added {fileName}");
        }
        catch (Exception ex)
        {
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

    if (!File.Exists(filePath))
    {
        UpdateProfilesUI(); // 🔥 HIER
        return;
    }

    string json = File.ReadAllText(filePath);
    var profiles = JsonSerializer.Deserialize<List<Profile>>(json);

    if (profiles == null)
        return;

    foreach (var p in profiles)
    {
        // ✅ OUTER CARD (Modernes Styling mit Accent Border & Hover-Effekt)
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

        // Container Hover-Effekt (Rahmen leuchtet leicht auf)
        container.MouseEnter += (s, e) => container.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#3B82F6");
        container.MouseLeave += (s, e) => container.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#334155");

        // ✅ INNER GRID
        Grid innerGrid = new Grid();
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) }); // Icon
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // Buttons

        // ✅ PROFILBILD (Abgerundet mit Ellipse-Maske)
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

        // Rounded Corners für Bild
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

        // ✅ TITEL
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

        // ✅ SUBTEXT (Loader + Version)
        TextBlock sub = new TextBlock
        {
            Text = $"{p.Loader} • {p.Version}",
            Foreground = (Brush)new BrushConverter().ConvertFromString("#94A3B8"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetRow(sub, 1);
        Grid.SetColumn(sub, 1);
        innerGrid.Children.Add(sub);

        // ✅ CLICK AREA (Klickbare Fläche über Text)
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

        // ✅ ACTION PANEL (✏️ + 🗑)
        StackPanel actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        // ✏️ EDIT BUTTON
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

        // CLICK → EDIT PANEL ÖFFNEN
        editBtn.Click += (s, e) =>
        {
            if (IsOfflineMode)
            {
                ShowToast("You must be online to edit profiles.");
                return;
            }
            currentProfile = p;

            // ✅ OVERLAY ÖFFNEN
            EditProfileOverlay.Visibility = Visibility.Visible;

            // ✅ NAME
            EditNameBox.Text = p.Name;

            // ✅ LOADER
            EditLoaderBox.SelectedIndex = 0; // aktuell nur Fabric

            // ✅ VERSION
            foreach (ComboBoxItem item in EditVersionBox.Items)
            {
                if (item.Content.ToString() == p.Version)
                {
                    EditVersionBox.SelectedItem = item;
                    break;
                }
            }

            // ✅ LOADER VERSION
            foreach (ComboBoxItem item in EditLoaderVersionBox.Items)
            {
                if (item.Content.ToString() == p.LoaderVersion)
                {
                    EditLoaderVersionBox.SelectedItem = item;
                    break;
                }
            }

            // ✅ NRC TOGGLE
            editNrcEnabled = p.NrcEnabled;

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
        };

        // 🗑 DELETE BUTTON
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

        // ✅ REIHENFOLGE → EDIT LINKS, DELETE RECHTS
        actions.Children.Add(editBtn);
        actions.Children.Add(deleteBtn);

        // ✅ GRID POSITION
        Grid.SetRowSpan(actions, 2);
        Grid.SetColumn(actions, 2);
        innerGrid.Children.Add(actions);

        // ✅ FINAL
        container.Child = innerGrid;
        ProfilesList.Children.Add(container);
    }
    UpdateProfilesUI();
}    private void DeleteProfile(Profile profile)
{
    profileToDelete = profile;

    ConfirmText.Text = $"Delete profile '{profile.Name}'?";

    ConfirmOverlay.Visibility = Visibility.Visible;
}
   
    
    
private async Task DownloadAssets(string version)
{
    
    
    try
    {
        using HttpClient client = new HttpClient();

        // ✅ Manifest
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

            return;
        }

        // ✅ Version JSON
        string versionJson = await client.GetStringAsync(versionUrl);
        using JsonDocument versionDoc = JsonDocument.Parse(versionJson);

        var assetIndex = versionDoc.RootElement.GetProperty("assetIndex");

        string assetUrl = assetIndex.GetProperty("url").GetString();
        string assetId = assetIndex.GetProperty("id").GetString();

        if (string.IsNullOrEmpty(assetUrl))
        {
            
            ShowToast("Asset download error!");

            return;
        }

        // ✅ Pfade
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

        // ✅ Index laden + speichern
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

                // ✅ finaler echter Asset Pfad
                string fullPath = System.IO.Path.Combine(basePath, "objects", sub, hash);

                // ✅ ✅ WICHTIG: GLOBAL CHECK
                if (File.Exists(fullPath))
                    continue;

                // ✅ Download nur wenn fehlt
                string url = $"https://resources.download.minecraft.net/{sub}/{hash}";

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));

                ThrowIfCancelled();

                byte[] data = await client.GetByteArrayAsync(url);

                ThrowIfCancelled();
                File.WriteAllBytes(fullPath, data);

                counter++;
            }
            catch
            {
                // ignore
            }
        }



    }
    catch (Exception ex)
    {
        ShowToast("Asset download error!");
        
    }
    finally
    {
        
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

        // ✅ HELPER FUNCTION
        async Task InstallJava(string version, string url)
        {
            string extractPath = System.IO.Path.Combine(basePath, $"jdk-{version}");
            string javaExePath = System.IO.Path.Combine(extractPath, "bin", "java.exe");

            // ✅ schon installiert?
            if (File.Exists(javaExePath))
            {
                Console.WriteLine($"✅ Java {version} already installed");
                return;
            }

            // Falls der Ordner existiert, aber java.exe fehlt -> Reste löschen
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }

            Console.WriteLine($"⬇ Downloading Java {version}...");

            string zipPath = System.IO.Path.Combine(basePath, $"java{version}.zip");

            // ✅ Download
            if (!File.Exists(zipPath))
            {
                ThrowIfCancelled();

                byte[] data = await client.GetByteArrayAsync(url);

                ThrowIfCancelled();
                File.WriteAllBytes(zipPath, data);
            }

            // Vor dem Entpacken bestehende Ordner scannen, um den neuen zu identifizieren
            var dirsBefore = Directory.GetDirectories(basePath);

            // ✅ Entpacken
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, basePath, true);

            // Nach dem Entpacken scannen
            var dirsAfter = Directory.GetDirectories(basePath);

            // Den neu hinzugekommenen Ordner finden
            string extractedDir = null;
            foreach (var dir in dirsAfter)
            {
                if (!System.Linq.Enumerable.Contains(dirsBefore, dir) && !dir.EndsWith($"jdk-{version}"))
                {
                    extractedDir = dir;
                    break;
                }
            }

            // ✅ Umbenennen in jdk-VERSION
            if (extractedDir != null && Directory.Exists(extractedDir))
            {
                Directory.Move(extractedDir, extractPath);
            }

            // Zip nach erfolgreichem Entpacken aufräumen
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            // ✅ FINAL CHECK
            if (File.Exists(javaExePath))
            {
                Console.WriteLine($"✅ Java {version} installed");
            }
            else
            {
                ShowToast($"Java {version} install failed!");
            }
        }

        // ✅ INSTALLIEREN (ALLE VERSIONEN)
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
}private async Task DownloadFabricLibraries(string mcVersion, string loaderVersion)
    {
        try
        {
            if (cancelLaunch)
            {
                ResetLaunchUI();
                return;
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

                // ✅ split
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

                if (File.Exists(fullPath))
                    continue;
                
               ThrowIfCancelled(); 
                byte[] data = await client.GetByteArrayAsync(downloadUrl);
                ThrowIfCancelled();
                File.WriteAllBytes(fullPath, data);

                
            }

            
        }
        catch (Exception ex)
        {
            ShowToast(ex.ToString());
        }
    }
private async Task DownloadMinecraftClient(string version)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return;
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

        // ✅ ✅ CHECK: JAR existiert?
        if (File.Exists(jarPath))
        {
            
            return;
        }

        using HttpClient client = new HttpClient();

        // ✅ Manifest laden
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
            return;
        }

        // ✅ Version JSON holen
        string versionJson = await client.GetStringAsync(versionUrl);
        using JsonDocument versionDoc = JsonDocument.Parse(versionJson);

        string clientUrl = versionDoc
            .RootElement
            .GetProperty("downloads")
            .GetProperty("client")
            .GetProperty("url")
            .GetString();

        Directory.CreateDirectory(path);

        // ✅ DOWNLOAD
        ThrowIfCancelled();
        byte[] jar = await client.GetByteArrayAsync(clientUrl);
        ThrowIfCancelled();
        File.WriteAllBytes(jarPath, jar);

        // ✅ FINAL CHECK
        if (File.Exists(jarPath))
            Console.Write($"Minecraft {version} installed!");
        else
            ShowToast("Minecraft install failed!");
    }
    catch (Exception ex)
    {
        ShowToast("Minecraft download failed");
        MessageBox.Show(ex.ToString());
    }
}
private async Task DownloadForgeProfile(string mcVersion, string forgeVersion)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return;
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

        

        
    }
    catch (Exception ex)
    {
        Console.WriteLine("=== FORGE INSTALL ERROR ===");
        Console.WriteLine(ex);

        
    }
}private async Task DownloadForgeLibraries(string mcVersion, string forgeVersion)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return;
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

        if (!File.Exists(forgePath))
        {
            ThrowIfCancelled();
            byte[] data = await client.GetByteArrayAsync(universalUrl);
            ThrowIfCancelled();
            await File.WriteAllBytesAsync(forgePath, data);
        }
    }
    catch (Exception ex)
    {
        ShowToast("Forge libraries failed!");
        Console.WriteLine(ex);
    }
}
private async Task DownloadLibraries(string version)
{
    try
    {
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return;
        }

        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "meta",
            "libraries",
            version
        );

        // ✅ ✅ CHECK: Ordner schon da?
        if (Directory.Exists(basePath) && Directory.GetFiles(basePath, "*", SearchOption.AllDirectories).Length > 50)
        {
            
            return;
        }

        using HttpClient client = new HttpClient();

        // ✅ Manifest holen
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
            return;
        }

        // ✅ Version JSON holen
        string versionJson = await client.GetStringAsync(versionUrl);
        using JsonDocument versionDoc = JsonDocument.Parse(versionJson);

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

                if (File.Exists(fullPath))
                    continue;
                ThrowIfCancelled();
                byte[] data = await client.GetByteArrayAsync(url);
                ThrowIfCancelled();
                File.WriteAllBytes(fullPath, data);
            }
            catch
            {
                // ignore einzelne libs
            }
        }

        
    }
    catch (Exception ex)
    {
        ShowToast(ex.ToString());
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

            // ✅ Zielordner
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "meta",
                "versions",
                mcVersion
            );

            Directory.CreateDirectory(path);

            // =============================
            // ✅ JSON DOWNLOAD
            // =============================
            string jsonUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";

            string json = await client.GetStringAsync(jsonUrl);

            string jsonFile = System.IO.Path.Combine(
                path,
                $"fabric-loader-{loaderVersion}-{mcVersion}.json"
            );

            File.WriteAllText(jsonFile, json);

            // =============================
            // ✅ JAR DOWNLOAD
            // =============================
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

        // WICHTIG
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
    }    private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Doppelklick blockieren
        if (e.ClickCount > 1)
            return;

        // Maximierung rückgängig machen falls Windows es versucht
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
            SearchBox.Text = "Search...";
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

        // ✅ CHECKBOX STATE
        bool showInstalled = ShowInstalledCheck.IsChecked == true;

        string facets;

        // ✅ FILTER JE TAB
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
            installBtn.Content = "Installed";
            installBtn.Background = Brushes.Gray;
            installBtn.Foreground = Brushes.White;
            installBtn.IsEnabled = false;
        }
        else
        {
            installBtn.Content = "Install";

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

                string[] frames =
                {
                    "◐",
                    "◓",
                    "◑",
                    "◒"
                };

                bool running = true;

                _ = Task.Run(async () =>
                {
                    int i = 0;

                    while (running)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            installBtn.Content =
                                frames[i % frames.Length];
                        });

                        i++;

                        await Task.Delay(120);
                    }
                });

                await InstallCurseForgeMod(mod);

                running = false;

                installBtn.Content = "Installed";

                installBtn.Background =
                    new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#374151"));

                installBtn.Foreground =
                    Brushes.White;

                installBtn.IsEnabled = false;
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
        if (currentProfile == null)
            return false;

        string folder = currentTab switch
        {
            "mod" => "mods",
            "resourcepack" => "resourcepacks",
            "shader" => "shaderpacks",
            _ => "mods"
        };

        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            currentProfile.Name,
            folder
        );

        if (!Directory.Exists(basePath))
            return false;

        string ext = GetFileExtension();

        // ✅ alle Dateien holen
        var files = Directory.GetFiles(basePath, "*" + ext);

        foreach (var file in files)
        {
            string installedName =
                NormalizeName(
                    Path.GetFileNameWithoutExtension(file));

            string modName =
                NormalizeName(mod.title);

            

            if (installedName == modName)
                return true;
        }

        return false;
    }
    private void BackToProfile_Click(object sender, RoutedEventArgs e)
    {
        ContentBrowserPage.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Visible;

        // ✅ HARD RESET
        currentTab = "mod";

        // ✅ UI RESET (WICHTIG)
        ModsTab.Foreground = Brushes.White;
        RPacksTab.Foreground = Brushes.Gray;
        ShadersTab.Foreground = Brushes.Gray;

        ModsLine.Visibility = Visibility.Visible;
        RPacksLine.Visibility = Visibility.Collapsed;
        ShadersLine.Visibility = Visibility.Collapsed;

        // ✅ CLEAN RELOAD (RICHTIG REIHENFOLGE)
        // ✅ CLEAN RELOAD (RICHTIG REIHENFOLGE)
        ReloadProfileData();

        InstalledContentList.Items.Clear(); // ✅ wichtig gegen alte Anzeige
        LoadInstalledContent();
    }
    
    private async Task OpenCurseForgeInstalledContent(
        string modName)
    {
        using HttpClient client = new HttpClient();

        client.DefaultRequestHeaders.Add(
            "x-api-key",
            CurseForgeApiKey);

        string url =
            $"https://api.curseforge.com/v1/mods/search" +
            $"?gameId=432" +
            $"&searchFilter={Uri.EscapeDataString(modName)}" +
            $"&pageSize=1";

        string json =
            await client.GetStringAsync(url);

        var result =
            JsonSerializer.Deserialize<CurseForgeResponse>(
                json);

        if (result?.data == null ||
            result.data.Count == 0)
        {
            DetailTitle.Text = modName;
            DetailDescription.Text = "Not found!";
            DescriptionImagesPanel.Children.Clear();

            ContentBrowserPage.Visibility =
                Visibility.Collapsed;

            ContentDetailPage.Visibility =
                Visibility.Visible;

            return;
        }

        await OpenCurseForgeDetails(
            result.data[0]);
    }
    public class CurseForgeDescriptionResponse
    {
        public string data { get; set; }
    }
    private async Task OpenInstalledContentDetails(string modName)
    {
        openedFromInstalledContent = true;

            ProfileDetailPage.Visibility = Visibility.Collapsed;

            ContentBrowserPage.Visibility = Visibility.Collapsed;
            ContentDetailPage.Visibility = Visibility.Visible;

            
        
        using HttpClient client = new();

        string url =
            $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(modName)}";

        string json =
            await client.GetStringAsync(url);

        JsonDocument doc =
            JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("hits", out var hits) ||
            hits.GetArrayLength() == 0)
        {
            await OpenCurseForgeInstalledContent(modName);
            return;
        }

        var first = hits[0];

        ModrinthProject mod = new ModrinthProject
        {
            project_id = first.GetProperty("project_id").GetString(),
            title = first.GetProperty("title").GetString(),
            description = first.GetProperty("description").GetString(),
            icon_url = first.GetProperty("icon_url").GetString()
        };

        await OpenModDetails(mod);
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

    string resourceFolder =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            ".JAXSTORE",
            "JaxClient",
            "resource");

    Directory.CreateDirectory(resourceFolder);

    // ...
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

    // Alle HTML-Tags entfernen
    body = Regex.Replace(
        body,
        @"<[^>]+>",
        "",
        RegexOptions.IgnoreCase);
    // img tags entfernen
    body = Regex.Replace(
        body,
        "<img[^>]*>",
        "",
        RegexOptions.IgnoreCase);

    // Markdown Links hübscher
    body = Regex.Replace(
        body,
        @"\[(.*?)\]\((.*?)\)",
        "$1");

    body = body.Replace("### ", "");
    body = body.Replace("## ", "");
    body = body.Replace("# ", "");
// Bilder raus
    body = Regex.Replace(
        body,
        "<img[^>]*>",
        "",
        RegexOptions.IgnoreCase);

// Links url -> Text
    body = Regex.Replace(
        body,
        @"\[(.*?)\]\((.*?)\)",
        "$1");

// HTML-Tags entfernen
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

// Markdown Überschriften
    body = body.Replace("### ", "");
    body = body.Replace("## ", "");
    body = body.Replace("# ", "");

// Fett/Kursiv Sterne
    body = body.Replace("***", "");
    body = body.Replace("**", "");
    body = body.Replace("*", "");

// Blockquotes
    body = body.Replace("> ", "");

// HTML Entities
    body = System.Net.WebUtility.HtmlDecode(body);

// Zu viele Leerzeilen entfernen
    body = Regex.Replace(
        body,
        @"(\r?\n){3,}",
        "\n\n");

    DetailDescription.Text = body.Trim();
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
    DetailDescription.Text = "Loading description...";

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
    private void ContentBackBtn_Click(
        object sender,
        RoutedEventArgs e)
    {
        // ✅ Alles leeren
        DetailTitle.Text = "";
        DetailDescription.Text = "";
        DetailIcon.Source = null;

        DescriptionImagesPanel.Children.Clear();
        GalleryPanel.Children.Clear();

        FullImage.Source = null;
        ImageModal.Visibility = Visibility.Collapsed;

        ContentDetailPage.Visibility = Visibility.Collapsed;

        if (openedFromInstalledContent)
        {
            ProfileDetailPage.Visibility = Visibility.Visible;
        }
        else
        {
            ContentBrowserPage.Visibility = Visibility.Visible;
        }

        openedFromInstalledContent = false;
    }
// Modal schließen
    // Klick auf den Button (X: Button)
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ImageModal.Visibility = Visibility.Collapsed;
    }

// Klick auf den abgedunkelten Hintergrund
    private void ImageModal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ImageModal.Visibility = Visibility.Collapsed;
    }

// Verhindert Schließen beim Klick auf das Bild/Border selbst
    private void PreventModalClose_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

// Verhindert Schließen, wenn direkt auf das Bild geklickt wird
   
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

    // ✅ Rows
    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

    // ✅ Columns
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // Icon
    grid.ColumnDefinitions.Add(new ColumnDefinition()); // Text
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // Button

    // ✅ TITLE
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

    // ✅ BUTTON
    Button installBtn = new Button
    {
        Width = 100,
        Height = 32,
        BorderThickness = new Thickness(0),
        Cursor = Cursors.Hand,
        FontWeight = FontWeights.SemiBold
    };

    // ✅ STYLE SETZEN JE NACH STATUS
    if (isInstalled)
    {
        installBtn.Content = "Installed";
        installBtn.Background = Brushes.Gray;
        installBtn.Foreground = Brushes.White;
        installBtn.IsEnabled = false;
    }
    else
    {
        installBtn.Content = "Install";

        installBtn.Background =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#22C55E"));

        installBtn.Foreground = Brushes.White;

        // ✅ HOVER
        installBtn.MouseEnter += (s, e) =>
            installBtn.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#16A34A"));

        installBtn.MouseLeave += (s, e) =>
            installBtn.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#22C55E"));

        // ✅ CLICK
        installBtn.Click += async (s, e) =>
        {
            installBtn.IsEnabled = false;

            string[] frames = { "◐", "◓", "◑", "◒" };
            bool running = true;

            _ = Task.Run(async () =>
            {
                int i = 0;

                while (running)
                {
                    Dispatcher.Invoke(() =>
                    {
                        installBtn.Content =
                            frames[i % frames.Length];
                    });

                    i++;

                    await Task.Delay(120);
                }
            });

            await InstallMod(mod.project_id, mod.title, mod.description);

            running = false;

            installBtn.Content = "Installed";
            installBtn.Background =
                new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#374151"));

            installBtn.Foreground = Brushes.White;
        };
    }

    // ✅ RUNDES TEMPLATE (für beide Zustände gleich)
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
    // ✅ DESCRIPTION
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
private async Task InstallMod(string projectId, string modName, string description)    {
        if (currentProfile == null)
            return;

        string loader = currentProfile.Loader.ToLower();
        string version = currentProfile.Version;

        using (HttpClient client = new HttpClient())
        {
            // 🔥 1. Versionen holen
            string url = $"https://api.modrinth.com/v2/project/{projectId}/version";

            string json = await client.GetStringAsync(url);
            var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(json);

            if (versions == null || versions.Count == 0)
            {
                ShowToast("No versions found!");
                return;
            }

            // 🔥 2. passende Version finden
            ModrinthVersion match = null;

            if (currentTab == "mod")
            {
                // ✅ Mods: brauchen Loader + Version
                match = versions.FirstOrDefault(v =>
                    v.game_versions.Contains(version) &&
                    v.loaders.Contains(loader)
                );
            }
            else if (currentTab == "resourcepack")
            {
                // ✅ Resourcepacks: nur Version (falls vorhanden)
                match = versions.FirstOrDefault(v =>
                            v.game_versions.Contains(version)
                        )
                        // fallback falls keine version passt
                        ?? versions.FirstOrDefault();
            }
            else if (currentTab == "shader")
            {
                // ✅ Shader: einfach erste Version nehmen
                match = versions.FirstOrDefault();
            }
            if (match == null)
            {
                ShowToast("No compatible version!");
                return;
            }

            var file = match.files.FirstOrDefault();

            if (file == null)
            {
                ShowToast("No file found!");
                return;
            }

            string downloadUrl = file.url;
            string fileName = file.filename;
            string ext = GetFileExtension();

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
            

            // 🔥 3. Zielordner bestimmen
            string folder = currentTab switch
            {
                "mod" => "mods",
                "resourcepack" => "resourcepacks",
                "shader" => "shaderpacks",
                _ => "mods"
            };

            string basePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "Profiles",
                currentProfile.Name,
                folder
            );


            Directory.CreateDirectory(basePath);

            string fullPath = System.IO.Path.Combine(basePath, cleanName);

            // 🔥 4. Download
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

                if (cf != null)
                {
                    await InstallCurseForgeMod(cf);
                }

                return;
            }
            AddToProfileJson(modName, description);
            ReloadProfileData();
            await LoadContent();
            ShowToast($"Installed: {cleanName}");
            
        }
    }
    private async void FilterChanged(object sender, RoutedEventArgs e)
    {
        // ✅ SAFETY CHECKS
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

        // ✅ NEU OBEN EINFÜGEN
        ToastContainer.Children.Insert(0, toast);

        // ✅ ANIMATION IN
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

        // ✅ sichtbare Zeit
        await Task.Delay(2000);

        // ✅ OUT ANIMATION
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

        // ✅ immer frisch aus JSON laden
        var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));

        currentProfile = profiles.FirstOrDefault(p => p.Name == currentProfile.Name);

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
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            currentProfile.Name,
            folder
        );

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

        var profile = profiles.FirstOrDefault(p => p.Name == currentProfile.Name);

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
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            currentProfile.Name,
            folder
        );

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
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "Profiles",
            currentProfile.Name,
            folder
        );

        if (!Directory.Exists(basePath))
        {
            ShowToast("Folder not found");
            return;
        }

        var files = Directory.GetFiles(basePath);

        // ✅ exakter Match (wie Import aufgebaut)
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

    private void AddToProfileJson(
        string name,
        string description,
        string iconUrl = null)
    {
        var profiles =
            JsonSerializer.Deserialize<List<Profile>>(
                File.ReadAllText(filePath));

        var profile =
            profiles.FirstOrDefault(
                p => p.Name == currentProfile.Name);

        if (profile == null)
            return;

        var item = new ContentItem
        {
            Name = name,
            Description = description,
            IconUrl = iconUrl
        };

        if (currentTab == "mod")
            profile.Mods.Add(item);
        else if (currentTab == "resourcepack")
            profile.RP.Add(item);
        else if (currentTab == "shader")
            profile.Shaders.Add(item);

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
        public List<string> game_versions { get; set; }
        public List<string> loaders { get; set; }
        public List<ModrinthFile> files { get; set; }
    }
    private async void MicrosoftLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string redirectUri = "http://localhost:5000/callback";
            string clientId = "COPYRIGHT";

            string authorizeUrl =
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize" +
                $"?client_id={clientId}" +
                "&response_type=code" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&scope=XboxLive.signin offline_access" +
                "&prompt=select_account";

            // 🌐 Browser öffnen
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authorizeUrl,
                UseShellExecute = true
            });

            // ✅ Code holen
            var code = await StartLocalServer();

            if (string.IsNullOrEmpty(code))
            {
                ShowToast("Login failed");
                return;
            }

            // ✅ Microsoft Token
            var msToken = await ExchangeCodeForToken(code, redirectUri);

            if (msToken == null)
            {
                ShowToast("Microsoft login failed");
                return;
            }

            // ✅ 🔥 Xbox → XSTS → Minecraft FLOW
            string xbox = await GetXboxToken(msToken.access_token);

            var xsts = await GetXstsToken(xbox);

            string mcToken = await GetMinecraftToken(xsts.token, xsts.uhs);

            var profile = await GetProfile(mcToken);
            await DownloadSkinHead(profile.name, profile.uuid);
            await DownloadBodySkin(profile.name, profile.uuid);
            // ✅ UI UPDATE (ECHTER NAME!)
            AccountNameText.Text = profile.name;

            // ✅ speichern (Minecraft Token!)
            SaveAccount(profile.name, profile.uuid, mcToken, msToken.refresh_token);
            SaveLastAccount(profile.name);
            LoadAccountsUI();
            


            
        }
        catch (Exception ex)
        {
            ShowToast("Login error: " + ex.Message);
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
        if (cancelLaunch)
        {
            ResetLaunchUI();
            return;
        }

        try
        {
            string resourcePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "resource"
            );

            Directory.CreateDirectory(resourcePath);

            // ✅ ✅ CHECK: vorhandene NRC Mods zählen
            int matchingMods = Directory.GetFiles(resourcePath, "*.jar", SearchOption.AllDirectories)
                .Count(f => System.IO.Path.GetFileName(f).Contains(currentProfile.Version));

            // ✅ ✅ WENN GENUG → SKIP DOWNLOAD
            if (matchingMods >= 5)
            {
                Console.WriteLine("✅ NRC already installed → skip download");
                return;
            }

            
            StartTask("Download NRC...");

            using HttpClient client = new HttpClient();

            // ❗ FIX: & statt &amp;
            string url = "https://www.dropbox.com/scl/fi/yomc0j742ave5sx29y37j/NRC-mods.zip?rlkey=4j4rb4ub14ghgoh0xc9q59tl2&dl=1";

            string zipPath = System.IO.Path.Combine(resourcePath, "nrc.zip");
            ThrowIfCancelled();
            byte[] data = await client.GetByteArrayAsync(url);
            ThrowIfCancelled();
            await File.WriteAllBytesAsync(zipPath, data);

            // ✅ ENT PACKEN
            System.IO.Compression.ZipFile.ExtractToDirectory(
                zipPath,
                resourcePath,
                true
            );

            File.Delete(zipPath);

            
            StopTask("Download NRC...");
        }
        catch (Exception ex)
        
        {
            ShowToast("NRC download failed!");
            StopTask("Download NRC...");
            Console.WriteLine(ex);
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

            string modsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "Profiles",
                currentProfile.Name,
                "mods"
            );

            Directory.CreateDirectory(modsPath);

            // ✅ ALLE DATEIEN DURCHGEHEN
            foreach (var file in Directory.GetFiles(resourcePath, "*.*", SearchOption.AllDirectories))
            {
                string fileName = System.IO.Path.GetFileName(file);

                // ✅ nur Mods mit Version match kopieren
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
    private Border CreateLoadingProfileCard(string name, string loader, string version)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = name,
                        Foreground = Brushes.White,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold
                    },

                    new TextBlock
                    {
                        Text = $"{loader} - {version}",
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        FontSize = 12,
                        Margin = new Thickness(0,2,0,10)
                    },

                    new TextBlock
                    {
                        Text = "⏳ Creating profile...",
                        Foreground = Brushes.LightBlue,
                        FontSize = 13
                    }
                }
            },
            IsHitTestVisible = false // ❌ NICHT klickbar
        };
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
private async Task InstallSingleStandardContent(
    Profile profile,
    StandardContentItem item)
{
    string oldTab = currentTab;
    Profile oldProfile = currentProfile;

    try
    {
        currentProfile = profile;

        // ✅ TYPE setzen
        currentTab = item.Type;

        using HttpClient client =
            new HttpClient();

        // ✅ MODRINTH direkt
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

        // ✅ CURSEFORGE direkt
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

        // ✅ FALLBACK (falls altes JSON ohne Plattform existiert)

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


private async void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            
            if (IsOfflineMode)
                
            {
                
                ShowToast("You must be online to create profiles.");
                
                return;
                
            }
            bool nrcEnabled = nrcEnabledUI;

        // ✅ BUTTON DISABLE
        
        string name = ProfileNameBox.Text.Trim();

        // ✅ Pflichtfeld check
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowToast("Name is required!");
            return;
        }

        // Loader
        ComboBoxItem loaderItem = (ComboBoxItem)LoaderBox.SelectedItem;
        string loader = loaderItem.Content.ToString();
        ComboBoxItem loaderVerItem = (ComboBoxItem)LoaderVersionBox.SelectedItem;
        string loaderVersion = loaderVerItem.Content.ToString();
            
        // Version
        ComboBoxItem versionItem = (ComboBoxItem)VersionBox.SelectedItem;
        string version = versionItem.Content.ToString();

        
        // ✅ Pfad erstellen
        string folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient"
        );

        Directory.CreateDirectory(folder);

        string filePath = System.IO.Path.Combine(folder, "profiles.json");

        // ✅ Profil Objekt (RICHTIGER TYPE)
        var profile = new Profile
        {
            Name = name,
            Loader = loader,
            Version = version,
            LoaderVersion = loaderVersion,
            NrcEnabled = nrcEnabled,
            ProfilePicture = GetRandomProfilePicture()
        };

        // ✅ Liste laden oder neu erstellen
        List<Profile> profiles;

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);

            profiles = JsonSerializer.Deserialize<List<Profile>>(json)
                       ?? new List<Profile>(); // ✅ null safe
        }
        else
        {
            profiles = new List<Profile>(); // ✅ FIX
        }
        // ✅ CHECK: Profilname existiert schon?
        bool exists = profiles.Any(p => 
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );

        if (exists)
        {
            ShowToast("Profile already exists!");
            return;
        }
        
        // ❌ DIESE ZWEI ZEILEN ENTFERNEN HIER
        CreateProfilePanel.Visibility = Visibility.Collapsed;
        RightColumn.Width = new GridLength(0);
        // ✅ Dummy Loading Card anzeigen
        Dispatcher.Invoke(() =>
        {
            var loadingCard = CreateLoadingProfileCard(name, loader, version);
            ProfilesList.Children.Insert(0, loadingCard); // ganz oben
        });
        

        // ✅ hinzufügen
        profiles.Add(profile);

        // ✅ speichern
        string output = JsonSerializer.Serialize(
            profiles,
            new JsonSerializerOptions { WriteIndented = true }
        );

        File.WriteAllText(filePath, output);
       

        
        StartTask("Downloading Java (1/6)...");
        await DownloadJavaVersions();
        ThrowIfCancelled();
        StopTask("Downloading Java (1/6)...");

        StartTask("Downloading Minecraft (2/6)...");
        await DownloadMinecraftClient(version);
        ThrowIfCancelled();
        StopTask("Downloading Minecraft (2/6)...");

        StartTask("Downloading Libraries (3/6)...");
        await DownloadLibraries(version);
        ThrowIfCancelled();
        StopTask("Downloading Libraries (3/6)...");

        if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
        {
            StartTask("Downloading Fabric (4/6)...");
            await DownloadFabricProfile(version, loaderVersion);
            ThrowIfCancelled();
            StopTask("Downloading Fabric (4/6)...");

            StartTask("Downloading Fabric Libraries (5/6)...");
            await DownloadFabricLibraries(version, loaderVersion);
            ThrowIfCancelled();
            StopTask("Downloading Fabric Libraries (5/6)...");
        }
        else if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
        {
            StartTask("Preparing Forge (4/6)...");
    
            // TODO:
            await DownloadForgeProfile(version, loaderVersion);
            ThrowIfCancelled();

            StopTask("Preparing Forge (4/6)...");

            StartTask("Downloading Forge Libraries (5/6)...");

            // TODO:
            await DownloadForgeLibraries(version, loaderVersion);
            ThrowIfCancelled();

            StopTask("Downloading Forge Libraries (5/6)...");
        }

        StartTask("Downloading Assets (6/6)...");
        await DownloadAssets(version);
        ThrowIfCancelled();
        StopTask("Downloading Assets (6/6)...");
        StartTask("Installing Standard Content...");

        await InstallStandardContent(profile);

        StopTask("Installing Standard Content...");
        

        

// ✅ // ✅ UI reset
        ProfileNameBox.Text = "";

       

        // ✅ Liste neu laden (WICHTIG!)
        LoadProfiles();

        
        // ✅ Panel schließen
        CloseCreateProfile_Click(null, null);
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
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".JAXSTORE",
                "JaxClient",
                "Profiles",
                currentProfile.Name,
                "resourcepacks"
            );

            Directory.CreateDirectory(resourcePath);

            string filePath = Path.Combine(resourcePath, "jxc_icon.zip");

            // ✅ Wenn schon vorhanden → skip
            if (File.Exists(filePath))
            {
                Console.WriteLine("[JXC] Resourcepack already exists ✅");
                return;
            }

            StartTask("Downloading ResourcePack...");

            using HttpClient client = new HttpClient();

            // 🔥 WICHTIG: dl=1 !!!
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
    
    private string NormalizeName(string text)
    {
        text = Regex.Replace(
            text,
            @"\bv\d+(\.\d+)+\b",
            "",
            RegexOptions.IgnoreCase);

        return text
            .Replace(":", "")
            .Replace("!", "")
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace(".jar", "")
            .Replace(".zip", "")
            .Replace(".disabled", "")
            .Trim()
            .ToLowerInvariant();
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
        ThrowIfCancelled();

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

        // ✅ JAVA AUTO SELECT (LOCAL PARSE ONLY)
        string javaFolder =
            currentProfile.Loader == "Forge" &&
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
            javaFolder = "jdk-21"; // fallback
        }

        string javaPath = System.IO.Path.Combine(
            basePath, "meta", "Java", javaFolder, "bin", "java.exe");
        if (!File.Exists(javaPath))
        {
            ShowToast("Java is not installed!");
            StopTask("Preparing launch...");
            return;
        }

        // ✅ HARD DEBUG – CHECK REAL JAVA VERSION
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

        // ✅ SYSTEM JAVA DEBUG
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

        // ✅ FABRIC / FORGE JSON
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

            mainClass =
                doc.RootElement.GetProperty("mainClass").GetString();
        }
        else
        {
            string forgeVersion = currentProfile.LoaderVersion;

            // Aus "26.2-65.1.2" -> "65.1.2"
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

                if (fileName.Contains("jarjarfilesystems"))
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
// Java 21 kann das nicht
            forgeJvmArgs = forgeJvmArgs.Replace(
                "-XX:+UseCompactObjectHeaders",
                "");

            mainClass =
                doc.RootElement
                    .GetProperty("mainClass")
                    .GetString();

            Console.WriteLine("FORGE JSON = " + forgeJson);
        }

        // ✅ LIBS
        string libsFolder = System.IO.Path.Combine(basePath, "meta", "libraries", version);

        if (Directory.Exists(libsFolder))
        {
            Dictionary<string, string> bestLibs = new();
            Dictionary<string, Version> bestVersions = new();

            foreach (var file in Directory.GetFiles(libsFolder, "*.jar", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);

                // ❌ natives skip
                if (name.Contains("natives-linux") || name.Contains("natives-macos"))
                    continue;

                // ✅ FABRIC → NUR Profil Version
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

                // ✅ LIB NAME + VERSION SPLITTEN
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

                    // "+mixin" etc entfernen
                    versionStr = versionStr.Split('+')[0];

                    if (!Version.TryParse(versionStr, out Version ver))
                        ver = new Version(0, 0);

                    // ✅ BESTE VERSION MERKEN
                    if (!bestVersions.ContainsKey(libName) || ver > bestVersions[libName])
                    {
                        bestVersions[libName] = ver;
                        bestLibs[libName] = file;
                    }
                }
                catch
                {
                    classpathList.Add(file); // fallback
                }
            }

            // ✅ nur neueste Versionen rein
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

        // ✅ PATHS
        string nativesPath = System.IO.Path.Combine(basePath, "meta", "natives", version);
        string gameDir = System.IO.Path.Combine(basePath, "Profiles", currentProfile.Name);

        Directory.CreateDirectory(nativesPath);
        Directory.CreateDirectory(gameDir);

        // ✅ ASSET INDEX (LOCAL READ)
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
        // ✅ ARGS
        string args =
            forgeJvmArgs +
            
            $"-Xmx{(int)RamSlider.Value}G " +
            $"-Djava.library.path=\"{nativesPath}\" " +
            $"-cp \"{classpath}\" " +
            $"{mainClass} " +
            customJvm +" "+
            forgeGameArgs +
            $"--username \"{currentAccountData.username}\" " +
            $"--uuid {offlineUuid} " +
            $"--accessToken 0 " +
            $"--version \"{version}\" " +
            $"--gameDir \"{gameDir}\" " +
            $"--assetsDir \"{assetsRoot}\" " +
            $"--assetIndex \"{assetIndex}\" " +
            $"--userType legacy";

        Console.WriteLine("=== FINAL ARGS ===");
        Console.WriteLine(args);

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

        // ✅ HARD OVERRIDE WINDOWS JAVA
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
        Console.WriteLine("FINAL ARGS:");
        Console.WriteLine(args);
        ThrowIfCancelled();

        process.Start();
        process.EnableRaisingEvents = true;

        process.Exited += (_, __) =>
        {
            Dispatcher.Invoke(() =>
            {
                UpdateDiscordPresence();
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            });
        };
        ResetLaunchUI();
        var instance = new RunningInstance
        {
            Process = process,
            ProfileName = currentProfile.Name,
            Version = currentProfile.Version,
            Loader = currentProfile.Loader,
            Username = currentAccountData?.username ?? "Unkown"
        };
        process.OutputDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            Dispatcher.Invoke(() =>
            {
                instance.ConsoleLines.Add(e.Data);

                if (InstancesList.SelectedItem == instance)
                {
                    InstanceConsole.AppendText(e.Data + Environment.NewLine);
                    InstanceConsole.ScrollToEnd();
                }
            });
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            Dispatcher.Invoke(() =>
            {
                instance.ConsoleLines.Add("[ERR] " + e.Data);
            });
        };
        runningInstances.Add(instance);
        if (OpenLogsAfterStartingCheckBox.IsChecked == true
            && InstancesPanel.Visibility != Visibility.Visible)
        {
            InstancesPanel.Visibility = Visibility.Visible;

            InstancesList.ItemsSource = null;
            InstancesList.ItemsSource = runningInstances;

            if (runningInstances.Count > 0)
            {
                InstancesList.SelectedIndex = 0;
            }
        }
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
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
        await RefreshLogin(currentAccountData);
        ThrowIfCancelled();
        await EnsureJxcResourcePack();
        ThrowIfCancelled();
        await HandleSpecialVersion(currentProfile.Version);
        ThrowIfCancelled();
        await DownloadNRCFiles();
        ThrowIfCancelled();
        ApplyNRCMods(); 

        StartTask("Preparing launch...");
        

        string basePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE", "JaxClient");

        string version = currentProfile.Version;
        string loaderVersion = currentProfile.LoaderVersion;
       
       
        // ✅ ✅ ✅ FULL VERIFY BEFORE LAUNCH (AUTO REPAIR)
        try
        {
            Console.WriteLine("=== VERIFY START ===");

            // ✅ JAVA (async!)
            Console.WriteLine("STEP: Ensure Java");
            await DownloadJavaVersions();
            ThrowIfCancelled();
            string metaPath = Path.Combine(basePath, "meta");

            // ✅ CLIENT JAR
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

            // ✅ LIBRARIES
            string libsPath = Path.Combine(metaPath, "libraries", version);

            if (!Directory.Exists(libsPath) ||
                Directory.GetFiles(libsPath, "*", SearchOption.AllDirectories).Length < 100)
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

            // ✅ FABRIC
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
            else
            {
                Console.WriteLine("✅ Fabric OK");
            }

            Console.WriteLine("=== VERIFY DONE ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ VERIFY ERROR:");
            Console.WriteLine(ex.ToString());

            ShowToast("Fix download failed: " + ex.Message);
        }

        
        // ✅ ✅ ✅ JAVA AUTO SELECT (FIXED)
        string javaFolder = "jdk-21";

        try
        {
            using var client = new HttpClient();

            // ✅ gleiche Version JSON wie du eh schon lädst
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

            int javaVer = 21; // fallback

            if (vDoc.RootElement.TryGetProperty("javaVersion", out var javaProp))
            {
                javaVer = javaProp.GetProperty("majorVersion").GetInt32();
            }

            javaFolder = $"jdk-{javaVer}";

            
        }
        catch (Exception ex)
        {
            
            javaFolder = "jdk-21"; // fallback
        }


        string javaPath = System.IO.Path.Combine(
            basePath, "meta", "Java", javaFolder, "bin", "java.exe");

        

        // ✅ ✅ ✅ HARD DEBUG – CHECK REAL JAVA VERSION
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

        // ✅ SYSTEM JAVA DEBUG
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

        

        // ✅ FABRIC JSON
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

            mainClass =
                doc.RootElement.GetProperty("mainClass").GetString();
        }
        else
        {
            string forgeVersion = currentProfile.LoaderVersion;

// Aus "26.2-65.1.2" -> "65.1.2"
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

                if (fileName.Contains("jarjarfilesystems"))
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

        // ✅ LIBS
      
        string libsFolder = System.IO.Path.Combine(basePath, "meta", "libraries", version);

        if (Directory.Exists(libsFolder))
        {
            Dictionary<string, string> bestLibs = new();
            Dictionary<string, Version> bestVersions = new();

            foreach (var file in Directory.GetFiles(libsFolder, "*.jar", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);

                // ❌ natives skip
                if (name.Contains("natives-linux") || name.Contains("natives-macos"))
                    continue;

                // ✅ FABRIC → NUR Profil Version
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

                // ✅ LIB NAME + VERSION SPLITTEN
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

                    // "+mixin" etc entfernen
                    versionStr = versionStr.Split('+')[0];

                    if (!Version.TryParse(versionStr, out Version ver))
                        ver = new Version(0, 0);

                    // ✅ BESTE VERSION MERKEN
                    if (!bestVersions.ContainsKey(libName) || ver > bestVersions[libName])
                    {
                        bestVersions[libName] = ver;
                        bestLibs[libName] = file;
                    }
                }
                catch
                {
                    classpathList.Add(file); // fallback
                }
            }

            // ✅ nur neueste Versionen rein
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
        // ✅ PATHS
        string nativesPath = System.IO.Path.Combine(basePath, "meta", "natives", version);
        string gameDir = System.IO.Path.Combine(basePath, "Profiles", currentProfile.Name);

        Directory.CreateDirectory(nativesPath);
        Directory.CreateDirectory(gameDir);

        // ✅ ASSET INDEX (MOJANG)
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

// ✅ ✅ ✅ ASSET VERIFY + REPAIR (CLEAN VERSION)
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

        // ✅ ARGS
        
        string customJvm =
            JvmArgumentsBox.Text?.Trim() ?? "";

        string args =
            forgeJvmArgs +
            $"{customJvm} "+
            $"-Xmx{(int)RamSlider.Value}G " +
            $"-Djava.library.path=\"{nativesPath}\" " +
            $"-cp \"{classpath}\" " +
            $"{mainClass} " +
            forgeGameArgs +
            $"--username \"{currentAccountData.username}\" " +
            $"--uuid {currentAccountData.uuid} " +
            $"--accessToken {currentAccountData.accessToken} " +
            $"--version \"{version}\" " +
            $"--gameDir \"{gameDir}\" " +
            $"--assetsDir \"{assetsRoot}\" " +
            $"--assetIndex \"{assetIndex}\" " +
            $"--userType msa";
        
            

        Console.WriteLine("=== FINAL ARGS ===");
        Console.WriteLine(args);

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

        // ✅ ✅ ✅ HARD OVERRIDE WINDOWS JAVA
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
       Console.WriteLine("FINAL ARGS:");
       Console.WriteLine(args);
      
        process.Start();
        
        process.EnableRaisingEvents = true;

        process.Exited += (_, __) =>
        {
            Dispatcher.Invoke(() =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
                UpdateDiscordPresence();
            });
        };
        var instance = new RunningInstance
        {
            Process = process,
            ProfileName = currentProfile.Name,
            Version = currentProfile.Version,
            Loader = currentProfile.Loader,
            Username = currentAccountData?.username ?? "Unkown"
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            Dispatcher.Invoke(() =>
            {
                instance.ConsoleLines.Add("[ERR] " + e.Data);
            });
        };
        process.EnableRaisingEvents = true;

        process.Exited += (_, __) =>
        {
            Dispatcher.Invoke(() =>
            {
                runningInstances.Remove(instance);
                runningMinecraftInstances.Remove(process);
                UpdateDiscordPresence();

                UpdateInstancesButton();

                if (InstancesList.ItemsSource != null)
                {
                    InstancesList.ItemsSource = null;
                    InstancesList.ItemsSource = runningInstances;
                }
            });
        };
        runningInstances.Add(instance);
        if (OpenLogsAfterStartingCheckBox.IsChecked == true
            && InstancesPanel.Visibility != Visibility.Visible)
        {
            InstancesPanel.Visibility = Visibility.Visible;

            InstancesList.ItemsSource = null;
            InstancesList.ItemsSource = runningInstances;

            if (runningInstances.Count > 0)
            {
                InstancesList.SelectedIndex = 0;
            }
        }
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        runningMinecraftInstances.Add(process);
        UpdateDiscordPresence();
        UpdateInstancesButton();
        process.OutputDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            Dispatcher.Invoke(() =>
            {
                instance.ConsoleLines.Add(e.Data);

                if (InstancesList.SelectedItem == instance)
                {
                    InstanceConsole.AppendText(e.Data + Environment.NewLine);
                    InstanceConsole.ScrollToEnd();
                }
            });
        };

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

            // ✅ LETZTEN ACCOUNT NEHMEN
            var acc = data.accounts.Last();

            currentAccountData = acc;

            // ✅ UI UPDATE
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
    
    
    private void SaveAccount(string username, string uuid, string token, string refreshToken)
    {
        string path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient",
            "accounts.json"
        );

        List<AccountData> accounts;

        // ✅ Datei existiert? -> laden
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

        // ✅ Doppelten Account vermeiden
        accounts.RemoveAll(a => a.uuid == uuid);

        // ✅ neuen hinzufügen
        accounts.Add(new AccountData
        {
            username = username,
            uuid = uuid,
            accessToken = token,
            refreshToken = refreshToken
            
        });

        // ✅ speichern
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
        //cancelLaunch = false;

        LaunchBtn.Tag = "LAUNCH";

        LaunchBtn.Background =
            new SolidColorBrush(Color.FromRgb(37, 99, 235));

        ProfileSwitchBtn.Visibility = Visibility.Visible;

        ProfileSwitchBtn.IsEnabled = true;
        ProfilesBtn.IsEnabled = true;
        CreateProfileBtn.IsEnabled = true;

        StopTask("Preparing launch...");
    }
    private async Task<bool> RefreshLogin(AccountData acc)
    {
      
        
        try
        {
           
            using HttpClient client = new HttpClient();

            var values = new Dictionary<string, string>
            {
                {"client_id", "e16699bb-2aa8-46da-b5e3-45cbcce29091"},
                {"refresh_token", acc.refreshToken},
                {"grant_type", "refresh_token"}
            };

            var response = await client.PostAsync(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                new FormUrlEncodedContent(values)
            );

            var json = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<OAuthToken>(json);

            if (token == null)
                return false;

            // ✅ neues Xbox + MC Token holen
            string xbox = await GetXboxToken(token.access_token);
            var xsts = await GetXstsToken(xbox);
            string mcToken = await GetMinecraftToken(xsts.token, xsts.uhs);

            // ✅ speichern
            acc.accessToken = mcToken;
            acc.refreshToken = token.refresh_token;

            SaveAccountsToFile(); // 👉 musst du gleich machen

            return true;
        }
        catch
        {
            return false;
        }
    }
    private void SaveAccountsToFile()
    {
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

        // ✅ bestehenden Account updaten
        accounts.RemoveAll(a => a.uuid == currentAccountData.uuid);
        accounts.Add(currentAccountData);

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
    }
    public class ModrinthFile
    {
        public string url { get; set; }
        public string filename { get; set; }
    }

    public class ModrinthResponse
    {
        public List<ModrinthProject> hits { get; set; }
    }

    public class ModrinthProject
    {
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

        // ✅ RESET TAB AUF MODS
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

            return; // <- wichtig
        }


            // ERST DANACH TAB WECHSELN
           
        // Buttons reset
        HomeBtn.Tag = null;
        ProfilesBtn.Tag = null;
        SettingsBtn.Tag = null;
        AccountBtn.Tag = null;

        // Seiten ausblenden
        HomePage.Visibility = Visibility.Collapsed;
        ProfilesPage.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        AccountPage.Visibility = Visibility.Collapsed;
        ContentBrowserPage.Visibility = Visibility.Collapsed;

        // Klick bestimmen
        
        clicked.Tag = "Active";

        // richtige Seite anzeigen
        if (clicked == HomeBtn)
            HomePage.Visibility = Visibility.Visible;

        else if (clicked == ProfilesBtn)
            ProfilesPage.Visibility = Visibility.Visible;

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
    
   
    public class ContentItem
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public string IconUrl { get; set; } // ✅ NEU
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

                if (data != null && data.ContainsKey("lastProfile"))
                {
                    string lastName = data["lastProfile"];

                    // ✅ FIX: EXISTENZ CHECK
                    selected = profiles.FirstOrDefault(p =>
                        p.Name.Equals(lastName, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }
        }

        // ✅ FALLBACK (wenn gelöscht)
        if (selected == null)
        {
            selected = profiles.Last();
            SaveLastProfile(selected.Name); // 🔥 optional fix
        }

        currentProfile = selected;

        Dispatcher.InvokeAsync(() =>
        {
            LaunchBtn.ApplyTemplate();
            var txt = LaunchBtn.Template.FindName("SelectedProfileText", LaunchBtn) as TextBlock;

            if (txt != null)
            {
                txt.Text = $"{selected.Name} ({selected.Loader} {selected.Version})";
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
        // 🔥 OUTER CARD
        Grid grid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 10)
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

        // ✅ ACCOUNT BUTTON
        Button btn = new Button
        {
            Content = acc.username,
            Style = TryFindResource("AccountButtonStyle") as Style
        };

        // ✅ aktiver Account highlight
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

        // ✅ 🗑 DELETE BUTTON
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

        // ✅ kein weißer hover
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

    // ✅ löschen
    data.accounts.RemoveAll(a => a.uuid == acc.uuid);

    File.WriteAllText(path,
        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })
    );

    // ✅ wenn aktueller Account gelöscht wird → logout
    if (currentAccountData?.uuid == acc.uuid)
    {
        currentAccountData = null;
        LoadSkinLocal();
        UpdateWelcomeText();
        AccountNameText.Text = "Not logged in";
    }

    ShowToast($"Removed {acc.username}");

    LoadAccountsUI(); // 🔥 refresh
}

private async Task DownloadSkinHead(string username, string uuid)
{
    StartTask("Downloading Skin...");

    try
    {
        using HttpClient client = new HttpClient();

        // ✅ Mineatar URL
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

        // ✅ gleicher Name wie vorher
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

    AccountNameText.Text = acc.username;
    SaveLastAccount(acc.username);
    // 🔥 HEAD NEU LADEN
    await DownloadSkinHead(acc.username, acc.uuid);

    // 🔥 WICHTIG: danach laden
    LoadAccountHead(acc.username);

    ShowToast($"Switched to {acc.username}");
}
private void LoadAccountHead(string username)
{
    string basePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".JAXSTORE",
        "JaxClient",
        "resource"
    );

    string path = System.IO.Path.Combine(basePath, $"{username.ToLower()}head.png");

    if (!File.Exists(path))
    {
        path = System.IO.Path.Combine(basePath, "steve.png");
    }

    if (File.Exists(path))
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.UriSource = new Uri(path, UriKind.Absolute);
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();

        // ✅ ALTES (dein großes UI)
        AccountHeadImage.Source = img;

// Sidebar nur laden wenn online
        if (!IsOfflineMode)
        {
            // immer laden
            AccountHeadImage.Source = img;
            AccountImage.Source = img;

            UpdateOfflineIcon();
        }

        UpdateOfflineIcon();
    }
}
private void EditProfile_Click(object sender, RoutedEventArgs e)
{
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
    EditNameBox.Text = currentProfile.Name;
    SetComboBoxValue(EditLoaderBox, currentProfile.Loader);
    SetComboBoxValue(EditLoaderVersionBox, currentProfile.LoaderVersion);
    SetComboBoxValue(EditVersionBox, currentProfile.Version);


    EditProfileOverlay.Visibility = Visibility.Visible;
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

    // optional reset
    EditNameBox.Text = "";
    EditLoaderBox.SelectedIndex = -1;
    EditLoaderVersionBox.Items.Clear();
    EditVersionBox.SelectedIndex = -1;
}
private async Task<string> GetIconForInstalledContent(string name)
{
    try
    {
        string cleanName = name
            .Replace(".jar", "")
            .Replace(".zip", "")
            .Replace(".disabled", "")
            .Trim()
            .ToLower();

        // ✅ Cache Check
        if (iconCache.TryGetValue(cleanName, out string cached))
            return cached;

        using HttpClient client = new HttpClient();

        string url =
            $"https://api.modrinth.com/v2/search?limit=1&query={Uri.EscapeDataString(cleanName)}";

        string json = await client.GetStringAsync(url);

        var result = JsonSerializer.Deserialize<ModrinthResponse>(json);

        // ✅ Treffer
        if (result?.hits?.Count > 0)
        {
            string icon = result.hits[0].icon_url;

            iconCache[cleanName] = icon;

            return icon;
        }

        // ✅ Kein Treffer
        iconCache[cleanName] = "";
        return null;
    }
    catch
    {
        return null;
    }
}private async void SaveProfileEdit_Click(object sender, RoutedEventArgs e)
{
    var profiles = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(filePath));

    var profile = profiles.FirstOrDefault(p => p.Name == currentProfile.Name);

    if (profile != null)
    {
        string oldName = profile.Name;

        string newName = EditNameBox.Text?.Trim();
        string loader = ((ComboBoxItem)EditLoaderBox.SelectedItem)?.Content.ToString();
        string loaderVersion = ((ComboBoxItem)EditLoaderVersionBox.SelectedItem)?.Content.ToString();
        string gameVersion = ((ComboBoxItem)EditVersionBox.SelectedItem)?.Content.ToString();

        // ✅ 1. VALIDATION
        if (string.IsNullOrWhiteSpace(newName))
        {
            ShowToast("Name cannot be empty!");
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowToast("Invalid profile name!");
            return;
        }

        // ✅ 2. CHECK: NAME SCHON IN JSON VORHANDEN
        bool nameExists = profiles.Any(p => 
            p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
            p != profile
        );

        if (nameExists)
        {
            ShowToast("Profile name already exists!");
            return;
        }
// ✅ NRC CLEANUP (WICHTIG)
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

// ✅ ROLLBACK LOGIC
        if (nrcDeleteFailed)
        {
            editNrcEnabled = true;
            profile.NrcEnabled = true;

            //ShowToast("NRC could not be fully removed → re-enabled.");
        }
        else
        {
            profile.NrcEnabled = editNrcEnabled;
        }

        // ✅ APPLY CHANGES
        profile.Name = newName;
        profile.Loader = loader;
        profile.LoaderVersion = loaderVersion;
        profile.Version = gameVersion;
        profile.NrcEnabled = editNrcEnabled;

        
        // ✅ 3. ORDNER UMBENNEN
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
                // ✅ CHECK: ORDNER EXISTIERT SCHON
                if (Directory.Exists(newPath))
                {
                    ShowToast("Profile folder already exists!");
                    return;
                }

                // ✅ FIX: WINDOWS CASE RENAME + STABILITY
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

        // ✅ 4. FABRIC DOWNLOAD
        try
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".JAXSTORE", "JaxClient", "meta", "versions"
            );

            string versionFolderName = $"fabric-loader-{loaderVersion}-{gameVersion}";
            string versionPath = Path.Combine(basePath, versionFolderName);

            Directory.CreateDirectory(versionPath);

            using HttpClient client = new HttpClient();

            // ✅ JSON
            string jsonUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
            string jsonContent = await client.GetStringAsync(jsonUrl);

            string jsonPath = Path.Combine(versionPath, $"{versionFolderName}.json");
            File.WriteAllText(jsonPath, jsonContent);

            // ✅ JAR
            string jarUrl = $"https://maven.fabricmc.net/net/fabricmc/fabric-loader/{loaderVersion}/fabric-loader-{loaderVersion}.jar";
            byte[] jarBytes = await client.GetByteArrayAsync(jarUrl);

            string jarPath = Path.Combine(versionPath, $"fabric-loader-{loaderVersion}.jar");
            File.WriteAllBytes(jarPath, jarBytes);
        }
        catch (Exception ex)
        {
            ShowToast("Fabric download error: " + ex.Message);
        }

        // ✅ SAVE JSON
        File.WriteAllText(filePath, JsonSerializer.Serialize(profiles, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        // ✅ UI UPDATE
        EditProfileOverlay.Visibility = Visibility.Collapsed;
        ProfileDetailPage.Visibility = Visibility.Collapsed;
        ProfilesPage.Visibility = Visibility.Visible;
        LoadLastProfile();
        // ✅ RELOAD
        LoadProfiles();
    }
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

        public List<ContentItem> Mods { get; set; } = new();
        public List<ContentItem> RP { get; set; } = new();
        public List<ContentItem> Shaders { get; set; } = new();
    }    
    private void UpdateWelcomeText()
    {
        if (currentAccountData != null)
        {
            WelcomeText.Text = $"Welcome back, {currentAccountData.username}!";
        }
        else
        {
            WelcomeText.Text = "Welcome back!";
        }
    }
    private void ProfilesPage_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length == 1 &&
                (files[0].EndsWith(".noriskpack", StringComparison.OrdinalIgnoreCase) ||
                 files[0].EndsWith(".jxcpack", StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }
    private async void ProfilesPage_Drop(object sender, DragEventArgs e)
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
        else
        {
            ShowToast("Unsupported file type!");
        }
    }
    private void Search_TextChangedd(object sender, TextChangedEventArgs e)
    {
        if (ProfilesList == null)
            return;

        string search = SearchBox.Text?.ToLower().Trim() ?? "";

        // ✅ IGNORE PLACEHOLDER
        if (search == "search..." || string.IsNullOrWhiteSpace(search))
        {
            LoadProfiles(); // 🔥 RESET KOMPLETT
            return;
        }

        foreach (var child in ProfilesList.Children)
        {
            if (child is Border border && border.Tag is Profile profile)
            {
                if (profile.Name.ToLower().Contains(search))
                    border.Visibility = Visibility.Visible;
                else
                    border.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void LoadLastAccount()
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
    
        // ✅ SETZEN
        currentAccountData = acc;
    
        AccountNameText.Text = acc.username;
        LoadAccountHead(acc.username);
        LoadSkinLocal();
        UpdateWelcomeText();
    
       
    }
    private void CancelOffline_Click(object sender, RoutedEventArgs e)
    {
        OfflineLoginOverlay.Visibility = Visibility.Collapsed;
    }
    private async Task EnsureSteveSkin()
    {
        string baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient"
        );

        string resourceFolder = Path.Combine(baseFolder, "resource");
        string logFile = Path.Combine(baseFolder, "log.txt");

        Directory.CreateDirectory(resourceFolder);

        string stevePath = Path.Combine(resourceFolder, "steve.png");

        // ✅ Wenn schon existiert → nix machen
        if (File.Exists(stevePath))
            return;

        using HttpClient client = new HttpClient();

        // ✅ WICHTIG: User-Agent (sonst 400 Fehler!)
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "JaxClient/1.0 (https://jaxclient.local; dev@jaxclient.local)"
        );

       

        try
        {
           

            // ✅ offizieller Steve (VZGE built-in)
            string url = "https://visage.surgeplay.com/full/512/X-Steve.png?no=cape";

            

            byte[] data = await client.GetByteArrayAsync(url);

            if (data != null && data.Length > 1000)
            {
                await File.WriteAllBytesAsync(stevePath, data);

                
                
                return;
            }

            throw new Exception("Invalid Steve data");
        }
        catch (Exception ex)
        {
            
            ShowToast("Failed to download Steve skin!");
        }
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

        // ✅ Offline -> Online
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
                        await RefreshLogin(currentAccountData);

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

    private void KillInstance_Click(object sender, RoutedEventArgs e)
    {
        if (InstancesList.SelectedItem is RunningInstance instance)
        {
            try
            {
                instance.Process?.Kill(true);
            }
            catch { }

            runningInstances.Remove(instance);
            UpdateDiscordPresence();
            runningMinecraftInstances.Remove(instance.Process);

            if (runningInstances.Count == 0)
            {
                InstancesPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                InstancesList.ItemsSource = null;
                InstancesList.ItemsSource = runningInstances;

                InstancesList.SelectedIndex = 0;
            }

            UpdateInstancesButton();
        }
    }
        
    private async void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && InstancesList.SelectedItem is RunningInstance instance)
        {
            Clipboard.SetText(string.Join("\n", instance.ConsoleLines));

            object originalContent = btn.Content;
            btn.Content = "✓ Copied!";
            btn.IsEnabled = false;

            await Task.Delay(1500);

            btn.Content = originalContent;
            btn.IsEnabled = true;
        }
    }    
    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        if (InstancesList.SelectedItem is RunningInstance instance)
        {
            instance.ConsoleLines.Clear();
            InstanceConsole.Clear();
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
        InstancesPanel.Visibility = Visibility.Visible;

        InstancesList.ItemsSource = null;
        InstancesList.ItemsSource = runningInstances;

        if (runningInstances.Count > 0)
        {
            InstancesList.SelectedIndex = 0;
        }
    }
    private void InstancesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InstancesList.SelectedItem is RunningInstance instance)
        {
            InstanceConsole.Text =
                string.Join(Environment.NewLine, instance.ConsoleLines);

            InstanceConsole.ScrollToEnd();
        }
    }
    
    public class LauncherSettings
    {
        public bool HideLauncherOnLaunch { get; set; }

        public bool OpenLogsAfterStarting { get; set; }
        public bool OfflineMode { get; set; }
        public int RamAllocation { get; set; } = 6;

        public string JvmArguments { get; set; } = "";
        public List<StandardContentItem>
            StandardContent
        {
            get;
            set;
        }
            = new();
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

        // ✅ Offline Mode wurde deaktiviert
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
                return;

            string json = File.ReadAllText(SettingsFile);

            var settings =
                JsonSerializer.Deserialize<LauncherSettings>(json);

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

          

            
            //Matrix
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

    private void SaveSettingsObject(
        LauncherSettings settings)
    {
        try
        {
            File.WriteAllText(
                SettingsFile,
                JsonSerializer.Serialize(
                    settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
        catch
        {
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

            // ✅ Standard Content ebenfalls leeren
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

    // ✅ NONE CARD

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

    // ✅ CHECKMARK

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
            var settings = new LauncherSettings
            {
                
                HideLauncherOnLaunch =
                    HideLauncherOnLaunchCheckBox.IsChecked == true,

                OpenLogsAfterStarting =
                    OpenLogsAfterStartingCheckBox.IsChecked == true,

                OfflineMode =
                    OfflineModeCheckBox.IsChecked == true,
                JvmArguments = 
                    JvmArgumentsBox.Text,
                RamAllocation =
                    (int)RamSlider.Value,
                MatrixEnabled =
                    MatrixEnabledCheckBox.IsChecked == true,
                
            };

            string json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
        }
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
    public MainWindow()
    {
        
        SetCurrentProcessExplicitAppUserModelID(
        "JaxClient.Launcher");
        if (!Environment.GetCommandLineArgs()
                .Contains("--launcher"))
        {
            MessageBox.Show("NO LAUNCHER ARGS! PLS START THE SHORTCUT OR JXCUpdater.exe (not JaxClient-v3... .exe)!");
            Environment.Exit(0); 
            return;
        }
       
       
    
        InitializeComponent();
        
        
       
        InitInstanceWatcher();
        LoadLauncherVersion();
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
        // ✅ UPDATER
        
        
        Dispatcher.InvokeAsync(async () =>
        {
            StartTask("Checking for updates...");

           

            try
            {
                await Task.Delay(800);

                if (!Environment.GetCommandLineArgs()
                        .Contains("--updated"))
                {
                    await StartUpdater();
                }
            }
            finally
            {
                StopTask("Checking for updates...");
            }
        });;
        
        Dispatcher.InvokeAsync(async () =>
        {
            await EnsureSteveSkin();   // ✅ Steve laden
            LoadSkinLocal();       
            UpdateWelcomeText();
            // ✅ anzeigen
        });

        // ✅ Loader Default
        Dispatcher.InvokeAsync(() =>
        {
            LoaderBox.SelectedIndex = 0;
        });

        // ✅ PATH SETUP
        string folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".JAXSTORE",
            "JaxClient"
        );

        Directory.CreateDirectory(folder);

        filePath = System.IO.Path.Combine(folder, "profiles.json");

        // ✅ LOAD DATA
        LoadLastProfile();
        LoadAccounts(); 
        LoadLastAccount(); // 🔥 DAS HIER

        // 👉 EINMAL reicht!!

        // ✅ UI SAFE LOAD
        Dispatcher.InvokeAsync(() =>
        {
            LoadAccountsUI();
        });

        // ✅ ✅ ✅ TOKEN REFRESH (FIXED 🔥)
        if (!IsOfflineMode &&
            OfflineModeCheckBox.IsChecked != true &&
            currentAccountData != null &&
            !string.IsNullOrEmpty(currentAccountData.refreshToken))
            
        {
            Dispatcher.InvokeAsync(async () =>
            {
                StartTask("Refreshing session...");

                bool success = await RefreshLogin(currentAccountData);

                StopTask("Refreshing session...");
            });
        }
        
        AssignMissingProfilePictures();
        // ✅ Profiles laden
        LoadProfiles();
        
        
        bool startedByUpdater =
            Environment.GetCommandLineArgs()
                .Contains("--updaterJXC");

        if (!startedByUpdater)
        {
            _ = Task.Run(async () =>
            {
                StartTask("Update Updater...");

                try
                {
                    await EnsureLatestUpdater();
                }
                finally
                {
                    StopTask("Update Updater...");
                }
            });
        }
    }
}