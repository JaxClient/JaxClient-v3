using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace JaxClient_v3;

public sealed record NoRiskCachedModSnapshot(
    string Name,
    string FileName,
    bool IsInCache,
    string? SourceType,
    string? DownloadUrl,
    string? RepositoryUrl,
    string? GroupId,
    string? ArtifactId,
    string? Version,
    string? Sha1);

internal sealed record NoRiskProfileSnapshot(
    string Id,
    string Name,
    string RelativePath,
    string GameVersion,
    string Loader,
    string LoaderVersion,
    string Directory,
    IReadOnlyList<ExternalProfileContentSnapshot> Content,
    IReadOnlyList<NoRiskCachedModSnapshot> CachedMods,
    int MissingCachedModCount,
    string? CacheWarning);

internal static class NoRiskProfileSynchronizer
{
    public static IReadOnlyList<NoRiskProfileSnapshot>? ReadProfiles()
    {
        string settingsDirectory = GetSettingsDirectory();
        string profilesPath = Path.Combine(settingsDirectory, "profiles.json");
        if (!File.Exists(profilesPath))
        {
            profilesPath += ".migrated";
        }

        if (!File.Exists(profilesPath))
        {
            return null;
        }

        Dictionary<string, string> loaderVersionOverrides =
            ReadLoaderVersionOverrides(settingsDirectory);
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(profilesPath));
        JsonElement entries = document.RootElement;
        if (entries.ValueKind == JsonValueKind.Object &&
            TryGetProperty(entries, "profiles", out JsonElement profiles))
        {
            entries = profiles;
        }

