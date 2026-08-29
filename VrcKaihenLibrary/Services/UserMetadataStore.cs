using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VrcKaihenLibrary.Models;

namespace VrcKaihenLibrary.Services;

public sealed record ItemMetadata(string Category, bool ImportToAssetsRoot, bool SupportsAllAvatars);
public sealed record CategoryImportSetting(string Category, string FolderName, bool ImportToAssetsRoot);

public sealed class UserMetadataStore
{
    public const int CurrentDataAccessConsentVersion = 1;
    private readonly string _databasePath;

    public UserMetadataStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(localAppData, "VrcKaihenLibrary");
        Directory.CreateDirectory(directory);
        _databasePath = Path.Combine(directory, "library.db");
        var legacyDatabasePath = Path.Combine(localAppData, "VrcKaihenManager", "library.db");
        var isExistingInstallation = File.Exists(_databasePath) || File.Exists(legacyDatabasePath);
        MigrateLegacyDatabaseIfNeeded(legacyDatabasePath);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS item_metadata (
                registration_id TEXT PRIMARY KEY,
                category TEXT NOT NULL,
                import_to_assets_root INTEGER NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS avatar_profiles (
                registration_id TEXT PRIMARY KEY,
                booth_item_id INTEGER,
                name TEXT NOT NULL,
                primary_identifier TEXT NOT NULL DEFAULT '',
                identifiers_manual INTEGER NOT NULL DEFAULT 0,
                base_body_group TEXT,
                is_unpurchased INTEGER NOT NULL DEFAULT 0,
                booth_url TEXT,
                shop_name TEXT,
                thumbnail_url TEXT
            );
            CREATE TABLE IF NOT EXISTS avatar_identifiers (
                registration_id TEXT NOT NULL,
                identifier TEXT NOT NULL,
                PRIMARY KEY(registration_id, identifier),
                FOREIGN KEY(registration_id) REFERENCES avatar_profiles(registration_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS item_compatibility_overrides (
                item_registration_id TEXT NOT NULL,
                avatar_registration_id TEXT NOT NULL,
                state INTEGER NOT NULL CHECK(state IN (-1, 1)),
                updated_at TEXT NOT NULL,
                PRIMARY KEY(item_registration_id, avatar_registration_id),
                FOREIGN KEY(avatar_registration_id) REFERENCES avatar_profiles(registration_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS avatar_shared_body_relations (
                avatar_registration_id TEXT NOT NULL,
                related_avatar_registration_id TEXT NOT NULL,
                PRIMARY KEY(avatar_registration_id, related_avatar_registration_id),
                CHECK(avatar_registration_id <> related_avatar_registration_id),
                FOREIGN KEY(avatar_registration_id) REFERENCES avatar_profiles(registration_id) ON DELETE CASCADE,
                FOREIGN KEY(related_avatar_registration_id) REFERENCES avatar_profiles(registration_id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS category_import_settings (
                category TEXT PRIMARY KEY,
                folder_name TEXT NOT NULL,
                import_to_assets_root INTEGER NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS application_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "avatar_profiles", "primary_identifier", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "avatar_profiles", "identifiers_manual", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "avatar_profiles", "is_unpurchased", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "avatar_profiles", "booth_url", "TEXT");
        EnsureColumn(connection, "avatar_profiles", "shop_name", "TEXT");
        EnsureColumn(connection, "avatar_profiles", "thumbnail_url", "TEXT");
        EnsureColumn(connection, "item_metadata", "supports_all_avatars", "INTEGER NOT NULL DEFAULT 0");
        InitializeFirstLaunchSetting(connection, isExistingInstallation);
        RemoveLegacyBoothUrlIdentifiers(connection);
    }

    private static void InitializeFirstLaunchSetting(SqliteConnection connection, bool isExistingInstallation)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO application_settings(key, value, updated_at)
            VALUES('first_launch_completed', $value, $updated)
            """;
        command.Parameters.AddWithValue("$value", isExistingInstallation ? "true" : "false");
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void MigrateLegacyDatabaseIfNeeded(string legacyDatabasePath)
    {
        if (File.Exists(_databasePath) || !File.Exists(legacyDatabasePath)) return;

        using var source = new SqliteConnection($"Data Source={legacyDatabasePath};Mode=ReadOnly");
        using var destination = new SqliteConnection($"Data Source={_databasePath}");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    public IReadOnlyDictionary<string, ItemMetadata> ReadAll()
    {
        var result = new Dictionary<string, ItemMetadata>();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT registration_id, category, import_to_assets_root, supports_all_avatars FROM item_metadata";
        using var reader = command.ExecuteReader();
        while (reader.Read()) result[reader.GetString(0)] = new ItemMetadata(reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3));
        return result;
    }

    public IReadOnlyDictionary<string, CategoryImportSetting> ReadCategoryImportSettings()
    {
        var result = new Dictionary<string, CategoryImportSetting>(StringComparer.Ordinal);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT category, folder_name, import_to_assets_root FROM category_import_settings";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var setting = new CategoryImportSetting(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2));
            result[setting.Category] = setting;
        }
        return result;
    }

    public void SaveCategoryImportSettings(IEnumerable<CategoryImportSetting> settings)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        foreach (var setting in settings)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO category_import_settings(category, folder_name, import_to_assets_root, updated_at)
                VALUES($category, $folder, $root, $updated)
                ON CONFLICT(category) DO UPDATE SET
                    folder_name=excluded.folder_name,
                    import_to_assets_root=excluded.import_to_assets_root,
                    updated_at=excluded.updated_at
                """;
            command.Parameters.AddWithValue("$category", setting.Category);
            command.Parameters.AddWithValue("$folder", setting.FolderName);
            command.Parameters.AddWithValue("$root", setting.ImportToAssetsRoot);
            command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public bool ReadSmartTitleShorteningEnabled()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM application_settings WHERE key='smart_title_shortening'";
        var value = command.ExecuteScalar() as string;
        return value is null || !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    public void SaveSmartTitleShorteningEnabled(bool enabled)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO application_settings(key, value, updated_at)
            VALUES('smart_title_shortening', $value, $updated)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at
            """;
        command.Parameters.AddWithValue("$value", enabled ? "true" : "false");
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public double? ReadDetailPanelWidth()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM application_settings WHERE key='detail_panel_width'";
        var value = command.ExecuteScalar() as string;
        return double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var width) ? width : null;
    }

    public void SaveDetailPanelWidth(double width)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO application_settings(key, value, updated_at)
            VALUES('detail_panel_width', $value, $updated)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at
            """;
        command.Parameters.AddWithValue("$value", width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public string ReadCardSizePreset()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM application_settings WHERE key='card_size_preset'";
        var value = command.ExecuteScalar() as string;
        return value is "Small" or "Large" ? value : "Medium";
    }

    public void SaveCardSizePreset(string preset)
    {
        if (preset is not ("Small" or "Medium" or "Large"))
            throw new ArgumentOutOfRangeException(nameof(preset));
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO application_settings(key, value, updated_at)
            VALUES('card_size_preset', $value, $updated)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at
            """;
        command.Parameters.AddWithValue("$value", preset);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public bool ReadHideR18Items()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM application_settings WHERE key='hide_r18_items'";
        return string.Equals(command.ExecuteScalar() as string, "1", StringComparison.Ordinal);
    }

    public void SaveHideR18Items(bool hide)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO application_settings(key, value, updated_at)
            VALUES('hide_r18_items', $value, $updated)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at
            """;
        command.Parameters.AddWithValue("$value", hide ? "1" : "0");
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public bool CompleteFirstLaunchAndShouldShowHelp()
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var readCommand = connection.CreateCommand();
        readCommand.Transaction = transaction;
        readCommand.CommandText = "SELECT value FROM application_settings WHERE key='first_launch_completed'";
        var completed = (readCommand.ExecuteScalar() as string)?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (completed)
        {
            transaction.Commit();
            return false;
        }

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
            UPDATE application_settings
            SET value='true', updated_at=$updated
            WHERE key='first_launch_completed'
            """;
        updateCommand.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        updateCommand.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }

    public bool HasCurrentDataAccessConsent()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM application_settings WHERE key='data_access_consent_version'";
        return int.TryParse(command.ExecuteScalar() as string, out var version)
            && version >= CurrentDataAccessConsentVersion;
    }

    public void SaveCurrentDataAccessConsent()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO application_settings(key, value, updated_at)
            VALUES('data_access_consent_version', $value, $updated)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at
            """;
        command.Parameters.AddWithValue("$value", CurrentDataAccessConsentVersion.ToString());
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void ClearDataAccessConsent()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM application_settings WHERE key='data_access_consent_version'";
        command.ExecuteNonQuery();
    }

    public void Save(LibraryItem item)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO item_metadata(registration_id, category, import_to_assets_root, supports_all_avatars, updated_at)
            VALUES ($id, $category, $root, $allAvatars, $updated)
            ON CONFLICT(registration_id) DO UPDATE SET
                category = excluded.category,
                import_to_assets_root = excluded.import_to_assets_root,
                supports_all_avatars = excluded.supports_all_avatars,
                updated_at = excluded.updated_at
            """;
        command.Parameters.AddWithValue("$id", item.RegistrationId);
        command.Parameters.AddWithValue("$category", item.Category);
        command.Parameters.AddWithValue("$root", item.ImportToAssetsRoot);
        command.Parameters.AddWithValue("$allAvatars", item.SupportsAllAvatars);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void SyncAvatarDefaults(IEnumerable<LibraryItem> items)
    {
        var avatarItems = items.Where(x => x.Category == AssetCategories.Avatar).ToList();
        var activeAvatarIds = avatarItems.Select(x => x.RegistrationId).ToHashSet(StringComparer.Ordinal);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var staleProfiles = connection.CreateCommand())
        {
            staleProfiles.Transaction = transaction;
            staleProfiles.CommandText = "SELECT registration_id FROM avatar_profiles WHERE is_unpurchased=0";
            using var reader = staleProfiles.ExecuteReader();
            var staleIds = new List<string>();
            while (reader.Read())
            {
                var registrationId = reader.GetString(0);
                if (!activeAvatarIds.Contains(registrationId)) staleIds.Add(registrationId);
            }
            reader.Close();
            foreach (var staleId in staleIds) DeleteAvatarProfile(connection, transaction, staleId);
        }

        foreach (var item in avatarItems)
        {
            using var profile = connection.CreateCommand();
            profile.Transaction = transaction;
            profile.CommandText = """
                INSERT INTO avatar_profiles(registration_id, booth_item_id, name, is_unpurchased)
                VALUES($id, $boothId, $name, 0)
                ON CONFLICT(registration_id) DO UPDATE SET booth_item_id=excluded.booth_item_id, name=excluded.name, is_unpurchased=0
                """;
            profile.Parameters.AddWithValue("$id", item.RegistrationId);
            profile.Parameters.AddWithValue("$boothId", (object?)item.BoothItemId ?? DBNull.Value);
            profile.Parameters.AddWithValue("$name", item.Name);
            profile.ExecuteNonQuery();

            using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = """
                SELECT p.identifiers_manual,
                       (SELECT COUNT(*) FROM avatar_identifiers i WHERE i.registration_id=p.registration_id)
                FROM avatar_profiles p WHERE p.registration_id=$id
                """;
            countCommand.Parameters.AddWithValue("$id", item.RegistrationId);
            using var defaultsState = countCommand.ExecuteReader();
            if (!defaultsState.Read()) continue;
            var identifiersManual = defaultsState.GetInt32(0) != 0;
            var hasIdentifiers = defaultsState.GetInt32(1) > 0;
            defaultsState.Close();
            if (identifiersManual) continue;
            var hasCatalogEntry = DefaultAvatarIdentifierCatalog.TryGet(item.BoothItemId, out _);
            if (hasIdentifiers && !hasCatalogEntry) continue;

            if (hasCatalogEntry)
            {
                using var deleteAutomaticIdentifiers = connection.CreateCommand();
                deleteAutomaticIdentifiers.Transaction = transaction;
                deleteAutomaticIdentifiers.CommandText = "DELETE FROM avatar_identifiers WHERE registration_id=$id";
                deleteAutomaticIdentifiers.Parameters.AddWithValue("$id", item.RegistrationId);
                deleteAutomaticIdentifiers.ExecuteNonQuery();
            }

            var defaults = AvatarCompatibilityService.GenerateDefaultIdentifiers(item);
            var primaryIdentifier = AvatarCompatibilityService.GenerateDefaultPrimaryIdentifier(item);
            using (var primary = connection.CreateCommand())
            {
                primary.Transaction = transaction;
                primary.CommandText = hasCatalogEntry
                    ? "UPDATE avatar_profiles SET primary_identifier=$primary WHERE registration_id=$id AND identifiers_manual=0"
                    : "UPDATE avatar_profiles SET primary_identifier=$primary WHERE registration_id=$id AND primary_identifier=''";
                primary.Parameters.AddWithValue("$primary", primaryIdentifier);
                primary.Parameters.AddWithValue("$id", item.RegistrationId);
                primary.ExecuteNonQuery();
            }
            foreach (var identifier in defaults.Where(x => !x.Equals(primaryIdentifier, StringComparison.OrdinalIgnoreCase)))
            {
                using var tag = connection.CreateCommand();
                tag.Transaction = transaction;
                tag.CommandText = "INSERT OR IGNORE INTO avatar_identifiers(registration_id, identifier) VALUES($id, $identifier)";
                tag.Parameters.AddWithValue("$id", item.RegistrationId);
                tag.Parameters.AddWithValue("$identifier", identifier);
                tag.ExecuteNonQuery();
            }
        }
        foreach (var item in avatarItems.Where(x => x.BoothItemId is not null))
            MergeMatchingUnpurchasedAvatar(connection, transaction, item.RegistrationId, item.BoothItemId!.Value);
        transaction.Commit();
    }

    private static void MergeMatchingUnpurchasedAvatar(SqliteConnection connection, SqliteTransaction transaction, string purchasedId, long boothItemId)
    {
        using var find = connection.CreateCommand();
        find.Transaction = transaction;
        find.CommandText = "SELECT registration_id FROM avatar_profiles WHERE is_unpurchased=1 AND booth_item_id=$boothId AND registration_id<>$purchasedId LIMIT 1";
        find.Parameters.AddWithValue("$boothId", boothItemId);
        find.Parameters.AddWithValue("$purchasedId", purchasedId);
        var manualId = find.ExecuteScalar() as string;
        if (manualId is null) return;

        using (var updateProfile = connection.CreateCommand())
        {
            updateProfile.Transaction = transaction;
            updateProfile.CommandText = """
                UPDATE avatar_profiles SET
                    primary_identifier=(SELECT primary_identifier FROM avatar_profiles WHERE registration_id=$manualId),
                    identifiers_manual=1,
                    base_body_group=(SELECT base_body_group FROM avatar_profiles WHERE registration_id=$manualId)
                WHERE registration_id=$purchasedId
                """;
            updateProfile.Parameters.AddWithValue("$manualId", manualId);
            updateProfile.Parameters.AddWithValue("$purchasedId", purchasedId);
            updateProfile.ExecuteNonQuery();
        }
        using (var deleteTargetIdentifiers = connection.CreateCommand())
        {
            deleteTargetIdentifiers.Transaction = transaction;
            deleteTargetIdentifiers.CommandText = "DELETE FROM avatar_identifiers WHERE registration_id=$purchasedId";
            deleteTargetIdentifiers.Parameters.AddWithValue("$purchasedId", purchasedId);
            deleteTargetIdentifiers.ExecuteNonQuery();
        }
        using (var copyIdentifiers = connection.CreateCommand())
        {
            copyIdentifiers.Transaction = transaction;
            copyIdentifiers.CommandText = "INSERT OR IGNORE INTO avatar_identifiers(registration_id, identifier) SELECT $purchasedId, identifier FROM avatar_identifiers WHERE registration_id=$manualId";
            copyIdentifiers.Parameters.AddWithValue("$manualId", manualId);
            copyIdentifiers.Parameters.AddWithValue("$purchasedId", purchasedId);
            copyIdentifiers.ExecuteNonQuery();
        }
        foreach (var column in new[] { "avatar_registration_id", "related_avatar_registration_id" })
        {
            using var copyRelations = connection.CreateCommand();
            copyRelations.Transaction = transaction;
            var otherColumn = column == "avatar_registration_id" ? "related_avatar_registration_id" : "avatar_registration_id";
            copyRelations.CommandText = $"INSERT OR IGNORE INTO avatar_shared_body_relations(avatar_registration_id, related_avatar_registration_id) SELECT {(column == "avatar_registration_id" ? "$purchasedId" : otherColumn)}, {(column == "related_avatar_registration_id" ? "$purchasedId" : otherColumn)} FROM avatar_shared_body_relations WHERE {column}=$manualId AND {otherColumn}<>$purchasedId";
            copyRelations.Parameters.AddWithValue("$manualId", manualId);
            copyRelations.Parameters.AddWithValue("$purchasedId", purchasedId);
            copyRelations.ExecuteNonQuery();
        }
        using (var copyOverrides = connection.CreateCommand())
        {
            copyOverrides.Transaction = transaction;
            copyOverrides.CommandText = "INSERT OR REPLACE INTO item_compatibility_overrides(item_registration_id, avatar_registration_id, state, updated_at) SELECT item_registration_id, $purchasedId, state, updated_at FROM item_compatibility_overrides WHERE avatar_registration_id=$manualId";
            copyOverrides.Parameters.AddWithValue("$manualId", manualId);
            copyOverrides.Parameters.AddWithValue("$purchasedId", purchasedId);
            copyOverrides.ExecuteNonQuery();
        }
        DeleteAvatarProfile(connection, transaction, manualId);
    }

    private static void DeleteAvatarProfile(SqliteConnection connection, SqliteTransaction transaction, string registrationId)
    {
        foreach (var sql in new[]
        {
            "DELETE FROM item_compatibility_overrides WHERE avatar_registration_id=$id",
            "DELETE FROM avatar_shared_body_relations WHERE avatar_registration_id=$id OR related_avatar_registration_id=$id",
            "DELETE FROM avatar_identifiers WHERE registration_id=$id",
            "DELETE FROM avatar_profiles WHERE registration_id=$id"
        })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", registrationId);
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<AvatarProfile> ReadAvatarProfiles()
    {
        var result = new List<AvatarProfile>();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.registration_id, p.booth_item_id, p.name, p.primary_identifier, p.base_body_group,
                   COALESCE(group_concat(i.identifier, char(10)), ''), p.is_unpurchased,
                   p.booth_url, p.shop_name, p.thumbnail_url
            FROM avatar_profiles p
            LEFT JOIN avatar_identifiers i ON i.registration_id=p.registration_id
            GROUP BY p.registration_id, p.booth_item_id, p.name, p.primary_identifier, p.base_body_group,
                     p.is_unpurchased, p.booth_url, p.shop_name, p.thumbnail_url
            ORDER BY p.name COLLATE NOCASE
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new AvatarProfile(
            reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetInt64(1), reader.GetString(2),
            string.IsNullOrWhiteSpace(reader.GetString(3)) ? reader.GetString(2) : reader.GetString(3),
            reader.GetString(5).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetBoolean(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9)));
        return result;
    }

    public void SaveUnpurchasedAvatar(AvatarProfile profile)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO avatar_profiles(registration_id, booth_item_id, name, primary_identifier, identifiers_manual,
                                            is_unpurchased, booth_url, shop_name, thumbnail_url)
                VALUES($id, $boothId, $name, $primary, 1, 1, $url, $shop, $thumbnail)
                ON CONFLICT(registration_id) DO UPDATE SET booth_item_id=excluded.booth_item_id, name=excluded.name,
                    primary_identifier=excluded.primary_identifier, identifiers_manual=1, is_unpurchased=1,
                    booth_url=excluded.booth_url, shop_name=excluded.shop_name, thumbnail_url=excluded.thumbnail_url
                """;
            command.Parameters.AddWithValue("$id", profile.RegistrationId);
            command.Parameters.AddWithValue("$boothId", (object?)profile.BoothItemId ?? DBNull.Value);
            command.Parameters.AddWithValue("$name", profile.Name.Trim());
            command.Parameters.AddWithValue("$primary", profile.PrimaryIdentifier.Trim());
            command.Parameters.AddWithValue("$url", (object?)profile.BoothUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("$shop", (object?)profile.ShopName ?? DBNull.Value);
            command.Parameters.AddWithValue("$thumbnail", (object?)profile.ThumbnailUrl ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM avatar_identifiers WHERE registration_id=$id";
            delete.Parameters.AddWithValue("$id", profile.RegistrationId);
            delete.ExecuteNonQuery();
        }
        foreach (var identifier in profile.Identifiers.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO avatar_identifiers(registration_id, identifier) VALUES($id, $identifier)";
            insert.Parameters.AddWithValue("$id", profile.RegistrationId);
            insert.Parameters.AddWithValue("$identifier", identifier);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void DeleteUnpurchasedAvatar(string registrationId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var check = connection.CreateCommand();
        check.Transaction = transaction;
        check.CommandText = "SELECT is_unpurchased FROM avatar_profiles WHERE registration_id=$id";
        check.Parameters.AddWithValue("$id", registrationId);
        if (Convert.ToInt32(check.ExecuteScalar() ?? 0) != 1) return;
        DeleteAvatarProfile(connection, transaction, registrationId);
        transaction.Commit();
    }

    public void SaveAvatarProfile(AvatarProfile profile)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE avatar_profiles SET primary_identifier=$primary, identifiers_manual=1, base_body_group=$group WHERE registration_id=$id";
            update.Parameters.AddWithValue("$primary", profile.PrimaryIdentifier.Trim());
            update.Parameters.AddWithValue("$group", string.IsNullOrWhiteSpace(profile.BaseBodyGroup) ? DBNull.Value : profile.BaseBodyGroup.Trim());
            update.Parameters.AddWithValue("$id", profile.RegistrationId);
            update.ExecuteNonQuery();
        }
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM avatar_identifiers WHERE registration_id=$id";
            delete.Parameters.AddWithValue("$id", profile.RegistrationId);
            delete.ExecuteNonQuery();
        }
        foreach (var identifier in profile.Identifiers.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO avatar_identifiers(registration_id, identifier) VALUES($id, $identifier)";
            insert.Parameters.AddWithValue("$id", profile.RegistrationId);
            insert.Parameters.AddWithValue("$identifier", identifier);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public Dictionary<string, HashSet<string>> ReadSharedBodyRelations()
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT avatar_registration_id, related_avatar_registration_id FROM avatar_shared_body_relations";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var avatarId = reader.GetString(0);
            if (!result.TryGetValue(avatarId, out var related)) result[avatarId] = related = [];
            related.Add(reader.GetString(1));
        }
        return result;
    }

    public void SaveSharedBodyRelations(string avatarId, IEnumerable<string> relatedAvatarIds)
    {
        var relatedIds = relatedAvatarIds.Where(x => !string.IsNullOrWhiteSpace(x) && x != avatarId).Distinct().ToList();
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM avatar_shared_body_relations WHERE avatar_registration_id=$id OR related_avatar_registration_id=$id";
            delete.Parameters.AddWithValue("$id", avatarId);
            delete.ExecuteNonQuery();
        }
        foreach (var relatedId in relatedIds)
        foreach (var (left, right) in new[] { (avatarId, relatedId), (relatedId, avatarId) })
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT OR IGNORE INTO avatar_shared_body_relations(avatar_registration_id, related_avatar_registration_id) VALUES($left,$right)";
            insert.Parameters.AddWithValue("$left", left);
            insert.Parameters.AddWithValue("$right", right);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public bool ResetAutomaticAvatarIdentifiers(LibraryItem item)
    {
        using var connection = Open();
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT identifiers_manual FROM avatar_profiles WHERE registration_id=$id";
        check.Parameters.AddWithValue("$id", item.RegistrationId);
        if (Convert.ToInt32(check.ExecuteScalar() ?? 0) != 0) return false;

        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM avatar_identifiers WHERE registration_id=$id";
            delete.Parameters.AddWithValue("$id", item.RegistrationId);
            delete.ExecuteNonQuery();
        }
        var primary = AvatarCompatibilityService.GenerateDefaultPrimaryIdentifier(item);
        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE avatar_profiles SET primary_identifier=$primary WHERE registration_id=$id";
            update.Parameters.AddWithValue("$primary", primary);
            update.Parameters.AddWithValue("$id", item.RegistrationId);
            update.ExecuteNonQuery();
        }
        foreach (var identifier in AvatarCompatibilityService.GenerateDefaultIdentifiers(item).Where(x => !x.Equals(primary, StringComparison.OrdinalIgnoreCase)))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT OR IGNORE INTO avatar_identifiers(registration_id, identifier) VALUES($id,$identifier)";
            insert.Parameters.AddWithValue("$id", item.RegistrationId);
            insert.Parameters.AddWithValue("$identifier", identifier);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
        return true;
    }

    public IReadOnlyDictionary<string, int> ReadCompatibilityOverrides(string itemRegistrationId)
    {
        var result = new Dictionary<string, int>();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT avatar_registration_id, state FROM item_compatibility_overrides WHERE item_registration_id=$itemId";
        command.Parameters.AddWithValue("$itemId", itemRegistrationId);
        using var reader = command.ExecuteReader();
        while (reader.Read()) result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }

    public Dictionary<string, Dictionary<string, int>> ReadAllCompatibilityOverrides()
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT item_registration_id, avatar_registration_id, state FROM item_compatibility_overrides";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var itemId = reader.GetString(0);
            if (!result.TryGetValue(itemId, out var states)) result[itemId] = states = new(StringComparer.Ordinal);
            states[reader.GetString(1)] = reader.GetInt32(2);
        }
        return result;
    }

    public void SaveCompatibilityOverrides(string itemRegistrationId, IReadOnlyDictionary<string, int> states)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM item_compatibility_overrides WHERE item_registration_id=$itemId";
            delete.Parameters.AddWithValue("$itemId", itemRegistrationId);
            delete.ExecuteNonQuery();
        }
        foreach (var (avatarId, state) in states.Where(x => x.Value is -1 or 1))
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO item_compatibility_overrides(item_registration_id, avatar_registration_id, state, updated_at)
                VALUES($itemId, $avatarId, $state, $updated)
                """;
            insert.Parameters.AddWithValue("$itemId", itemRegistrationId);
            insert.Parameters.AddWithValue("$avatarId", avatarId);
            insert.Parameters.AddWithValue("$state", state);
            insert.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void ResetCompatibilityOverrides(string itemRegistrationId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM item_compatibility_overrides WHERE item_registration_id=$itemId";
        command.Parameters.AddWithValue("$itemId", itemRegistrationId);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        using var reader = check.ExecuteReader();
        while (reader.Read()) if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase)) return;
        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        alter.ExecuteNonQuery();
    }

    private static void RemoveLegacyBoothUrlIdentifiers(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM avatar_identifiers
            WHERE lower(identifier) GLOB 'http*://booth.pm/*/items/[0-9]*'
               OR lower(identifier) GLOB 'http*://booth.pm/items/[0-9]*'
            """;
        command.ExecuteNonQuery();
    }
}
