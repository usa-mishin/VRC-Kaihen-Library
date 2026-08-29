using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VrcKaihenLibrary.Models;

namespace VrcKaihenLibrary.Services;

public sealed record BoothLibrarySnapshot(string DatabasePath, string ItemDirectory, int SchemaVersion, IReadOnlyList<LibraryItem> Items);

public sealed class BoothLibraryReader
{
    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "pm.booth.library-manager", "data.db");

    public BoothLibrarySnapshot Read(string? databasePath = null)
    {
        databasePath ??= DefaultDatabasePath;
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("BOOTH Library Manager の data.db が見つかりません。", databasePath);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        var schemaVersion = ReadScalarInt(connection, "SELECT version FROM schema_version LIMIT 1");
        var itemDirectory = ReadItemDirectory(connection);
        var items = ReadItems(connection, itemDirectory);
        return new BoothLibrarySnapshot(databasePath, itemDirectory, schemaVersion, items);
    }

    private static int ReadScalarInt(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    private static string ReadItemDirectory(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT item_directory_path FROM preferences ORDER BY id LIMIT 1";
        var value = command.ExecuteScalar();
        return value switch
        {
            byte[] bytes => Encoding.Unicode.GetString(bytes).TrimEnd('\0'),
            string text => text,
            _ => string.Empty
        };
    }

    private static IReadOnlyList<LibraryItem> ReadItems(SqliteConnection connection, string itemDirectory)
    {
        const string sql = """
            SELECT r.id,
                   COALESCE(obi.name, bi.name, ui.name) AS item_name,
                   COALESCE(s.name, ui.shop_name, '') AS shop_name,
                   COALESCE(pc.name || ' > ' || sc.name, sc.name, '未分類') AS category_name,
                   COALESCE(obi.description, bi.description, ui.description, '') AS description,
                   bi.thumbnail_url,
                   ui.thumbnail_filename,
                   r.booth_item_id,
                   r.created_at,
                   COALESCE(bi.updated_at, ui.updated_at, r.updated_at),
                   bi.published_at,
                   COALESCE((SELECT group_concat(tag, ' / ') FROM (
                       SELECT tag FROM overwritten_booth_item_tags WHERE booth_item_id = bi.id ORDER BY tag
                   )), (SELECT group_concat(tag, ' / ') FROM (
                       SELECT tag FROM booth_item_tag_relations WHERE booth_item_id = bi.id ORDER BY tag
                   )), '') AS tags,
                   COALESCE((SELECT group_concat(variation_name, ' / ') FROM (
                       SELECT variation_name FROM booth_item_variations
                       WHERE booth_item_id = bi.id AND variation_name IS NOT NULL
                       ORDER BY id
                   )), '') AS variation_names,
                   COALESCE((SELECT group_concat(variation_name, char(31)) FROM (
                       SELECT variation_name FROM booth_item_variations
                       WHERE booth_item_id = bi.id
                         AND variation_name IS NOT NULL
                         AND order_id IS NOT NULL
                       ORDER BY id
                   )), '') AS downloaded_variation_names,
                   EXISTS (
                       SELECT 1 FROM booth_item_variations av
                       WHERE av.booth_item_id = bi.id
                   ) AS has_variation_rows,
                   EXISTS (
                       SELECT 1 FROM booth_item_variations pv
                       WHERE pv.booth_item_id = bi.id AND pv.order_id IS NOT NULL
                   ) AS has_purchased_variation_order,
                   EXISTS (
                       SELECT 1 FROM notifications n
                       JOIN booth_item_variations nv
                         ON nv.id = CAST(json_extract(n.content, '$.data[0]') AS INTEGER)
                       WHERE nv.booth_item_id = bi.id
                         AND json_extract(n.content, '$.type') = 'latestDownloadableAvailable'
                         AND n.read = 0
                   ) AS has_file_update,
                   s.thumbnail_url AS shop_thumbnail_url
            FROM registered_items r
            LEFT JOIN booth_items bi ON bi.id = r.booth_item_id
            LEFT JOIN overwritten_booth_items obi ON obi.booth_item_id = bi.id
            LEFT JOIN shops s ON s.subdomain = bi.shop_subdomain
            LEFT JOIN user_item_info ui ON ui.id = r.user_item_info_id
            LEFT JOIN sub_categories sc ON sc.id = COALESCE(bi.sub_category, ui.sub_category)
            LEFT JOIN parent_categories pc ON pc.id = sc.parent_category_id
            ORDER BY item_name COLLATE NOCASE
            """;

        var result = new List<LibraryItem>();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var registrationId = reader.GetString(0);
            var folder = string.IsNullOrWhiteSpace(itemDirectory) ? string.Empty : Path.Combine(itemDirectory, registrationId);
            var thumbnail = reader.IsDBNull(5) ? null : reader.GetString(5);
            if (thumbnail is null && !reader.IsDBNull(6) && !string.IsNullOrWhiteSpace(folder))
                thumbnail = Path.Combine(folder, reader.GetString(6));
            thumbnail = BoothNetworkPolicy.FilterImageSource(thumbnail);

            result.Add(new LibraryItem
            {
                RegistrationId = registrationId,
                Name = reader.IsDBNull(1) ? "名称未設定" : reader.GetString(1),
                ShopName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                OriginalCategory = reader.IsDBNull(3) ? "未分類" : reader.GetString(3),
                ThumbnailUrl = thumbnail,
                FolderPath = folder,
                BoothItemId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                RegisteredAt = ReadDate(reader, 8),
                UpdatedAt = ReadDate(reader, 9),
                PublishedAt = ReadDate(reader, 10),
                Tags = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                VariationNames = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                DownloadedVariationNames = reader.IsDBNull(13) || string.IsNullOrWhiteSpace(reader.GetString(13))
                    ? []
                    : reader.GetString(13).Split('\u001F', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                HasBoothVariationRows = !reader.IsDBNull(14) && reader.GetBoolean(14),
                HasPurchasedVariationOrder = !reader.IsDBNull(15) && reader.GetBoolean(15),
                HasFileUpdate = !reader.IsDBNull(16) && reader.GetBoolean(16),
                ShopThumbnailUrl = BoothNetworkPolicy.FilterImageSource(
                    reader.IsDBNull(17) ? null : reader.GetString(17))
            });
            result[^1].IsAgeRestricted = IsAgeRestricted(connection, result[^1].BoothItemId);
        }
        return result;
    }

    private static DateTimeOffset? ReadDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) || !DateTimeOffset.TryParse(reader.GetString(ordinal), out var value) ? null : value;

    private static bool IsAgeRestricted(SqliteConnection connection, long? boothItemId)
    {
        if (boothItemId is null) return false;
        // BLM schema versions use different names for the BOOTH age gate.
        // Inspect the read-only schema and use only that field; never infer R18
        // from names, descriptions, or tags.
        var candidates = new List<string>();
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = "PRAGMA table_info(booth_items)";
            using var rows = schema.ExecuteReader();
            while (rows.Read())
            {
                var name = rows.GetString(1);
                var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
                if (normalized.Contains("adult") || normalized.Contains("r18") || normalized.Contains("agerestrict"))
                    candidates.Add(name);
            }
        }
        foreach (var column in candidates)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT [{column.Replace("]", "]]", StringComparison.Ordinal)}] FROM booth_items WHERE id=$id";
            command.Parameters.AddWithValue("$id", boothItemId.Value);
            var value = command.ExecuteScalar();
            if (value is bool flag && flag) return true;
            if (value is long number && number != 0) return true;
            if (value is int integer && integer != 0) return true;
            if (value is string text && (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }
}
