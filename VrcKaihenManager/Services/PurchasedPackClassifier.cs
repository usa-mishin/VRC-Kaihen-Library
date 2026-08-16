using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VrcKaihenManager.Models;

namespace VrcKaihenManager.Services;

public static class PurchasedPackClassifier
{
    public const string FullPack = "フルパック";
    public const string AvatarSpecific = "単体購入";
    public const string FreeDownload = "無料/ギフト";

    private static readonly Regex FullPackPattern = new(
        @"(?:full\s*[_-]?\s*(?:pack(?:age)?|set)|complete\s*set|all(?:\s+avatar)?|フル\s*(?:パック|セット|パッケージ)|全アバター)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IgnoredIdentifierPattern = new(
        @"^(?:ver(?:sion)?\.?\d*|v?\d+(?:\.\d+)*|pc|quest|mobile|vrchat|avatar|avatars?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Classify(LibraryItem item, IReadOnlyList<AvatarProfile> profiles)
    {
        if (!SupportsCompatibility(item.Category) || item.BoothItemId is null) return string.Empty;

        if (item.HasBoothVariationRows && !item.HasPurchasedVariationOrder) return FreeDownload;
        if (!item.HasBoothVariationRows || item.DownloadedVariationNames.Count == 0) return FullPack;

        var variationNames = item.DownloadedVariationNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Normalize(NormalizationForm.FormKC))
            .ToList();
        if (variationNames.Count == 0) return FullPack;
        if (variationNames.Any(x => FullPackPattern.IsMatch(x))) return FullPack;

        var identifiers = profiles
            .SelectMany(x => new[] { x.PrimaryIdentifier }.Concat(x.Identifiers))
            .Select(x => x.Normalize(NormalizationForm.FormKC).Trim())
            .Where(IsUsefulAvatarName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length);

        return variationNames.Any(name => identifiers.Any(identifier => ContainsIdentifier(name, identifier)))
            ? AvatarSpecific
            : FullPack;
    }

    private static bool SupportsCompatibility(string category) =>
        category is "衣装" or "髪型" or "アクセサリー" or "テクスチャ" or "マテリアル" or "ギミック" or "アニメーション";

    private static bool IsUsefulAvatarName(string value) =>
        value.Length >= 2
        && value.Any(char.IsLetter)
        && !value.Contains("3Dモデル", StringComparison.OrdinalIgnoreCase)
        && !IgnoredIdentifierPattern.IsMatch(value);

    private static bool ContainsIdentifier(string text, string identifier)
    {
        var escaped = Regex.Escape(identifier);
        return Regex.IsMatch(text,
            $@"(?<![\p{{L}}\p{{N}}]){escaped}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
