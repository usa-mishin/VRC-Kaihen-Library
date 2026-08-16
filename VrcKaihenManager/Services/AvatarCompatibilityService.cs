using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VrcKaihenManager.Models;

namespace VrcKaihenManager.Services;

public static partial class AvatarCompatibilityService
{
    private static readonly HashSet<string> IgnoredWords = new(StringComparer.OrdinalIgnoreCase)
    { "オリジナル", "モデル", "avatar", "original", "model", "VRChat", "PC", "Mobile", "版" };

    [GeneratedRegex(@"[\p{L}\p{N}_-]{2,}")]
    private static partial Regex WordPattern();

    public static string GenerateDefaultPrimaryIdentifier(LibraryItem item)
    {
        var candidates = WordPattern().Matches(item.Name).Select(x => x.Value)
            .Where(IsUsefulName).ToList();
        return candidates.FirstOrDefault(x => ContainsJapanese(x))
            ?? candidates.FirstOrDefault()
            ?? item.Name;
    }

    public static IReadOnlyList<string> GenerateDefaultIdentifiers(LibraryItem item)
    {
        var values = WordPattern().Matches(item.Name).Select(x => x.Value)
            .Where(IsUsefulName)
            .ToList();
        if (item.BoothItemId is long id)
            values.Add(id.ToString());
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<CompatibilityMatch> Detect(LibraryItem item, IReadOnlyList<AvatarProfile> profiles, IReadOnlyDictionary<string, HashSet<string>> sharedBodyRelations)
    {
        if (item.Category is not ("衣装" or "髪型" or "アクセサリー" or "テクスチャ" or "マテリアル" or "ギミック" or "アニメーション")) return [];
        IEnumerable<(string Place, string Text)> sources =
            PurchasedPackClassifier.Classify(item, profiles) == PurchasedPackClassifier.AvatarSpecific
                ? item.DownloadedVariationNames.Select(name => ("DL商品", name))
                : [("商品説明", item.Description), ("タグ", item.Tags), ("商品種類", item.VariationNames)];
        var direct = new List<(AvatarProfile Profile, string Evidence)>();
        foreach (var profile in profiles)
        {
            var matched = (from identifier in GetMatchingIdentifiers(profile).OrderByDescending(x => x.Length)
                           from source in sources
                           where IsMatch(source.Text, identifier)
                           select (Identifier: identifier, Place: source.Place)).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(matched.Identifier))
            {
                direct.Add((profile, $"{matched.Place}: {matched.Identifier}"));
            }
        }

        var result = direct.Select(x => new CompatibilityMatch(x.Profile.RegistrationId, x.Profile.PrimaryIdentifier, x.Evidence, false)).ToList();
        foreach (var match in direct)
        foreach (var sibling in GetSharedBodyAvatars(match.Profile, profiles, sharedBodyRelations))
            if (result.All(x => x.AvatarRegistrationId != sibling.RegistrationId))
                result.Add(new CompatibilityMatch(sibling.RegistrationId, sibling.PrimaryIdentifier, $"共通素体: {match.Profile.PrimaryIdentifier}", true));
        return result;
    }

    private static IEnumerable<string> GetMatchingIdentifiers(AvatarProfile profile) =>
        new[] { profile.PrimaryIdentifier }.Concat(profile.Identifiers)
            .Where(x => !string.IsNullOrWhiteSpace(x));

    private static IEnumerable<AvatarProfile> GetSharedBodyAvatars(AvatarProfile origin, IReadOnlyList<AvatarProfile> profiles, IReadOnlyDictionary<string, HashSet<string>> relations)
    {
        var visited = new HashSet<string> { origin.RegistrationId };
        var byId = profiles.ToDictionary(x => x.RegistrationId);
        var queue = new Queue<string>();
        queue.Enqueue(origin.RegistrationId);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!relations.TryGetValue(currentId, out var relatedIds)) continue;
            foreach (var relatedId in relatedIds)
            {
                if (!visited.Add(relatedId) || !byId.TryGetValue(relatedId, out var candidate)) continue;
                queue.Enqueue(relatedId);
                yield return candidate;
            }
        }
    }

    private static bool IsUsefulName(string value) =>
        !IgnoredWords.Contains(value) && !value.Contains("3D", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsJapanese(string value) => value.Any(c =>
        c is >= '\u3040' and <= '\u30ff' or >= '\u3400' and <= '\u9fff');

    private static bool IsMatch(string text, string identifier)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(identifier)) return false;
        text = text.Normalize(NormalizationForm.FormKC);
        identifier = identifier.Normalize(NormalizationForm.FormKC);
        if (identifier.All(char.IsDigit) || identifier.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return text.Contains(identifier, StringComparison.OrdinalIgnoreCase);
        var escaped = Regex.Escape(identifier);
        return Regex.IsMatch(text,
            $@"(?<![\p{{L}}\p{{N}}]){escaped}(?=(?:\s*(?:対応|用|専用))?(?![\p{{L}}\p{{N}}]))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
