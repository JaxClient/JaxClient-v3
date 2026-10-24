using Microsoft.Data.Sqlite;
using System.IO;

namespace JaxClient_v3;

internal sealed record ModrinthProfileSnapshot(
    string InstanceId,
    string Name,
    string RelativePath,
    string InstallStage,
    string UpdateChannel,
    string? IconPath,
    long Created,
    long Modified,
    long? LastPlayed,
    long SubmittedTimePlayed,
    long RecentTimePlayed,
    string ContentSetId,
    string ContentSetName,
    string ContentSetStatus,
    string SourceKind,
    string GameVersion,
    long? ProtocolVersion,
    string Loader,
    string LoaderVersion,
    string? LinkKind,
    string? ProjectId,
    string? VersionId,
    string Directory,
    IReadOnlyList<ExternalProfileContentSnapshot> Content);

internal sealed record ExternalProfileContentSnapshot(
    string Type,
    string Name,
    string RelativePath,
    string? ProjectId,
    string? VersionId,
    bool Enabled);

internal static class ModrinthProfileSynchronizer
{
    public static IReadOnlyList<ModrinthProfileSnapshot>? ReadProfiles()
    {
        string settingsDirectory = GetSettingsDirectory();
        string databasePath = Path.Combine(settingsDirectory, "app.db");
        if (!File.Exists(databasePath))
        {
            return null;
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        if (!TableExists(connection, "instances") ||
            !TableExists(connection, "instance_content_sets"))
        {
            throw new InvalidDataException(
                "The Modrinth database does not contain the expected instance tables.");
        }

        string profilesDirectory = ReadConfiguredProfilesDirectory(
            connection,
            settingsDirectory);
        List<ModrinthProfileSnapshot> profiles = new();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                i.id,
                i.name,
                i.path,
                i.install_stage,
                i.update_channel,
                i.icon_path,
                i.created,
                i.modified,
                i.last_played,
                i.submitted_time_played,
                i.recent_time_played,
                cs.id,
                cs.name,
                cs.status,
                cs.source_kind,
                cs.game_version,
                cs.protocol_version,
                cs.loader,
                COALESCE(cs.loader_version, ''),
                link.link_kind,
                link.modrinth_project_id,
                link.modrinth_version_id
            FROM instances AS i
            JOIN instance_content_sets AS cs
                ON cs.id = i.applied_content_set_id
                AND cs.instance_id = i.id
            LEFT JOIN instance_links AS link
                ON link.instance_id = i.id
            ORDER BY i.name COLLATE NOCASE
            """;

        using (SqliteDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                string relativePath = reader.GetString(2);
                profiles.Add(new ModrinthProfileSnapshot(
                    reader.GetString(0),
                    reader.GetString(1),
                    relativePath,
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetInt64(6),
                    reader.GetInt64(7),
                    reader.IsDBNull(8) ? null : reader.GetInt64(8),
                    reader.GetInt64(9),
                    reader.GetInt64(10),
                    reader.GetString(11),
                    reader.GetString(12),
                    reader.GetString(13),
                    reader.GetString(14),
                    reader.GetString(15),
                    reader.IsDBNull(16) ? null : reader.GetInt64(16),
                    NormalizeLoader(reader.GetString(17)),
                    reader.GetString(18),
                    reader.IsDBNull(19) ? null : reader.GetString(19),
                    reader.IsDBNull(20) ? null : reader.GetString(20),
                    reader.IsDBNull(21) ? null : reader.GetString(21),
                    ResolveInstanceDirectory(profilesDirectory, relativePath),
                    Array.Empty<ExternalProfileContentSnapshot>()));
            }
        }

        return profiles
            .Select(profile => profile with
            {
                Content = ReadContent(
                    connection,
                    profile.InstanceId,
                    profile.ContentSetId,
                    profile.Directory)
            })
            .ToArray();
    }

    private static string GetSettingsDirectory()
    {
        string? overrideDirectory =
            Environment.GetEnvironmentVariable("THESEUS_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return Path.GetFullPath(overrideDirectory);
        }

        string roamingDirectory =
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(roamingDirectory))
        {
            throw new InvalidOperationException(
                "The Windows roaming application-data directory could not be found.");
        }

        return Path.Combine(roamingDirectory, "ModrinthApp");
    }

    private static string ReadConfiguredProfilesDirectory(
        SqliteConnection connection,
        string settingsDirectory)
    {
        if (!TableExists(connection, "settings"))
        {
            return Path.Combine(settingsDirectory, "profiles");
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT custom_dir FROM settings WHERE id = 0";
        object? value = command.ExecuteScalar();
        string? customDirectory =
            value == null || value == DBNull.Value ? null : value.ToString();

        string configDirectory = string.IsNullOrWhiteSpace(customDirectory)
            ? settingsDirectory
            : Path.GetFullPath(customDirectory);

        return Path.Combine(configDirectory, "profiles");
    }

    private static string ResolveInstanceDirectory(
        string profilesDirectory,
        string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                "A Modrinth profile has an absolute path instead of a profile-relative path.");
        }

        string root = Path.GetFullPath(profilesDirectory);
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A Modrinth profile path resolves outside its profiles directory.");
        }

        return fullPath;
    }

    private static IReadOnlyList<ExternalProfileContentSnapshot> ReadContent(
        SqliteConnection connection,
        string instanceId,
        string contentSetId,
        string instanceDirectory)
    {
        Dictionary<string, ExternalProfileContentSnapshot> content =
            new(StringComparer.OrdinalIgnoreCase);

        if (TableExists(connection, "instance_files") &&
            TableExists(connection, "instance_content_entries"))
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    files.relative_path,
                    files.file_name,
                    files.enabled,
                    entries.project_type,
                    entries.project_id,
                    entries.version_id
                FROM instance_files AS files
                LEFT JOIN instance_content_entries AS entries
                    ON entries.file_id = files.id
                    AND entries.instance_id = files.instance_id
                    AND entries.content_set_id = $contentSetId
                WHERE files.instance_id = $instanceId
                    AND files.missing = 0
                """;
            command.Parameters.AddWithValue("$contentSetId", contentSetId);
            command.Parameters.AddWithValue("$instanceId", instanceId);

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string relativePath =
                    NormalizeContentRelativePath(reader.GetString(0));
                string fileName = reader.GetString(1);
                string? type = GetContentType(
                    relativePath,
                    reader.IsDBNull(3) ? null : reader.GetString(3));
                if (type == null)
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(Path.Combine(
                    instanceDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(
                        instanceDirectory.TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar) +
                        Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(fullPath))
                {
                    continue;
                }

                content[relativePath] = new ExternalProfileContentSnapshot(
                    type,
                    Path.GetFileNameWithoutExtension(fileName),
                    relativePath,
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetInt64(2) != 0);
            }
        }

        AddFilesFromDirectory(content, instanceDirectory, "mods", "mod");
        AddFilesFromDirectory(content, instanceDirectory, "resourcepacks", "resourcepack");
        AddFilesFromDirectory(content, instanceDirectory, "shaderpacks", "shader");

        return content.Values.ToArray();
    }

