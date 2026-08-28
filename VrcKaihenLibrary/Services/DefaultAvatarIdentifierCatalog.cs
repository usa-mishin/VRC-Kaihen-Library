using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VrcKaihenLibrary.Services;

public sealed record DefaultAvatarIdentifierEntry(
    long BoothItemId,
    string DisplayName,
    string PrimaryIdentifier,
    IReadOnlyList<string> Identifiers);

public static class DefaultAvatarIdentifierCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<long, DefaultAvatarIdentifierEntry>> Entries = new(Load);

    public static bool TryGet(long? boothItemId, out DefaultAvatarIdentifierEntry entry)
    {
        if (boothItemId is long id && Entries.Value.TryGetValue(id, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    private static IReadOnlyDictionary<long, DefaultAvatarIdentifierEntry> Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "default-avatar-identifiers.json");
            if (!File.Exists(path)) return new Dictionary<long, DefaultAvatarIdentifierEntry>();
            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<DefaultAvatarIdentifierEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return entries
                .Where(x => x.BoothItemId > 0 && !string.IsNullOrWhiteSpace(x.PrimaryIdentifier))
                .GroupBy(x => x.BoothItemId)
                .ToDictionary(x => x.Key, x => x.First());
        }
        catch
        {
            return new Dictionary<long, DefaultAvatarIdentifierEntry>();
        }
    }
}