        if (entries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "The NoRisk profile file does not contain a profile array.");
        }

        string profilesDirectory = Path.GetFullPath(
            Path.Combine(settingsDirectory, "data", "profiles"));
        string modCacheDirectory = GetModCacheDirectory(settingsDirectory);
        string packConfigurationPath = Path.Combine(
            settingsDirectory,
            "norisk_modpacks.json");
        using JsonDocument? packConfiguration =
            File.Exists(packConfigurationPath)
                ? JsonDocument.Parse(File.ReadAllText(packConfigurationPath))
                : null;
        JsonElement packs = default;
        JsonElement repositories = default;
        if (packConfiguration != null &&
            (!TryGetProperty(
                 packConfiguration.RootElement,
                 "packs",
                 out packs) ||
             packs.ValueKind != JsonValueKind.Object))
        {
            throw new InvalidDataException(
                "The NoRisk pack configuration does not contain a pack map.");
        }
        if (packConfiguration != null)
        {
            TryGetProperty(
                packConfiguration.RootElement,
                "repositories",
                out repositories);
        }

        List<NoRiskProfileSnapshot> snapshots = new();
        Dictionary<string, IReadOnlyList<ExternalProfileContentSnapshot>> contentByDirectory =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (!TryGetString(entry, "id", out string id) ||
                !TryGetString(entry, "name", out string name) ||
                !TryGetString(entry, "path", out string relativePath) ||
                !TryGetString(entry, "game_version", out string gameVersion) ||
                !TryGetString(entry, "loader", out string loader))
            {
                continue;
            }

            string directory = ResolveProfileDirectory(
                profilesDirectory,
                relativePath);
            if (!System.IO.Directory.Exists(directory))
            {
                continue;
            }

            TryGetString(entry, "loader_version", out string loaderVersion);
            if (loaderVersionOverrides.TryGetValue(id, out string? overrideVersion))
            {
                loaderVersion = overrideVersion;
            }

            List<NoRiskCachedModSnapshot> cachedMods = new();
            HashSet<string> missingCachedMods = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> consideredFileNames = new(StringComparer.OrdinalIgnoreCase);
            string normalizedLoader = NormalizeLoader(loader);
            string? cacheWarning = null;
            string? selectedPackId = TryGetString(
                entry,
                "selected_norisk_pack_id",
                out string packId)
                ? packId
                : null;

            if (!string.IsNullOrWhiteSpace(selectedPackId))
            {
                if (packConfiguration == null)
                {
                    cacheWarning =
                        "The NoRisk pack configuration is missing.";
                }
                else
                {
                    try
                    {
                        Dictionary<string, JsonElement> resolvedPackMods =
                            ResolvePackMods(packs, selectedPackId);
                        foreach ((string modId, JsonElement mod) in resolvedPackMods)
                        {
                            if (!TryGetProperty(mod, "compatibility", out JsonElement compatibility) ||
                                !TryGetProperty(
                                    compatibility,
                                    gameVersion,
                                    out JsonElement versionCompatibility) ||
                                !TryGetProperty(
                                    versionCompatibility,
                                    GetLoaderKey(normalizedLoader),
                                    out JsonElement target) ||
                                IsPackModDisabled(
                                    entry,
                                    selectedPackId,
                                    modId,
                                    gameVersion,
                                    normalizedLoader))
                            {
                                continue;
                            }

                            string fileName = GetPackModFileName(mod, target, modId);
                            string displayName = TryGetString(
                                mod,
                                "displayName",
                                out string packDisplayName)
                                ? packDisplayName
                                : Path.GetFileNameWithoutExtension(fileName);
                            ModDownloadMetadata download =
                                GetPackModDownloadMetadata(
                                    mod,
                                    target,
                                    repositories);
                            AddCachedMod(
                                cachedMods,
                                missingCachedMods,
                                consideredFileNames,
                                modCacheDirectory,
                                fileName,
                                displayName,
                                download.SourceType,
                                download);
                        }
                    }
                    catch (InvalidDataException exception)
                    {
                        cacheWarning = exception.Message;
                    }
                }
            }

            AddProfileCachedMods(
                entry,
                gameVersion,
                normalizedLoader,
                modCacheDirectory,
                cachedMods,
                missingCachedMods,
                consideredFileNames);

            if (!contentByDirectory.TryGetValue(directory, out var content))
            {
                content = ReadContent(directory);
                contentByDirectory.Add(directory, content);
            }

            List<ExternalProfileContentSnapshot> combinedContent = content.ToList();
            HashSet<string> existingModNames = content
                .Where(item => item.Type == "mod")
                .Select(item => GetEffectiveModFileName(
                    Path.GetFileName(item.RelativePath)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (NoRiskCachedModSnapshot cachedMod in cachedMods)
            {
                if (!existingModNames.Add(cachedMod.FileName))
                {
                    continue;
                }

                combinedContent.Add(new ExternalProfileContentSnapshot(
                    "mod",
                    cachedMod.Name,
                    $"mods/{cachedMod.FileName}",
                    null,
                    null,
                    true));
            }

            snapshots.Add(new NoRiskProfileSnapshot(
                id,
                name,
                relativePath,
                gameVersion,
                normalizedLoader,
                loaderVersion,
                directory,
                combinedContent,
                cachedMods,
                missingCachedMods.Count(fileName =>
                    !content.Any(item =>
                        item.Type == "mod" &&
                        string.Equals(
                            GetEffectiveModFileName(
                                Path.GetFileName(item.RelativePath)),
                            fileName,
                            StringComparison.OrdinalIgnoreCase))),
                cacheWarning));
        }

        return snapshots;
    }

    public static string GetModCacheDirectory()
    {
        string settingsDirectory = GetSettingsDirectory();
        return GetModCacheDirectory(settingsDirectory);
    }

    public static string GetEffectiveModFileName(string fileName) =>
        fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;

    private static string GetSettingsDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "norisk",
            "NoRiskClientV3");

    private static Dictionary<string, string> ReadLoaderVersionOverrides(
        string settingsDirectory)
    {
        Dictionary<string, string> overrides =
            new(StringComparer.Ordinal);
        string databasePath = Path.Combine(settingsDirectory, "meta", "app.db");
        if (!File.Exists(databasePath))
        {
            return overrides;
        }

        SqliteConnectionStringBuilder connectionString =
            new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
        using SqliteConnection connection =
            new(connectionString.ToString());
        connection.Open();

        using SqliteCommand tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'profiles' LIMIT 1";
        if (tableCommand.ExecuteScalar() == null)
        {
            return overrides;
        }

        using SqliteCommand profileCommand = connection.CreateCommand();
        profileCommand.CommandText =
            "SELECT id, loader, settings FROM profiles";
        using SqliteDataReader reader = profileCommand.ExecuteReader();
        while (reader.Read())
        {
            string profileId = reader.GetString(0);
            string loader = GetLoaderKey(reader.GetString(1));
            if (reader.IsDBNull(2))
            {
                continue;
            }

            using JsonDocument settings = JsonDocument.Parse(reader.GetString(2));
            JsonElement settingsRoot = settings.RootElement;
            if (!TryGetProperty(
                    settingsRoot,
                    "use_overwrite_loader_version",
                    out JsonElement useOverride) ||
                useOverride.ValueKind != JsonValueKind.True)
            {
                continue;
            }

            string? version = null;
            bool hasVersionMap = TryGetProperty(
                settingsRoot,
                "overwrite_loader_versions",
                out JsonElement versionMap) &&
                versionMap.ValueKind == JsonValueKind.Object;
            if (hasVersionMap)
            {
                if (TryGetString(versionMap, loader, out string mappedVersion))
                {
                    version = mappedVersion;
                }
                else if (versionMap.EnumerateObject().Any())
                {
                    continue;
                }
            }

            if (version == null &&
                TryGetString(
                    settingsRoot,
                    "overwrite_loader_version",
                    out string legacyVersion))
            {
                version = legacyVersion;
            }

            if (!string.IsNullOrWhiteSpace(version))
            {
                overrides[profileId] = version;
            }
        }

        return overrides;
    }

    private static string GetModCacheDirectory(string settingsDirectory)
    {
        string metadataDirectory = Path.Combine(settingsDirectory, "meta");
        string configurationPath = Path.Combine(
            settingsDirectory,
            "launcher_config.json");
        if (File.Exists(configurationPath))
        {
            using JsonDocument configuration = JsonDocument.Parse(
                File.ReadAllText(configurationPath));
            if (TryGetProperty(
                    configuration.RootElement,
                    "custom_game_directory",
                    out JsonElement customDirectory) &&
                customDirectory.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(customDirectory.GetString()))
            {
                metadataDirectory = customDirectory.GetString()!;
            }
        }

        return Path.GetFullPath(Path.Combine(
            metadataDirectory,
            "mod_cache"));
    }

    private static Dictionary<string, JsonElement> ResolvePackMods(
        JsonElement packs,
        string packId,
        HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visited.Add(packId))
        {
            throw new InvalidDataException(
                $"A circular NoRisk pack inheritance was found for '{packId}'.");
        }

        if (!TryGetProperty(packs, packId, out JsonElement pack) ||
            pack.ValueKind != JsonValueKind.Object)
        {
           
        }

        Dictionary<string, JsonElement> resolvedMods =
            new(StringComparer.Ordinal);
        if (TryGetProperty(pack, "inheritsFrom", out JsonElement inheritedPacks) &&
            inheritedPacks.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement inheritedPack in inheritedPacks.EnumerateArray())
            {
                if (inheritedPack.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(inheritedPack.GetString()))
                {
                    continue;
                }

                foreach ((string inheritedId, JsonElement mod) in ResolvePackMods(
                             packs,
                             inheritedPack.GetString()!,
                             new HashSet<string>(visited, StringComparer.Ordinal)))
                {
                    resolvedMods[inheritedId] = mod;
                }
            }
        }

        if (TryGetProperty(pack, "mods", out JsonElement localMods) &&
            localMods.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement mod in localMods.EnumerateArray())
            {
                if (TryGetString(mod, "id", out string modId))
                {
                    resolvedMods[modId] = mod;
                }
            }
        }

        if (TryGetProperty(pack, "excludeMods", out JsonElement excludedMods) &&
            excludedMods.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement excludedMod in excludedMods.EnumerateArray())
            {
                if (excludedMod.ValueKind == JsonValueKind.String)
                {
                    resolvedMods.Remove(excludedMod.GetString()!);
                }
            }
        }

        return resolvedMods;
    }

    private static bool IsPackModDisabled(
        JsonElement profile,
        string packId,
        string modId,
        string gameVersion,
        string loader)
    {
        if (!TryGetProperty(
                profile,
                "disabled_norisk_mods_detailed",
                out JsonElement disabledMods) ||
            disabledMods.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return disabledMods.EnumerateArray().Any(disabled =>
            TryGetString(disabled, "pack_id", out string disabledPackId) &&
            TryGetString(disabled, "mod_id", out string disabledModId) &&
            TryGetString(disabled, "game_version", out string disabledGameVersion) &&
            TryGetString(disabled, "loader", out string disabledLoader) &&
            string.Equals(disabledPackId, packId, StringComparison.Ordinal) &&
            string.Equals(disabledModId, modId, StringComparison.Ordinal) &&
            string.Equals(disabledGameVersion, gameVersion, StringComparison.Ordinal) &&
            string.Equals(
                NormalizeLoader(disabledLoader),
                loader,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPackModFileName(
        JsonElement mod,
        JsonElement target,
        string modId)
    {
        if (TryGetString(target, "filename", out string targetFileName))
        {
            return ValidateModFileName(targetFileName);
        }

        if (!TryGetString(target, "identifier", out string identifier) ||
            !TryGetProperty(mod, "source", out JsonElement source))
        {
            throw new InvalidDataException(
                $"NoRisk pack mod '{modId}' has no valid cache filename.");
        }

        if (TryGetProperty(target, "source", out JsonElement sourceOverride) &&
            sourceOverride.ValueKind == JsonValueKind.Object)
        {
            source = sourceOverride;
        }

        string? artifact = TryGetString(source, "type", out string sourceType)
            ? sourceType.ToLowerInvariant() switch
            {
                "modrinth" when TryGetString(
                    source,
                    "projectSlug",
                    out string projectSlug) => projectSlug,
                "maven" when TryGetString(
                    source,
                    "artifactId",
                    out string artifactId) => artifactId,
                _ => null
            }
            : null;
        if (artifact == null)
        {
            throw new InvalidDataException(
                $"NoRisk pack mod '{modId}' has no cache filename for its source.");
        }

        return ValidateModFileName($"{artifact}-{identifier}.jar");
    }

    private static void AddProfileCachedMods(
        JsonElement profile,
        string gameVersion,
        string loader,
        string modCacheDirectory,
        List<NoRiskCachedModSnapshot> cachedMods,
        HashSet<string> missingCachedMods,
        HashSet<string> consideredFileNames)
    {
        if (!TryGetProperty(profile, "mods", out JsonElement profileMods) ||
            profileMods.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement mod in profileMods.EnumerateArray())
        {
            if (TryGetProperty(mod, "enabled", out JsonElement enabled) &&
                enabled.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            if (TryGetString(mod, "associated_loader", out string associatedLoader))
            {
                if (!string.Equals(
                        NormalizeLoader(associatedLoader),
                        loader,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else
            {
                continue;
            }

            if (!TargetsGameVersion(mod, gameVersion) ||
                !TryGetProperty(mod, "source", out JsonElement source) ||
                !TryGetString(source, "type", out string sourceType))
            {
                continue;
            }

            string? fileName = TryGetString(
                    mod,
                    "file_name_override",
                    out string fileNameOverride)
                ? fileNameOverride
                : sourceType.ToLowerInvariant() switch
            {
                "modrinth" or "curseforge" =>
                    TryGetString(source, "file_name", out string sourceFileName)
                        ? sourceFileName
                        : null,
                "url" =>
                    TryGetString(source, "file_name", out string urlFileName)
                        ? urlFileName
                        : null,
                "maven" when TryGetString(
                    source,
                    "coordinates",
                    out string coordinates) =>
                    GetMavenFileName(coordinates),
                _ => null
            };
            if (fileName == null)
            {
                continue;
            }

            fileName = ValidateModFileName(fileName);
            string displayName = TryGetString(
                mod,
                "display_name",
                out string profileModDisplayName)
                ? profileModDisplayName
                : Path.GetFileNameWithoutExtension(fileName);
            string? downloadUrl = TryGetString(
                    source,
                    "download_url",
                    out string profileDownloadUrl)
                ? profileDownloadUrl
                : TryGetString(source, "url", out string profileUrl)
                    ? profileUrl
                    : TryGetString(
                        source,
                        "file_url",
                        out string profileFileUrl)
                        ? profileFileUrl
                        : null;
            string? sha1 = TryGetString(
                source,
                "file_hash_sha1",
                out string profileHash)
                ? profileHash
                : null;
            string? repositoryUrl = TryGetString(
                source,
                "repository_url",
                out string profileRepositoryUrl)
                ? profileRepositoryUrl
                : null;
            string? groupId = null;
            string? artifactId = null;
            string? version = null;
            version ??= TryGetString(
                    mod,
                    "version",
                    out string profileVersion)
                ? profileVersion
                : TryGetString(source, "version_id", out string sourceVersion)
                    ? sourceVersion
                    : TryGetString(source, "file_id", out string sourceFileId)
                        ? sourceFileId
                        : null;
            if (string.Equals(
                    sourceType,
                    "maven",
                    StringComparison.OrdinalIgnoreCase) &&
                TryGetString(source, "coordinates", out string mavenCoordinates))
            {
                string[] coordinates = mavenCoordinates.Split(':');
                if (coordinates.Length == 3)
                {
                    groupId = coordinates[0];
                    artifactId = coordinates[1];
                    version = coordinates[2];
                }
            }
            AddCachedMod(
                cachedMods,
                missingCachedMods,
                consideredFileNames,
                modCacheDirectory,
                fileName,
                displayName,
                sourceType,
                new ModDownloadMetadata(
                    sourceType.ToLowerInvariant(),
                    downloadUrl,
                    repositoryUrl,
                    groupId,
                    artifactId,
                    version,
                    sha1));
        }
    }

    private static string? GetMavenFileName(string coordinates)
    {
        string[] parts = coordinates.Split(':');
        return parts.Length == 3
            ? $"{parts[1]}-{parts[2]}.jar"
            : null;
    }

    private static ModDownloadMetadata GetPackModDownloadMetadata(
        JsonElement mod,
        JsonElement target,
        JsonElement repositories)
    {
        JsonElement source = TryGetProperty(
            target,
            "source",
            out JsonElement sourceOverride) &&
            sourceOverride.ValueKind == JsonValueKind.Object
                ? sourceOverride
                : TryGetProperty(mod, "source", out JsonElement originalSource)
                    ? originalSource
                    : default;
        string? sourceType = TryGetString(source, "type", out string type)
            ? type.ToLowerInvariant()
            : null;
        string? identifier = TryGetString(
            target,
            "identifier",
            out string targetIdentifier)
            ? targetIdentifier
            : null;

        if (sourceType == "modrinth" &&
            TryGetString(source, "projectSlug", out string projectSlug))
        {
            return new ModDownloadMetadata(
                "modrinth",
                null,
                "https://api.modrinth.com/maven",
                "maven.modrinth",
                projectSlug,
                identifier,
                null);
        }

        if (sourceType == "maven")
        {
            string? repositoryUrl = null;
            if (TryGetString(source, "repositoryRef", out string repositoryRef) &&
                TryGetProperty(repositories, repositoryRef, out JsonElement repository) &&
                repository.ValueKind == JsonValueKind.String)
            {
                repositoryUrl = repository.GetString();
            }

            return new ModDownloadMetadata(
                "maven",
                null,
                repositoryUrl,
                TryGetString(source, "groupId", out string groupId)
                    ? groupId
                    : null,
                TryGetString(source, "artifactId", out string artifactId)
                    ? artifactId
                    : null,
                identifier,
                null);
        }

        return new ModDownloadMetadata(
            sourceType ?? "unknown",
            sourceType == "url" ? identifier : null,
            null,
            null,
            null,
            null,
            null);
    }

    private static bool TargetsGameVersion(JsonElement mod, string gameVersion)
    {
        bool hasVersions = TryGetProperty(mod, "game_versions", out JsonElement versions) &&
                           versions.ValueKind == JsonValueKind.Array &&
                           versions.GetArrayLength() > 0;
        if (!hasVersions)
        {
            return true;
        }

        return ContainsString(versions, gameVersion) ||
               (TryGetProperty(mod, "force_include_versions", out JsonElement forcedVersions) &&
                ContainsString(forcedVersions, gameVersion));
    }

    private static bool ContainsString(JsonElement values, string expected) =>
        values.ValueKind == JsonValueKind.Array &&
        values.EnumerateArray().Any(value =>
            value.ValueKind == JsonValueKind.String &&
            string.Equals(value.GetString(), expected, StringComparison.Ordinal));

    private static void AddCachedMod(
        List<NoRiskCachedModSnapshot> cachedMods,
        HashSet<string> missingCachedMods,
        HashSet<string> consideredFileNames,
        string modCacheDirectory,
        string fileName,
        string displayName,
        string sourceType,
        ModDownloadMetadata download)
    {
        if (!consideredFileNames.Add(fileName))
        {
            return;
        }

        string cachePath = Path.Combine(modCacheDirectory, fileName);
        bool isInCache = File.Exists(cachePath);
        cachedMods.Add(new NoRiskCachedModSnapshot(
            displayName,
            fileName,
            isInCache,
            sourceType,
            download.DownloadUrl,
            download.RepositoryUrl,
            download.GroupId,
            download.ArtifactId,
            download.Version,
            download.Sha1));
        if (!isInCache)
        {
            missingCachedMods.Add(fileName);
        }
    }

    private sealed record ModDownloadMetadata(
        string SourceType,
        string? DownloadUrl,
        string? RepositoryUrl,
        string? GroupId,
        string? ArtifactId,
        string? Version,
        string? Sha1);

    private static string ValidateModFileName(string fileName)
    {
        if (Path.IsPathRooted(fileName) ||
            !string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal) ||
            fileName is "." or ".." ||
            !Path.GetExtension(fileName).Equals(
                ".jar",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The NoRisk cache contains an invalid mod filename: '{fileName}'.");
        }

        return fileName;
    }

    private static string ResolveProfileDirectory(
        string profilesDirectory,
        string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                "A NoRisk profile has an absolute path instead of a relative path.");
        }

        string root = Path.GetFullPath(profilesDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) ||
            !fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A NoRisk profile path resolves outside its profiles directory.");
        }

        return fullPath;
    }

    private static IReadOnlyList<ExternalProfileContentSnapshot> ReadContent(
        string profileDirectory)
    {
        Dictionary<string, ExternalProfileContentSnapshot> content =
            new(StringComparer.OrdinalIgnoreCase);
        AddContentFiles(content, profileDirectory, "mods", "mod");
        AddContentFiles(content, profileDirectory, "resourcepacks", "resourcepack");
        AddContentFiles(content, profileDirectory, "shaderpacks", "shader");
        return content.Values.ToArray();
    }

    private static void AddContentFiles(
        Dictionary<string, ExternalProfileContentSnapshot> content,
        string profileDirectory,
        string folder,
        string type)
    {
        string contentDirectory = Path.Combine(profileDirectory, folder);
        if (!System.IO.Directory.Exists(contentDirectory))
        {
            return;
        }

        foreach (string file in System.IO.Directory.EnumerateFiles(
                     contentDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            bool enabled = !fileName.EndsWith(
                ".disabled",
                StringComparison.OrdinalIgnoreCase);
            string effectiveName = enabled
                ? fileName
                : Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(effectiveName);
            if (type == "mod"
                    ? !extension.Equals(".jar", StringComparison.OrdinalIgnoreCase) &&
                      !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                    : !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(profileDirectory, file)
                .Replace('\\', '/');
            content[relativePath] = new ExternalProfileContentSnapshot(
                type,
                Path.GetFileNameWithoutExtension(effectiveName),
                relativePath,
                null,
                null,
                enabled);
        }
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        return TryGetProperty(element, propertyName, out JsonElement property) &&
               property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString()!);
    }

    private static string GetLoaderKey(string loader) =>
        loader.ToLowerInvariant() switch
        {
            "neoforge" => "neoforge",
            _ => loader.ToLowerInvariant()
        };

    private static string NormalizeLoader(string loader) =>
        loader.ToLowerInvariant() switch
        {
            "vanilla" or "minecraft" => "Vanilla",
            "fabric" => "Fabric",
            "forge" => "Forge",
            "neoforge" or "neo" => "NeoForge",
            "quilt" => "Quilt",
            _ => loader
        };
}
