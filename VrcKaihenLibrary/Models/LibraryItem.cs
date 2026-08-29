using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System.Text.RegularExpressions;

namespace VrcKaihenLibrary.Models;

public sealed class LibraryItem : INotifyPropertyChanged
{
    private string _category = AssetCategories.Unclassified;
    private bool _importToAssetsRoot;
    private double _cardWidth = 180;
    private bool _hasFileUpdate;
    private bool _supportsAllAvatars;
    private int _compatibleAvatarCount;
    private IReadOnlyList<string> _titleCleanupIdentifiers = [];
    private bool _smartTitleShorteningEnabled = true;
    private string _purchasedPackType = string.Empty;

    public string RegistrationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName => CleanDisplayName(Name);
    public string ShopName { get; set; } = string.Empty;
    public string? ShopThumbnailUrl { get; set; }
    public string OriginalCategory { get; set; } = string.Empty;
    public string Category
    {
        get => _category;
        set { if (_category != value) { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(CategoryBadgeBrush)); } }
    }
    public string Tags { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VariationNames { get; set; } = string.Empty;
    public IReadOnlyList<string> DownloadedVariationNames { get; set; } = [];
    public bool HasBoothVariationRows { get; set; }
    public bool IsAgeRestricted { get; set; }
    public bool HasPurchasedVariationOrder { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public long? BoothItemId { get; set; }
    public DateTimeOffset? RegisteredAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public bool HasFileUpdate
    {
        get => _hasFileUpdate;
        set { if (_hasFileUpdate != value) { _hasFileUpdate = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileUpdateBadgeVisibility)); } }
    }
    public Visibility FileUpdateBadgeVisibility => HasFileUpdate ? Visibility.Visible : Visibility.Collapsed;
    public bool SupportsAllAvatars
    {
        get => _supportsAllAvatars;
        set { if (_supportsAllAvatars != value) { _supportsAllAvatars = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompatibilityCountBadgeVisibility)); } }
    }
    public int CompatibleAvatarCount
    {
        get => _compatibleAvatarCount;
        set { if (_compatibleAvatarCount != value) { _compatibleAvatarCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompatibleAvatarCountText)); OnPropertyChanged(nameof(CompatibilityCountBadgeVisibility)); } }
    }
    public string CompatibleAvatarCountText => $"{CompatibleAvatarCount}アバター";
    public Visibility CompatibilityCountBadgeVisibility =>
        !SupportsAllAvatars && CompatibleAvatarCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    public string PurchasedPackType
    {
        get => _purchasedPackType;
        set
        {
            if (_purchasedPackType == value) return;
            _purchasedPackType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PurchasedPackBadgeVisibility));
            OnPropertyChanged(nameof(PurchasedPackBadgeBrush));
            OnPropertyChanged(nameof(PurchasedPackBadgeForegroundBrush));
            OnPropertyChanged(nameof(PurchasedPackBadgeBorderBrush));
        }
    }
    public Visibility PurchasedPackBadgeVisibility => string.IsNullOrEmpty(PurchasedPackType)
        ? Visibility.Collapsed : Visibility.Visible;
    public SolidColorBrush PurchasedPackBadgeBrush => new(PurchasedPackType == "フルパック"
        ? ColorHelper.FromArgb(255, 208, 87, 92)
        : Microsoft.UI.Colors.White);
    public SolidColorBrush PurchasedPackBadgeForegroundBrush => new(PurchasedPackType == "フルパック"
        ? Microsoft.UI.Colors.White
        : ColorHelper.FromArgb(255, 208, 87, 92));
    public SolidColorBrush PurchasedPackBadgeBorderBrush => new(ColorHelper.FromArgb(255, 208, 87, 92));
    public bool ImportToAssetsRoot
    {
        get => _importToAssetsRoot;
        set { if (_importToAssetsRoot != value) { _importToAssetsRoot = value; OnPropertyChanged(); } }
    }
    public double CardWidth
    {
        get => _cardWidth;
        set { if (Math.Abs(_cardWidth - value) > 0.1) { _cardWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(CardHeight)); } }
    }
    public double CardHeight => CardWidth + 76;
    public IReadOnlyList<string> Categories => AssetCategories.All;
    public string DisplayTags => string.IsNullOrWhiteSpace(Tags) ? "タグなし" : Tags;
    public string UpdatedText => UpdatedAt is null ? string.Empty : $"更新: {UpdatedAt:yyyy/MM/dd}";
    public string? BoothUrl => BoothItemId is null ? null : $"https://booth.pm/ja/items/{BoothItemId}";
    public SolidColorBrush CategoryBadgeBrush => GetCategoryBrush(Category);
    public static SolidColorBrush GetCategoryBrush(string category) => new(category switch
    {
        "アバター" => ColorHelper.FromArgb(255, 111, 78, 220),
        "衣装" => ColorHelper.FromArgb(255, 218, 74, 132),
        "髪型" => ColorHelper.FromArgb(255, 184, 105, 55),
        "アクセサリー" => ColorHelper.FromArgb(255, 15, 142, 153),
        "テクスチャ" => ColorHelper.FromArgb(255, 47, 126, 213),
        "マテリアル" => ColorHelper.FromArgb(255, 82, 99, 176),
        "ギミック" => ColorHelper.FromArgb(255, 220, 112, 24),
        "アニメーション" => ColorHelper.FromArgb(255, 32, 155, 96),
        "ツール" => ColorHelper.FromArgb(255, 87, 99, 112),
        "シェーダー" => ColorHelper.FromArgb(255, 127, 76, 170),
        "ワールド" => ColorHelper.FromArgb(255, 27, 128, 131),
        "その他" => ColorHelper.FromArgb(255, 105, 105, 105),
        _ => ColorHelper.FromArgb(255, 105, 105, 105)
    });

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void SetTitleCleanupIdentifiers(IEnumerable<string> identifiers)
    {
        _titleCleanupIdentifiers = identifiers.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        OnPropertyChanged(nameof(DisplayName));
    }

    public void SetSmartTitleShorteningEnabled(bool enabled)
    {
        if (_smartTitleShorteningEnabled == enabled) return;
        _smartTitleShorteningEnabled = enabled;
        OnPropertyChanged(nameof(DisplayName));
    }

    private string CleanDisplayName(string name)
    {
        if (!_smartTitleShorteningEnabled) return name;

        var value = Regex.Replace(name, @"[【\[［〖〈《]\s*([^】\]］〗〉》]+?)\s*[】\]］〗〉》]", match =>
            IsAuxiliaryBracketText(match.Groups[1].Value) ? string.Empty : match.Value,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        value = Regex.Replace(value, @"[（(]\s*([^（）()]+?)\s*[）)]", match =>
            IsParenthesizedAuxiliaryText(match.Groups[1].Value) ? string.Empty : match.Value,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        value = Regex.Replace(value,
            @"[\p{So}\p{Sk}\p{Cs}\p{M}\uFE0F]*\s*(?:(?:(?:SUMMER|THANK\s+YOU)\s+)?SALE\s*中?(?:\s*[0-9０-９]+\s*[%％]\s*OF{1,2})?|(?:UP\s+TO\s+)?[0-9０-９]+\s*[%％]\s*(?:SALE|OF{1,2})|サマーセール中?|セール中?)\s*[\p{So}\p{Sk}\p{Cs}\p{M}\uFE0F]*",
            string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        value = Regex.Replace(value,
            @"[\p{So}\p{Sk}\p{Cs}\p{M}\uFE0F]*\s*(?<![\p{L}\p{N}])(?:(?:[0-9０-９]+|複数|全)\s*(?:アバター|キャラ)\s*(?:[+＋]\s*[αa])?\s*(?:セミ)?対応|(?:[0-9０-９]+|複数|全)\s*(?:avatars?|人)(?:\s+update)?)(?![\p{L}\p{N}])\s*[\p{So}\p{Sk}\p{Cs}\p{M}\uFE0F]*",
            string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        value = Regex.Replace(value,
            @"[\p{So}\p{Sk}\p{Cs}\p{M}\uFE0F]*\s*(?<![\p{L}\p{N}])(?:[0-9０-９]+\s*colors?|ma対応[!！]?|vrc\s*想定)(?![\p{L}\p{N}])\s*[\p{So}\p{Sk}\p{Cs}\p{M}\uFE0F]*",
            string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        value = Regex.Replace(value, @"\s{2,}", " ").Trim();
        value = Regex.Replace(value, @"(?:\s*[|｜]\s*){2,}", "｜");
        value = Regex.Replace(value, @"^(?:[/|｜・:：_+!！-]\s*)+|(?:\s*[/|｜・:：_+!！-])+$", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? name : value;
    }

    private bool IsAuxiliaryBracketText(string text)
    {
        var value = Regex.Replace(Normalize(text).Trim(), @"^[\p{S}\p{P}\p{Cs}\p{M}\s]+|[\p{S}\p{P}\p{Cs}\p{M}\s]+$", string.Empty);
        if (Regex.IsMatch(value, @"^(?:(?:\d+|複数|全)\s*(?:アバター(?:[+＋]α)?(?:対応)?|avatars?|人))(?:ギミック|\s+(?:update|vrc\s*hair))?$", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(value, @"^(?:vrchat(?:想定)?(?:・ma対応)?|vrc\s*(?:衣装|hair|想定|向けしっぽアクセサリー)|3d(?:衣装モデル|モデル)?|オリジナル3dモデル|vrchat(?:向け衣装モデル|用ヘアモデル)|衣装\s*/\s*靴|liltoon対応|アイテクスチャ|アクセサリー)$", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(value, @"^(?:pb|ma対応|ma設定済み?|簡単導入(?:・ma対応)?|無料\s*/\s*free|セール中?|sale[\s_・-]*中?|update)$", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(value, @"^(?:無料版あり|無料有\s*/\s*[+＋]?free\s*sample|全?\d+種|\d+types|\d+colors?(?:[+＋]\d+)?)$", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(value, @"^(?:周年記念セール.*|(?:up\s+to\s+)?\d+\s*[%％]\s*of{1,2}|\d+\s*colors?)$", RegexOptions.IgnoreCase)) return true;

        var withoutCompatibilitySuffix = Regex.Replace(value, @"(?:専用|対応|用)$", string.Empty, RegexOptions.IgnoreCase).Trim();
        return _titleCleanupIdentifiers.Any(identifier =>
            value.Equals(identifier, StringComparison.OrdinalIgnoreCase)
            || withoutCompatibilitySuffix.Contains(identifier, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsParenthesizedAuxiliaryText(string text)
    {
        var value = Normalize(text).Trim();
        return Regex.IsMatch(value, @"^(?:(?:\d+|複数|全)\s*(?:アバター(?:対応)?|avatars?)|modular\s+avatar対応|vrc\s+3dアイテム)$", RegexOptions.IgnoreCase);
    }

    private static string Normalize(string value) => value.Normalize(NormalizationForm.FormKC);
}