    private static string NormalizeContentRelativePath(string relativePath) =>
        relativePath
            .Replace('\\', '/')
            .TrimStart('/');

    private static void AddFilesFromDirectory(
        Dictionary<string, ExternalProfileContentSnapshot> content,
        string instanceDirectory,
        string folder,
        string type)
    {
        string contentDirectory = Path.Combine(instanceDirectory, folder);
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
            string extension = Path.GetExtension(
                enabled ? file : Path.GetFileNameWithoutExtension(file));
            if (!extension.Equals(".jar", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = NormalizeContentRelativePath(
                Path.GetRelativePath(instanceDirectory, file));
            if (!content.ContainsKey(relativePath))
            {
                content.Add(relativePath, new ExternalProfileContentSnapshot(
                    type,
                    Path.GetFileNameWithoutExtension(
                        enabled ? file : Path.GetFileNameWithoutExtension(file)),
                    relativePath,
                    null,
                    null,
                    enabled));
            }
        }
    }

    private static string? GetContentType(string relativePath, string? projectType)
    {
        string[] segments = relativePath.Split(
            new[] { '/', '\\' },
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment =>
                segment.Equals("mods", StringComparison.OrdinalIgnoreCase)))
        {
            return "mod";
        }

        if (segments.Any(segment =>
                segment.Equals("resourcepacks", StringComparison.OrdinalIgnoreCase)))
        {
            return "resourcepack";
        }

        if (segments.Any(segment =>
                segment.Equals("shaderpacks", StringComparison.OrdinalIgnoreCase)))
        {
            return "shader";
        }

        return projectType?.ToLowerInvariant() switch
        {
            "mod" => "mod",
            "resource_pack" or "resourcepack" => "resourcepack",
            "shader" or "shader_pack" => "shader",
            _ => null
        };
    }

    private static string NormalizeLoader(string loader) =>
        loader.ToLowerInvariant() switch
        {
            "vanilla" => "Vanilla",
            "fabric" => "Fabric",
            "forge" => "Forge",
            "neoforge" or "neo" => "NeoForge",
            "quilt" => "Quilt",
            _ => loader
        };

    private static bool TableExists(
        SqliteConnection connection,
        string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM sqlite_master
                WHERE type = 'table' AND name = $name
            )
            """;
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }
}
