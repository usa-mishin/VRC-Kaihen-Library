using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VrcKaihenLibrary.Models;

namespace VrcKaihenLibrary.Services;

public static partial class AvatarCompatibilityService
{
    private static readonly HashSet<string> IgnoredWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "オリジナル", "モデル", "アバター", "キャラクター", "キャラクター素体", "対応", "専用", "商品", "販売", "ダウンロード",
        "女性型", "男性型", "想定", "搭載", "もちふぃった", "avatar", "avatars", "original", "model", "character",
        "VRChat", "VRC", "PC", "Quest", "Mobile", "Android", "iOS", "Cluster", "版", "RE",
        "Unity", "UnityPackage", "SDK", "PhysBone", "PhysBones", "Humanoid", "Blender", "FBX", "VRM", "ver", "version",
        "無料", "無料あり", "有料", "セール中", "free", "sale", "off"
    };

    [GeneratedRegex(@"[\p{L}\p{N}_-]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"^(?:v(?:er(?:sion)?)?\.?\d+(?:[._-]\d+)*[a-z]?|\d+(?:[._-]\d+)+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^(?:\d+(?:\.\d+)?(?:円|%|％|off)|\d{4}年?\d{1,2}月?\d{0,2}日?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceOrDatePattern();

    [GeneratedRegex(@"(?:発売記念|販売記念|期間限定|数量限定|周年記念|記念セール|セール価格|割引|キャンペーン|今だけ|月末まで|sale|anniversary|limited|discount)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromotionPattern();

    [GeneratedRegex(@"^(?:(?:[_-]?(?:vrchat|vrc|pc|quest|mobile|android|ios|cluster|avatar|model|unity|sdk|physbones?|humanoid|blender|fbx|vrm))|(?:[_-]?(?:対応|専用|向け|用|版)))+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GenericOnlyPattern();

    [GeneratedRegex(@"(?:オリジナル|original)?3d(?:モデル|model|アバター|avatar)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GenericModelPhrasePattern();

    [GeneratedRegex(@"(?:[_-]v(?:er(?:sion)?)?\.?\d+(?:[._-]\d+)*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingVersionPattern();

    [GeneratedRegex(@"[（(](?<reading>[^）)]*[↑↓←→][^）)]*)[）)]", RegexOptions.CultureInvariant)]
    private static partial Regex DecoratedReadingPattern();

    [GeneratedRegex(@"[^\p{L}\p{N}_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierDecorationPattern();

    public static string GenerateDefaultPrimaryIdentifier(LibraryItem item)
    {
        if (DefaultAvatarIdentifierCatalog.TryGet(item.BoothItemId, out var knownAvatar))
            return knownAvatar.PrimaryIdentifier;
        var candidates = GetNameCandidates(item.Name)
            .Where(IsUsefulName).ToList();
        return candidates.FirstOrDefault(x => ContainsJapanese(x))
            ?? candidates.FirstOrDefault()
            ?? item.Name;
    }

    public static IReadOnlyList<string> GenerateDefaultIdentifiers(LibraryItem item)
    {
        if (DefaultAvatarIdentifierCatalog.TryGet(item.BoothItemId, out var knownAvatar))
            return new[] { knownAvatar.PrimaryIdentifier }
                .Concat(knownAvatar.Identifiers)
                .Append(knownAvatar.BoothItemId.ToString())
                .Where(IsUsefulNameOrBoothId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        var values = GetNameCandidates(item.Name)
            .Where(IsUsefulName)
            .ToList();
        if (item.BoothItemId is long id)
            values.Add(id.ToString());
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsUsefulNameOrBoothId(string value) =>
        value.All(char.IsDigit) || IsUsefulName(value);

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

    public static IReadOnlyList<string> DetectAvatarNamesFromFileName(
        string fileName, IReadOnlyList<AvatarProfile> profiles)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem)) return [];
        return profiles
            .Where(profile => GetMatchingIdentifiers(profile)
                .Where(identifier => IsUsefulName(identifier)
                    && !identifier.All(char.IsDigit)
                    && !identifier.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(identifier => identifier.Length)
                .Any(identifier => IsMatch(stem, identifier)))
            .Select(profile => profile.PrimaryIdentifier)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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

    private static string CleanCandidate(string value)
    {
        value = value.Normalize(NormalizationForm.FormKC).Trim().Trim('_', '-');
        return TrailingVersionPattern().Replace(value, string.Empty).Trim().Trim('_', '-');
    }

    private static IEnumerable<string> GetNameCandidates(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormKC);
        foreach (Match match in DecoratedReadingPattern().Matches(normalized))
        {
            var joinedReading = IdentifierDecorationPattern().Replace(match.Groups["reading"].Value, string.Empty);
            if (!string.IsNullOrWhiteSpace(joinedReading)) yield return CleanCandidate(joinedReading);
        }

        var withoutDecoratedReadings = DecoratedReadingPattern().Replace(normalized, " ");
        foreach (Match match in WordPattern().Matches(withoutDecoratedReadings))
            yield return CleanCandidate(match.Value);
    }

    private static bool IsUsefulName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Any(char.IsLetter)) return false;
        if (value.Length < 2 && !ContainsJapanese(value)) return false;
        if (IgnoredWords.Contains(value) || GenericModelPhrasePattern().IsMatch(value)) return false;
        return !VersionPattern().IsMatch(value)
            && !PriceOrDatePattern().IsMatch(value)
            && !PromotionPattern().IsMatch(value)
            && !GenericOnlyPattern().IsMatch(value);
    }

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
