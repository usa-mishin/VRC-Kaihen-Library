using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VrcKaihenLibrary.Models;
using VrcKaihenLibrary.Services;

namespace VrcKaihenLibrary;

public sealed record DetailTagChip(string Text, string? AvatarRegistrationId, bool IsClickable, string Prefix = "")
{
    private static readonly Brush ChipColor = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 85, 91, 102));
    public Brush Background => IsClickable ? ChipColor : new SolidColorBrush(Microsoft.UI.Colors.White);
    public Brush Foreground => IsClickable ? new SolidColorBrush(Microsoft.UI.Colors.White) : ChipColor;
    public Brush BorderBrush => ChipColor;
    public Thickness BorderThickness => IsClickable ? new Thickness(0) : new Thickness(1);
    public Visibility PrefixVisibility => string.IsNullOrEmpty(Prefix) ? Visibility.Collapsed : Visibility.Visible;
}
public sealed class AvatarFilterOption
{
    public AvatarFilterOption() { }
    public AvatarFilterOption(string? registrationId, string displayName, string primaryIdentifier, string? thumbnailUrl = null) { RegistrationId = registrationId; DisplayName = displayName; PrimaryIdentifier = primaryIdentifier; ThumbnailUrl = thumbnailUrl; }
    public string? RegistrationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PrimaryIdentifier { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
}
public sealed class ShopFilterOption
{
    public ShopFilterOption(string? shopName, string displayName, string? thumbnailUrl = null) { ShopName = shopName; DisplayName = displayName; ThumbnailUrl = thumbnailUrl; }
    public string? ShopName { get; }
    public string DisplayName { get; }
    public string? ThumbnailUrl { get; }
}
public sealed class UnityPackageEntry
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DirectoryText { get; set; } = string.Empty;
    public DateTime LastWriteTime { get; set; }
    public string LastWriteTimeText => LastWriteTime.ToString("yyyy/MM/dd HH:mm");
    public bool HasDuplicateName { get; set; }
    public Visibility DuplicateBadgeVisibility => HasDuplicateName ? Visibility.Visible : Visibility.Collapsed;
}
public sealed class DownloadFileEntry
{
    public string CategoryKey { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DirectoryText { get; set; } = string.Empty;
    public DateTime LastWriteTime { get; set; }
    public string LastWriteTimeText => LastWriteTime.ToString("yyyy/MM/dd HH:mm");
    public bool HasDuplicateName { get; set; }
    public bool HasNewVersionCandidate { get; set; }
    public Brush? AccentBrush { get; set; }
    public Visibility DuplicateBadgeVisibility => HasDuplicateName ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NewVersionBadgeVisibility => HasNewVersionCandidate ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MaterialBadgeVisibility => CategoryKey == "UnityPackage"
        && FileName.Contains("material", StringComparison.OrdinalIgnoreCase)
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BadgeRowVisibility => MaterialBadgeVisibility == Visibility.Visible || HasDuplicateName || HasNewVersionCandidate
        ? Visibility.Visible : Visibility.Collapsed;
}
public sealed class DownloadFileCategory : INotifyPropertyChanged
{
    private bool _isExpanded;
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Glyph { get; set; } = string.Empty;
    public Brush AccentBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    public int Count { get; set; }
    public string CountText => $"{Count}個";
    public string Subtitle { get; set; } = string.Empty;
    public Visibility SubtitleVisibility => string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
    public IReadOnlyList<DownloadFileEntry> Files { get; set; } = [];
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChevronGlyph)));
        }
    }
    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public string ChevronGlyph => IsExpanded ? "\uE70E" : "\uE70D";
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class AvatarSelectionOption : INotifyPropertyChanged
{
    private bool _isSelected;
    public AvatarSelectionOption(AvatarProfile profile, bool isSelected, string displayName, string? thumbnailUrl)
    {
        Profile = profile; _isSelected = isSelected; DisplayName = displayName; ThumbnailUrl = thumbnailUrl;
    }
    public AvatarProfile Profile { get; }
    public string PrimaryIdentifier => Profile.PrimaryIdentifier;
    public string DisplayName { get; }
    public string? ThumbnailUrl { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed partial class MainWindow : Window
{
    private enum OperationPopupKind { Progress, Success, Information, Error }
    private sealed record UnityEditorTarget(int ProcessId, IntPtr WindowHandle);
    private sealed record ImportSettingEditor(string Category, TextBox FolderBox, CheckBox RootCheckBox);
    private const uint GwHwndNext = 2;
    private const string AllCategories = "すべて";
    private static readonly Windows.UI.Color BoothAccentColor = Windows.UI.Color.FromArgb(255, 208, 87, 92);
    private readonly BoothLibraryReader _reader = new();
    private readonly UserMetadataStore _metadataStore = new();
    private readonly DuplicateDownloadService _duplicateDownloadService = new();
    private readonly UnityPackageImportService _unityPackageImportService = new();
    private readonly UnityEditorBridgeService _unityEditorBridgeService = new();
    private IReadOnlyList<LibraryItem> _allItems = [];
    private IReadOnlyList<AvatarProfile> _avatarProfiles = [];
    private Dictionary<string, HashSet<string>> _sharedBodyRelations = new(StringComparer.Ordinal);
    private LibraryItem? _detailItem;
    private string _selectedCategory = AllCategories;
    private readonly List<Button> _categoryTabButtons = [];
    private readonly List<(Button Button, FontIcon Check, string Value, string Label)> _sortOptionButtons = [];
    private readonly List<TextBox> _avatarIdentifierBoxes = [];
    private int _currentPage = 1;
    private int _pageSize = 50;
    private int _filteredItemCount;
    private string _sortKey = "Registered";
    private bool _sortDescending = true;
    private List<AvatarSelectionOption> _compatibilityOptions = [];
    private List<AvatarSelectionOption> _sharedBodyOptions = [];
    private string? _selectedAvatarFilterId;
    private string? _selectedShopFilter;
    private string? _selectedPurchasedPackType;
    private readonly Dictionary<string, HashSet<string>> _compatibilityFilterCache = new(StringComparer.Ordinal);
    private Dictionary<string, Dictionary<string, int>> _compatibilityOverrides = new(StringComparer.Ordinal);
    private string _categoryAtEditStart = AssetCategories.Other;
    private int _unityPackageLoadVersion;
    private volatile bool _isClosing;
    private IReadOnlyList<DownloadFileEntry> _downloadFiles = [];
    private Dictionary<string, CategoryImportSetting> _categoryImportSettings = new(StringComparer.Ordinal);
    private readonly List<ImportSettingEditor> _importSettingEditors = [];
    private bool _isApplyingSmartTitleSetting = true;
    private int _operationPopupVersion;

    public ObservableCollection<LibraryItem> VisibleItems { get; } = [];
    public ObservableCollection<AvatarFilterOption> AvatarFilterOptions { get; } = [];
    public ObservableCollection<AvatarFilterOption> VisibleAvatarFilterOptions { get; } = [];
    public ObservableCollection<AvatarSelectionOption> VisibleCompatibilityOptions { get; } = [];
    public ObservableCollection<AvatarSelectionOption> VisibleSharedBodyOptions { get; } = [];
    public ObservableCollection<ShopFilterOption> ShopFilterOptions { get; } = [];
    public ObservableCollection<ShopFilterOption> VisibleShopFilterOptions { get; } = [];
    public ObservableCollection<UnityPackageEntry> UnityPackages { get; } = [];
    public ObservableCollection<DownloadFileCategory> DownloadFileCategories { get; } = [];
    public ObservableCollection<DownloadFileEntry> VisibleDownloadFiles { get; } = [];
    public ObservableCollection<string> DownloadedProductNames { get; } = [];
    public IReadOnlyList<string> DetailCategories => AssetCategories.All;

    public MainWindow()
    {
        InitializeComponent();
        var windowIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(windowIconPath)) AppWindow.SetIcon(windowIconPath);
        PageSizeBox.SelectedItem = 50;
        InitializeSortMenu();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        AppWindow.Closing += (_, _) => _isClosing = true;
        Closed += (_, _) => _isClosing = true;
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        await LoadLibraryAsync();
    }

    private async Task LoadLibraryAsync()
    {
        ShowOperationPopup(OperationPopupKind.Progress, "ライブラリを同期中", "BOOTH Library Manager のデータを読み込んでいます…");
        try
        {
            var snapshot = await Task.Run(() => _reader.Read());
            _allItems = snapshot.Items;
            var metadata = await Task.Run(_metadataStore.ReadAll);
            var savedImportSettings = await Task.Run(_metadataStore.ReadCategoryImportSettings);
            var smartTitleShorteningEnabled = await Task.Run(_metadataStore.ReadSmartTitleShorteningEnabled);
            SmartTitleShorteningToggle.IsOn = smartTitleShorteningEnabled;
            foreach (var item in _allItems) item.SetSmartTitleShorteningEnabled(smartTitleShorteningEnabled);
            _categoryImportSettings = AssetCategories.All.ToDictionary(
                category => category,
                category => savedImportSettings.TryGetValue(category, out var saved)
                    ? saved
                    : new CategoryImportSetting(category, category, category is AssetCategories.Avatar or "ワールド"),
                StringComparer.Ordinal);
            _categoryImportSettings[AssetCategories.Avatar] = _categoryImportSettings[AssetCategories.Avatar] with { ImportToAssetsRoot = true };
            PopulateImportSettings();
            foreach (var item in _allItems)
            {
                if (metadata.TryGetValue(item.RegistrationId, out var saved))
                {
                    item.Category = AssetCategories.All.Contains(saved.Category) ? saved.Category : AssetCategories.Unclassified;
                    item.ImportToAssetsRoot = saved.ImportToAssetsRoot;
                    item.SupportsAllAvatars = saved.SupportsAllAvatars;
                }
                else item.Category = AssetClassifier.Classify(item);

                if (item.Category == AssetCategories.Avatar)
                    item.ImportToAssetsRoot = true;
            }
            await Task.Run(() => _metadataStore.SyncAvatarDefaults(_allItems));
            _avatarProfiles = await Task.Run(_metadataStore.ReadAvatarProfiles);
            _sharedBodyRelations = await Task.Run(_metadataStore.ReadSharedBodyRelations);
            _compatibilityOverrides = await Task.Run(_metadataStore.ReadAllCompatibilityOverrides);
            _compatibilityFilterCache.Clear();
            ApplyTitleCleanupIdentifiers();
            RefreshCompatibilityCounts();
            PopulateAvatarFilters();
            PopulateShopFilters();
            PopulateCategories();
            ApplyFilter();
            _isApplyingSmartTitleSetting = false;
            ShowOperationPopup(OperationPopupKind.Success, "同期が完了しました", $"{_allItems.Count:N0}件の商品を読み込みました。", autoDismiss: true);
        }
        catch (Exception ex)
        {
            if (_isClosing) return;
            _allItems = [];
            VisibleItems.Clear();
            EmptyState.Visibility = Visibility.Visible;
            EmptyTitle.Text = "BOOTH Library Managerを読み込めませんでした";
            EmptyDescription.Text = ex.Message;
            ShowOperationPopup(OperationPopupKind.Error, "ライブラリを読み込めませんでした", ex.Message);
            _isApplyingSmartTitleSetting = false;
        }
    }

    private async void SmartTitleShorteningToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingSmartTitleSetting) return;
        var enabled = SmartTitleShorteningToggle.IsOn;
        foreach (var item in _allItems) item.SetSmartTitleShorteningEnabled(enabled);
        PopulateAvatarFilters();
        PopulateShopFilters();
        ApplyFilter();
        UpdateDetailPanel();
        await Task.Run(() => _metadataStore.SaveSmartTitleShorteningEnabled(enabled));
        SmartTitleShorteningStatus.Text = enabled
            ? "商品名スマート短縮機能をオンにしました。"
            : "商品名スマート短縮機能をオフにしました。元の商品名を表示します。";
    }

    private void PopulateCategories()
    {
        CategoryTabs.Children.Clear();
        _categoryTabButtons.Clear();
        AddCategoryTab(AllCategories);
        foreach (var category in AssetCategories.All)
            AddCategoryTab(category);
    }

    private void PopulateAvatarFilters()
    {
        AvatarFilterOptions.Clear();
        AvatarFilterOptions.Add(new AvatarFilterOption(null, "すべての対応アバター", "すべての対応アバター"));
        foreach (var profile in _avatarProfiles.OrderBy(x => x.PrimaryIdentifier, StringComparer.CurrentCultureIgnoreCase))
        {
            var avatarItem = _allItems.FirstOrDefault(x => x.RegistrationId == profile.RegistrationId);
            AvatarFilterOptions.Add(new AvatarFilterOption(profile.RegistrationId, avatarItem?.DisplayName ?? profile.PrimaryIdentifier, profile.PrimaryIdentifier, avatarItem?.ThumbnailUrl));
        }
        var selected = AvatarFilterOptions.FirstOrDefault(x => x.RegistrationId == _selectedAvatarFilterId) ?? AvatarFilterOptions[0];
        _selectedAvatarFilterId = selected.RegistrationId;
        AvatarFilterButtonText.Text = selected.PrimaryIdentifier;
        ApplyAvatarFilterOptionSearch();
    }

    private void PopulateShopFilters()
    {
        ShopFilterOptions.Clear();
        ShopFilterOptions.Add(new ShopFilterOption(null, "すべてのショップ"));
        foreach (var group in _allItems.Where(x => !string.IsNullOrWhiteSpace(x.ShopName))
                     .GroupBy(x => x.ShopName, StringComparer.CurrentCultureIgnoreCase)
                     .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
            ShopFilterOptions.Add(new ShopFilterOption(group.Key, group.Key, group.Select(x => x.ShopThumbnailUrl).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))));
        var selected = ShopFilterOptions.FirstOrDefault(x => string.Equals(x.ShopName, _selectedShopFilter, StringComparison.CurrentCultureIgnoreCase)) ?? ShopFilterOptions[0];
        ShopFilterButtonText.Text = selected.DisplayName;
        ApplyShopFilterOptionSearch();
    }

    private void AddCategoryTab(string category)
    {
        var tab = new Button
        {
            Content = category,
            Tag = category,
            Style = (Style)RootLayout.Resources["CategoryTabButtonStyle"]
        };
        tab.Click += CategoryTab_Click;
        tab.PointerEntered += CategoryTab_PointerEntered;
        tab.PointerExited += CategoryTab_PointerExited;
        _categoryTabButtons.Add(tab);
        CategoryTabs.Children.Add(tab);
        UpdateCategoryTabAppearance(tab);
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var filtered = _allItems.Where(item =>
            (string.IsNullOrEmpty(query) || Contains(item.Name, query) || Contains(item.ShopName, query) || Contains(item.Tags, query)) &&
            (_selectedCategory == AllCategories || item.Category == _selectedCategory) &&
            (_selectedShopFilter is null || item.ShopName.Equals(_selectedShopFilter, StringComparison.CurrentCultureIgnoreCase)) &&
            (_selectedPurchasedPackType is null || item.PurchasedPackType == _selectedPurchasedPackType) &&
            MatchesAvatarFilter(item)).ToList();
        filtered = SortItems(filtered).ToList();

        _filteredItemCount = filtered.Count;
        var pageCount = Math.Max(1, (int)Math.Ceiling(_filteredItemCount / (double)_pageSize));
        _currentPage = Math.Clamp(_currentPage, 1, pageCount);
        var offset = (_currentPage - 1) * _pageSize;

        VisibleItems.Clear();
        foreach (var item in filtered.Skip(offset).Take(_pageSize)) VisibleItems.Add(item);
        UpdatePager(pageCount, offset);
        EmptyState.Visibility = VisibleItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (VisibleItems.Count == 0 && _allItems.Count > 0)
        {
            EmptyTitle.Text = "商品が見つかりません";
            EmptyDescription.Text = "検索条件を変更してください。";
        }
    }

    private static bool Contains(string value, string query) => value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    private static bool IsCompatibilityCategory(string category) =>
        category is "衣装" or "髪型" or "アクセサリー" or "テクスチャ" or "マテリアル" or "ギミック" or "アニメーション";
    private bool MatchesAvatarFilter(LibraryItem item)
    {
        if (_selectedAvatarFilterId is null) return true;
        if (item.RegistrationId == _selectedAvatarFilterId) return true;
        if (item.SupportsAllAvatars) return IsCompatibilityCategory(item.Category);
        if (!IsCompatibilityCategory(item.Category)) return false;
        if (!_compatibilityFilterCache.TryGetValue(item.RegistrationId, out var avatarIds))
        {
            avatarIds = GetEffectiveCompatibilityMatches(item).Select(x => x.AvatarRegistrationId).ToHashSet();
            _compatibilityFilterCache[item.RegistrationId] = avatarIds;
        }
        return avatarIds.Contains(_selectedAvatarFilterId);
    }

    private void AvatarFilterGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AvatarFilterOption option) return;
        _selectedAvatarFilterId = option.RegistrationId;
        AvatarFilterButtonText.Text = option.PrimaryIdentifier;
        AvatarFilterFlyout.Hide();
        _currentPage = 1;
        if (_allItems.Count > 0) ApplyFilter();
    }

    private void AvatarFilterSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyAvatarFilterOptionSearch();

    private void ApplyAvatarFilterOptionSearch()
    {
        if (AvatarFilterSearchBox is null) return;
        var query = AvatarFilterSearchBox.Text.Trim();
        VisibleAvatarFilterOptions.Clear();
        foreach (var option in AvatarFilterOptions.Where(x => string.IsNullOrWhiteSpace(query)
                     || Contains(x.DisplayName, query)
                     || (_avatarProfiles.FirstOrDefault(p => p.RegistrationId == x.RegistrationId)?.PrimaryIdentifier is string primary && Contains(primary, query))))
            VisibleAvatarFilterOptions.Add(option);
    }

    private void ShopFilterSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyShopFilterOptionSearch();

    private void ApplyShopFilterOptionSearch()
    {
        if (ShopFilterSearchBox is null) return;
        var query = ShopFilterSearchBox.Text.Trim();
        VisibleShopFilterOptions.Clear();
        foreach (var option in ShopFilterOptions.Where(x => string.IsNullOrWhiteSpace(query) || Contains(x.DisplayName, query)))
            VisibleShopFilterOptions.Add(option);
    }

    private void ShopFilterList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ShopFilterOption option) return;
        _selectedShopFilter = option.ShopName;
        ShopFilterButtonText.Text = option.DisplayName;
        ShopFilterFlyout.Hide();
        _currentPage = 1;
        ApplyFilter();
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadLibraryAsync();
    private void PurchasedPackFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPurchasedPackType = PurchasedPackFilterBox.SelectedIndex switch
        {
            1 => PurchasedPackClassifier.FullPack,
            2 => PurchasedPackClassifier.AvatarSpecific,
            3 => PurchasedPackClassifier.FreeDownload,
            _ => null
        };
        _currentPage = 1;
        if (_allItems.Count > 0) ApplyFilter();
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        _selectedCategory = AllCategories;
        _selectedAvatarFilterId = null;
        _selectedShopFilter = null;
        _selectedPurchasedPackType = null;
        AvatarFilterButtonText.Text = "すべての対応アバター";
        ShopFilterButtonText.Text = "すべてのショップ";
        AvatarFilterSearchBox.Text = string.Empty;
        ShopFilterSearchBox.Text = string.Empty;
        PurchasedPackFilterBox.SelectedIndex = 0;
        _currentPage = 1;
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        ApplyFilter();
    }
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _currentPage = 1;
        ApplyFilter();
    }
    private void CategoryTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string category })
        {
            _selectedCategory = category;
            _currentPage = 1;
            foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
            ApplyFilter();
        }
    }

    private void UpdatePager(int pageCount, int offset)
    {
        PageStatusText.Text = $"{_currentPage:N0} / {pageCount:N0} ページ";
        PreviousPageButton.IsEnabled = _currentPage > 1;
        NextPageButton.IsEnabled = _currentPage < pageCount;
        var first = _filteredItemCount == 0 ? 0 : offset + 1;
        var last = Math.Min(offset + _pageSize, _filteredItemCount);
        ResultRangeText.Text = $"{first:N0}–{last:N0} / {_filteredItemCount:N0}件";
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        ApplyFilter();
        ItemsGrid.ScrollIntoView(VisibleItems.FirstOrDefault());
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        var pageCount = Math.Max(1, (int)Math.Ceiling(_filteredItemCount / (double)_pageSize));
        if (_currentPage >= pageCount) return;
        _currentPage++;
        ApplyFilter();
        ItemsGrid.ScrollIntoView(VisibleItems.FirstOrDefault());
    }

    private void PageSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageSizeBox.SelectedItem is not int pageSize) return;
        _pageSize = pageSize;
        _currentPage = 1;
        if (_allItems.Count > 0) ApplyFilter();
    }

    private IEnumerable<LibraryItem> SortItems(IEnumerable<LibraryItem> items)
    {
        return (_sortKey, _sortDescending) switch
        {
            ("Name", false) => items.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            ("Name", true) => items.OrderByDescending(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            ("Shop", false) => items.OrderBy(x => x.ShopName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Name),
            ("Shop", true) => items.OrderByDescending(x => x.ShopName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Name),
            ("Registered", false) => items.OrderBy(x => x.RegisteredAt ?? DateTimeOffset.MaxValue),
            ("Registered", true) => items.OrderByDescending(x => x.RegisteredAt ?? DateTimeOffset.MinValue),
            ("Updated", false) => items.OrderBy(x => x.UpdatedAt ?? DateTimeOffset.MaxValue),
            ("Updated", true) => items.OrderByDescending(x => x.UpdatedAt ?? DateTimeOffset.MinValue),
            ("Published", false) => items.OrderBy(x => x.PublishedAt ?? DateTimeOffset.MaxValue),
            ("Published", true) => items.OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue),
            _ => items
        };
    }

    private void InitializeSortMenu()
    {
        var options = new[]
        {
            ("Name:Asc", "商品名 (A-Z)"), ("Name:Desc", "商品名 (Z-A)"),
            ("Shop:Asc", "ショップ名 (A-Z)"), ("Shop:Desc", "ショップ名 (Z-A)"),
            ("Registered:Desc", "登録日時 (新しい順)"), ("Registered:Asc", "登録日時 (古い順)"),
            ("Updated:Desc", "更新日時 (新しい順)"), ("Updated:Asc", "更新日時 (古い順)"),
            ("Published:Desc", "公開日時 (新しい順)"), ("Published:Asc", "公開日時 (古い順)")
        };

        for (var index = 0; index < options.Length; index++)
        {
            var (value, label) = options[index];
            var check = new FontIcon
            {
                Glyph = "\uE73E",
                FontSize = 12,
                Foreground = new SolidColorBrush(BoothAccentColor),
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            var content = new Grid { ColumnSpacing = 8 };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.Children.Add(check);
            var text = new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            content.Children.Add(text);

            var button = new Controls.HandCursorButton
            {
                Tag = value,
                Content = content,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 9, 8, 9),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            button.Click += SortOption_Click;
            Grid.SetColumn(button, index % 2);
            Grid.SetRow(button, index / 2);
            SortOptionsGrid.Children.Add(button);
            _sortOptionButtons.Add((button, check, value, label));
        }
        UpdateSortMenuSelection();
    }

    private void SortOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value }) return;
        var parts = value.Split(':', 2);
        _sortKey = parts[0];
        _sortDescending = parts.Length > 1 && parts[1] == "Desc";
        UpdateSortMenuSelection();
        SortFlyout.Hide();
        _currentPage = 1;
        if (_allItems.Count > 0) ApplyFilter();
    }

    private void UpdateSortMenuSelection()
    {
        var selectedValue = $"{_sortKey}:{(_sortDescending ? "Desc" : "Asc")}";
        foreach (var option in _sortOptionButtons)
        {
            var selected = option.Value == selectedValue;
            option.Check.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            if (selected) SortButtonText.Text = option.Label;
        }
    }

    private void UpdateCategoryTabAppearance(Button tab)
    {
        var selected = Equals(tab.Tag, _selectedCategory);
        var selectedBrush = _selectedCategory == AllCategories
            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : LibraryItem.GetCategoryBrush(_selectedCategory);
        tab.Background = selected ? selectedBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        tab.BorderBrush = selected ? selectedBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        tab.Foreground = selected
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        tab.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        tab.BorderThickness = selected ? new Thickness(0, 0, 0, 3) : new Thickness(0, 0, 0, 2);
    }

    private void CategoryTab_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not Button { Tag: string category } tab) return;
        tab.BorderBrush = category == AllCategories
            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : LibraryItem.GetCategoryBrush(category);
        tab.BorderThickness = new Thickness(0, 0, 0, 4);
    }

    private void CategoryTab_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button tab) UpdateCategoryTabAppearance(tab);
    }

    private void ItemsGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not LibraryItem item) return;
        try
        {
            _detailItem = item;
            UpdateDetailPanel();
            var wasOpen = DetailPanel.Visibility == Visibility.Visible;
            DetailPanel.Visibility = Visibility.Visible;
            DetailPanel.Width = 420;
            if (!wasOpen) OpenDetailPanelStoryboard.Begin();
            _ = LoadDownloadFilesAsync(item);
        }
        catch (Exception ex)
        {
            ShowOperationPopup(OperationPopupKind.Error, "詳細を表示できませんでした", ex.Message);
            Debug.WriteLine($"Detail panel error ({item.RegistrationId}): {ex}");
        }
    }

    private static readonly (string Key, string DisplayName, string Glyph, Windows.UI.Color Color)[] DownloadCategoryDefinitions =
    [
        ("UnityPackage", "Unityパッケージ", "\uE7B8", Windows.UI.Color.FromArgb(255, 91, 105, 166)),
        ("Texture", "テクスチャ", "\uEB9F", Windows.UI.Color.FromArgb(255, 67, 145, 181)),
        ("ImageSource", "画像編集データ", "\uE91B", Windows.UI.Color.FromArgb(255, 173, 92, 154)),
        ("ThreeD", "3Dデータ", "\uF158", Windows.UI.Color.FromArgb(255, 83, 151, 112)),
        ("Document", "ドキュメント", "\uE8A5", Windows.UI.Color.FromArgb(255, 184, 127, 60)),
        ("Other", "その他", "\uE8B7", Windows.UI.Color.FromArgb(255, 112, 118, 128))
    ];

    private async Task LoadDownloadFilesAsync(LibraryItem item)
    {
        var loadVersion = ++_unityPackageLoadVersion;
        DownloadFileCategories.Clear();
        DownloadFileTotalCountText.Text = "検索中";

        var files = await Task.Run(() => FindDownloadFiles(item.FolderPath));
        if (_isClosing || loadVersion != _unityPackageLoadVersion || _detailItem?.RegistrationId != item.RegistrationId) return;

        _downloadFiles = files;
        foreach (var definition in DownloadCategoryDefinitions)
        {
            var categoryFiles = files.Where(x => x.CategoryKey == definition.Key).ToList();
            var accentBrush = new SolidColorBrush(definition.Color);
            foreach (var file in categoryFiles) file.AccentBrush = accentBrush;
            DownloadFileCategories.Add(new DownloadFileCategory
            {
                Key = definition.Key,
                DisplayName = definition.DisplayName,
                Glyph = definition.Glyph,
                AccentBrush = accentBrush,
                Count = categoryFiles.Count,
                Subtitle = definition.Key == "UnityPackage"
                    ? ImportsToAssetsRoot(item)
                        ? "Unity配置先: Assets直下"
                        : $"Unity配置先: Assets/{GetImportFolderName(item.Category)}"
                    : string.Empty,
                Files = categoryFiles
            });
        }
        DownloadFileTotalCountText.Text = $"{files.Count}個";
    }

    private static IReadOnlyList<DownloadFileEntry> FindDownloadFiles(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return [];
        var paths = new List<string>();
        var directories = new Stack<string>();
        directories.Push(rootPath);
        while (directories.Count > 0)
        {
            var directory = directories.Pop();
            try
            {
                paths.AddRange(Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly));
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) directories.Push(child);
                    }
                    catch { }
                }
            }
            catch { }
        }

        var duplicateNames = paths.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var updateCandidates = DownloadUpdateCandidateService.FindUnityPackageCandidates(rootPath,
            paths.Where(path => Path.GetExtension(path).Equals(".unitypackage", StringComparison.OrdinalIgnoreCase)));
        return paths.Select(path =>
            {
                var relativeDirectory = Path.GetRelativePath(rootPath, Path.GetDirectoryName(path) ?? rootPath);
                return new DownloadFileEntry
                {
                    CategoryKey = ClassifyDownloadFile(path),
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    DirectoryText = relativeDirectory == "." ? "保存フォルダー直下" : relativeDirectory,
                    LastWriteTime = File.GetLastWriteTime(path),
                    HasDuplicateName = duplicateNames.Contains(Path.GetFileName(path)),
                    HasNewVersionCandidate = updateCandidates.Contains(path)
                };
            })
            .OrderBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(x => x.LastWriteTime)
            .ToList();
    }

    private static string ClassifyDownloadFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".unitypackage") return "UnityPackage";
        if (TextureExtensions.Contains(extension)) return "Texture";
        if (ImageSourceExtensions.Contains(extension)) return "ImageSource";
        if (ThreeDExtensions.Contains(extension)) return "ThreeD";
        if (DocumentExtensions.Contains(extension)) return "Document";
        return "Other";
    }

    private static readonly HashSet<string> TextureExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tga", ".tif", ".tiff", ".dds", ".exr", ".hdr" };
    private static readonly HashSet<string> ImageSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".psd", ".psb", ".clip", ".kra", ".xcf", ".sai", ".sai2", ".afphoto" };
    private static readonly HashSet<string> ThreeDExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".blend", ".fbx", ".obj", ".mtl", ".gltf", ".glb", ".dae", ".3ds", ".max", ".ma", ".mb", ".stl", ".ply" };
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".pdf", ".rtf", ".doc", ".docx", ".html", ".htm", ".csv", ".tsv", ".url" };

    private void DownloadFileCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing || sender is not FrameworkElement { Tag: DownloadFileCategory category }) return;
        var expand = !category.IsExpanded;
        foreach (var other in DownloadFileCategories) other.IsExpanded = false;
        category.IsExpanded = expand;
    }

    private void DownloadFile_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing || sender is not FrameworkElement { Tag: DownloadFileEntry file } || !File.Exists(file.FilePath)) return;
        if (file.CategoryKey == "UnityPackage")
        {
            UnityPackage_Click(sender, e);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/n,/select,\"{file.FilePath}\"",
            UseShellExecute = true
        });
    }

    private void DownloadFilesOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        DetailOpenFolder_Click(sender, e);
    }

    private async Task LoadUnityPackagesAsync(LibraryItem item)
    {
        var loadVersion = ++_unityPackageLoadVersion;
        UnityPackages.Clear();
        UnityPackageCountText.Text = "検索中";
        UnityPackageEmptyText.Visibility = Visibility.Collapsed;

        var packages = await Task.Run(() => FindUnityPackages(item.FolderPath));
        if (loadVersion != _unityPackageLoadVersion || _detailItem?.RegistrationId != item.RegistrationId) return;

        foreach (var package in packages) UnityPackages.Add(package);
        UnityPackageCountText.Text = $"{packages.Count}個";
        UnityPackageEmptyText.Visibility = packages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static IReadOnlyList<UnityPackageEntry> FindUnityPackages(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath)) return [];
        var paths = new List<string>();
        var directories = new Stack<string>();
        directories.Push(rootPath);
        while (directories.Count > 0)
        {
            var directory = directories.Pop();
            try
            {
                paths.AddRange(Directory.EnumerateFiles(directory, "*.unitypackage", SearchOption.TopDirectoryOnly));
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) directories.Push(child);
                    }
                    catch { }
                }
            }
            catch { }
        }

        var duplicateNames = paths.GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return paths.Select(path =>
            {
                var directory = Path.GetDirectoryName(path) ?? rootPath;
                var relativeDirectory = Path.GetRelativePath(rootPath, directory);
                return new UnityPackageEntry
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    DirectoryText = relativeDirectory == "." ? "保存フォルダー直下" : relativeDirectory,
                    LastWriteTime = File.GetLastWriteTime(path),
                    HasDuplicateName = duplicateNames.Contains(Path.GetFileName(path))
                };
            })
            .OrderBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(x => x.LastWriteTime)
            .ToList();
    }

    private async void UnityPackage_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing || _detailItem is null || sender is not FrameworkElement element) return;
        var package = element.Tag switch
        {
            UnityPackageEntry oldEntry => oldEntry,
            DownloadFileEntry file => new UnityPackageEntry
            {
                FilePath = file.FilePath,
                FileName = file.FileName,
                DirectoryText = file.DirectoryText,
                LastWriteTime = file.LastWriteTime,
                HasDuplicateName = file.HasDuplicateName
            },
            _ => null
        };
        if (package is null || !File.Exists(package.FilePath)) return;
        var clickedItem = _detailItem;
        var importToRoot = ImportsToAssetsRoot(clickedItem);
        var importFolderName = GetImportFolderName(clickedItem.Category);
        ShowOperationPopup(OperationPopupKind.Progress, "Unityインポートを準備中", importToRoot
            ? "Unityパッケージを開いています…"
            : $"Assets/{importFolderName} 配下へのインポートを準備しています…", 0);
        try
        {
            var unityTarget = FindActiveUnityEditor();
            if (unityTarget is null)
                throw new InvalidOperationException("起動中のUnity Editorを検出できません。先にインポート先のUnityプロジェクトを開いてください。");
            void ReportProgress(ImportPreparationProgress progress)
            {
                if (_isClosing) return;
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isClosing) return;
                    UpdateOperationPopupProgress(progress.Percentage, progress.Message);
                });
            }
            var importPackagePath = await Task.Run(() =>
                _unityPackageImportService.PrepareForImport(clickedItem, package.FilePath, importToRoot ? null : importFolderName, ReportProgress));
            if (_isClosing) return;
            await _unityEditorBridgeService.RequestImportAsync(unityTarget.ProcessId, importPackagePath);
            if (_isClosing) return;
            SetForegroundWindow(unityTarget.WindowHandle);
            ShowOperationPopup(OperationPopupKind.Success, "Unityへ送信しました", "初回はスクリプトのコンパイル後にインポートを開始します。", 100, autoDismiss: true);
        }
        catch (Exception ex)
        {
            if (_isClosing) return;
            ShowOperationPopup(OperationPopupKind.Error, "Unityインポートに失敗しました", ex.Message);
        }
    }

    private static UnityEditorTarget? FindActiveUnityEditor()
    {
        // Clicking this app makes it the foreground window. The first visible Unity window behind
        // it in Z order is the editor the user was most recently working with.
        var window = GetForegroundWindow();
        while (window != IntPtr.Zero)
        {
            window = GetWindow(window, GwHwndNext);
            if (window == IntPtr.Zero) break;
            GetWindowThreadProcessId(window, out var processId);
            if (processId == 0) continue;
            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (!process.ProcessName.Equals("Unity", StringComparison.OrdinalIgnoreCase)
                    || process.MainWindowHandle == IntPtr.Zero)
                    continue;
                return new UnityEditorTarget(process.Id, process.MainWindowHandle);
            }
            catch { }
        }

        foreach (var process in Process.GetProcessesByName("Unity"))
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero) continue;
                    return new UnityEditorTarget(process.Id, process.MainWindowHandle);
                }
                catch { }
            }
        }
        return null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    private void UpdateDetailPanel()
    {
        if (_detailItem is not { } item) return;
        DetailName.Text = item.DisplayName;
        DetailShop.Text = item.ShopName;
        DetailShopThumbnail.Source = CreateImageSource(item.ShopThumbnailUrl);
        DetailCategoryText.Text = item.Category;
        DetailCategoryBadge.Background = item.CategoryBadgeBrush;
        DetailPlacement.Text = ImportsToAssetsRoot(item)
            ? "Unity配置先: Assets直下"
            : $"Unity配置先: Assets/{GetImportFolderName(item.Category)}";
        var matches = GetEffectiveCompatibilityMatches(item);
        var displayedMatches = matches.Where(x => !x.ThroughBaseBody).ToList();
        var isAvatar = item.Category == AssetCategories.Avatar;
        var hasCompatibility = IsCompatibilityCategory(item.Category);
        DetailCompatibilityTitle.Visibility = hasCompatibility ? Visibility.Visible : Visibility.Collapsed;
        DetailCompatibility.Visibility = hasCompatibility ? Visibility.Visible : Visibility.Collapsed;
        DetailPurchasedPackBadge.Visibility = item.PurchasedPackBadgeVisibility;
        DetailPurchasedPackBadge.Background = item.PurchasedPackBadgeBrush;
        DetailPurchasedPackBadge.BorderBrush = item.PurchasedPackBadgeBorderBrush;
        DetailPurchasedPackText.Text = item.PurchasedPackType;
        DetailPurchasedPackText.Foreground = item.PurchasedPackBadgeForegroundBrush;
        DetailTagScroller.Visibility = isAvatar ? Visibility.Visible : Visibility.Collapsed;
        IReadOnlyList<DetailTagChip> compatibilityChips = item.SupportsAllAvatars
            ? [new DetailTagChip("全アバター対応", null, false)]
            : matches.Count == 0
            ? [new DetailTagChip("検出なし", null, false)]
            : displayedMatches.Select((x, index) => new DetailTagChip(
                GetAvatarPrimaryIdentifier(x.AvatarRegistrationId), x.AvatarRegistrationId, true, index == 0 ? "" : "/")).ToList();
        DetailCompatibility.ItemsSource = compatibilityChips;
        var evidenceText = item.SupportsAllAvatars
            ? "全アバター対応として設定されています。"
            : string.Join("\n", displayedMatches.Select(x => $"• {GetAvatarPrimaryIdentifier(x.AvatarRegistrationId)}（{x.Evidence}）"));
        DetailCompatibilityEvidenceIcon.Visibility = hasCompatibility && (item.SupportsAllAvatars || displayedMatches.Count > 0)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ToolTipService.SetToolTip(DetailCompatibilityEvidenceIcon, new TextBlock
        {
            Text = evidenceText,
            MaxWidth = 360,
            TextWrapping = TextWrapping.Wrap
        });
        var avatarProfile = isAvatar ? _avatarProfiles.FirstOrDefault(x => x.RegistrationId == item.RegistrationId) : null;
        DetailTagChips.ItemsSource = avatarProfile is null ? null : GetAvatarDetailTags(avatarProfile);
        DetailOpenBoothButton.IsEnabled = item.BoothUrl is not null;
        DetailThumbnail.Source = CreateImageSource(item.ThumbnailUrl);
    }

    private async void DetailDownloadedProducts_Click(object sender, RoutedEventArgs e)
    {
        DownloadedProductNames.Clear();
        if (_detailItem is not null)
            foreach (var name in _detailItem.DownloadedVariationNames) DownloadedProductNames.Add(name);
        DownloadedProductsEmptyText.Visibility = DownloadedProductNames.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        await DownloadedProductsDialog.ShowAsync();
    }

    private async void EditDetail_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem is null) return;
        _categoryAtEditStart = _detailItem.Category;
        DetailCategory.SelectedItem = _detailItem.Category;
        UpdateImportRootEditor(_detailItem.Category, _detailItem.ImportToAssetsRoot);
        var isAvatar = _detailItem.Category == AssetCategories.Avatar;
        AvatarIdentifierEditor.Visibility = isAvatar ? Visibility.Visible : Visibility.Collapsed;
        CompatibilityEditor.Visibility = IsCompatibilityCategory(_detailItem.Category) ? Visibility.Visible : Visibility.Collapsed;
        if (isAvatar && _avatarProfiles.FirstOrDefault(x => x.RegistrationId == _detailItem.RegistrationId) is { } avatar)
        {
            AvatarPrimaryIdentifierBox.Text = avatar.PrimaryIdentifier;
            AvatarIdentifierRows.Children.Clear();
            _avatarIdentifierBoxes.Clear();
            foreach (var identifier in avatar.Identifiers) AddAvatarIdentifierRow(identifier);
            var relatedIds = _sharedBodyRelations.TryGetValue(avatar.RegistrationId, out var savedRelatedIds)
                ? savedRelatedIds
                : [];
            _sharedBodyOptions = _avatarProfiles.Where(x => x.RegistrationId != avatar.RegistrationId)
                .Select(profile =>
                {
                    var avatarItem = _allItems.FirstOrDefault(x => x.RegistrationId == profile.RegistrationId);
                    return new AvatarSelectionOption(profile, relatedIds.Contains(profile.RegistrationId), avatarItem?.DisplayName ?? profile.PrimaryIdentifier, avatarItem?.ThumbnailUrl);
                }).ToList();
            SharedBodySearchBox.Text = string.Empty;
            ApplySharedBodyOptionSearch();
            UpdateSharedBodySelectionSummary();
        }
        var effectiveIds = GetEffectiveCompatibilityMatches(_detailItem).Select(x => x.AvatarRegistrationId).ToHashSet();
        _compatibilityOptions = _avatarProfiles
            .Select(profile =>
            {
                var avatarItem = _allItems.FirstOrDefault(x => x.RegistrationId == profile.RegistrationId);
                return new AvatarSelectionOption(profile, effectiveIds.Contains(profile.RegistrationId), avatarItem?.DisplayName ?? profile.PrimaryIdentifier, avatarItem?.ThumbnailUrl);
            })
            .ToList();
        CompatibilitySearchBox.Text = string.Empty;
        ApplyCompatibilityOptionSearch();
        AllAvatarsCheckBox.IsChecked = _detailItem.SupportsAllAvatars;
        UpdateCompatibilitySelectionSummary();
        await DetailDialog.ShowAsync();
    }

    private async void CloseDetailPanel_Click(object sender, RoutedEventArgs e) => await CloseDetailPanelAsync();

    private void AddAvatarIdentifier_Click(object sender, RoutedEventArgs e) => AddAvatarIdentifierRow(string.Empty);

    private void AddAvatarIdentifierRow(string value)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var textBox = new TextBox { Text = value, PlaceholderText = "識別タグを入力" };
        row.Children.Add(textBox);
        var removeButton = new Controls.HandCursorButton
        {
            Content = new FontIcon { Glyph = "\uE738", FontSize = 14 },
            Tag = row,
            Padding = new Thickness(10)
        };
        ToolTipService.SetToolTip(removeButton, "この識別タグを削除");
        removeButton.Click += RemoveAvatarIdentifier_Click;
        Grid.SetColumn(removeButton, 1);
        row.Children.Add(removeButton);
        AvatarIdentifierRows.Children.Add(row);
        _avatarIdentifierBoxes.Add(textBox);
        if (string.IsNullOrEmpty(value)) textBox.Focus(FocusState.Programmatic);
    }

    private void RemoveAvatarIdentifier_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Grid row }) return;
        if (row.Children.OfType<TextBox>().FirstOrDefault() is { } textBox)
            _avatarIdentifierBoxes.Remove(textBox);
        AvatarIdentifierRows.Children.Remove(row);
    }

    private async Task CloseDetailPanelAsync()
    {
        CloseDetailPanelStoryboard.Begin();
        await Task.Delay(210);
        DetailPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Width = 0;
    }

    private void ItemsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        const double minimumCardWidth = 180;
        const double cardOuterSpacing = 8;
        var usableWidth = Math.Max(minimumCardWidth + cardOuterSpacing, e.NewSize.Width - 2);
        var columns = Math.Max(1, (int)Math.Floor(usableWidth / (minimumCardWidth + cardOuterSpacing)));
        var cardWidth = Math.Floor(usableWidth / columns) - cardOuterSpacing;
        foreach (var item in _allItems) item.CardWidth = Math.Max(minimumCardWidth, cardWidth);
    }

    private void LibraryMenu_Click(object sender, RoutedEventArgs e) => SetActivePage(showLibrary: true);
    private async void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DetailPanel.Visibility == Visibility.Visible) await CloseDetailPanelAsync();
        SetActivePage(showLibrary: false);
        await Task.CompletedTask;
    }

    private void PopulateImportSettings()
    {
        ImportSettingsRows.Children.Clear();
        _importSettingEditors.Clear();
        foreach (var category in AssetCategories.All)
        {
            var setting = GetCategoryImportSetting(category);
            var folderBox = new TextBox
            {
                Text = setting.FolderName,
                PlaceholderText = category,
                IsEnabled = !setting.ImportToAssetsRoot,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var rootCheckBox = new CheckBox
            {
                Content = "Assets直下",
                IsChecked = setting.ImportToAssetsRoot,
                IsEnabled = category != AssetCategories.Avatar,
                VerticalAlignment = VerticalAlignment.Center
            };
            rootCheckBox.Checked += (_, _) => folderBox.IsEnabled = false;
            rootCheckBox.Unchecked += (_, _) => folderBox.IsEnabled = true;
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            var label = new TextBlock { Text = category, VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            Grid.SetColumn(folderBox, 1);
            Grid.SetColumn(rootCheckBox, 2);
            row.Children.Add(label);
            row.Children.Add(folderBox);
            row.Children.Add(rootCheckBox);
            ImportSettingsRows.Children.Add(row);
            _importSettingEditors.Add(new ImportSettingEditor(category, folderBox, rootCheckBox));
        }
    }

    private async void SaveImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var settings = new List<CategoryImportSetting>();
        foreach (var editor in _importSettingEditors)
        {
            var importRoot = editor.Category == AssetCategories.Avatar || editor.RootCheckBox.IsChecked == true;
            var folderName = editor.FolderBox.Text.Trim();
            if (!importRoot && (string.IsNullOrWhiteSpace(folderName)
                || folderName.IndexOfAny(invalidCharacters) >= 0
                || folderName is "." or ".."
                || folderName.Equals("Assets", StringComparison.OrdinalIgnoreCase)))
            {
                ImportSettingsStatus.Text = $"{editor.Category}のフォルダ名を確認してください。フォルダ名には使用できない文字があります。";
                editor.FolderBox.Focus(FocusState.Programmatic);
                return;
            }
            settings.Add(new CategoryImportSetting(editor.Category, string.IsNullOrWhiteSpace(folderName) ? editor.Category : folderName, importRoot));
        }

        await Task.Run(() => _metadataStore.SaveCategoryImportSettings(settings));
        _categoryImportSettings = settings.ToDictionary(x => x.Category, StringComparer.Ordinal);
        ImportSettingsStatus.Text = "Unityインポート先設定を保存しました。";
        if (_detailItem is not null)
        {
            UpdateDetailPanel();
            _ = LoadDownloadFilesAsync(_detailItem);
        }
    }

    private CategoryImportSetting GetCategoryImportSetting(string category) =>
        _categoryImportSettings.TryGetValue(category, out var setting)
            ? setting
            : new CategoryImportSetting(category, category, category is AssetCategories.Avatar or "ワールド");

    private bool ImportsToAssetsRoot(LibraryItem item) =>
        item.Category == AssetCategories.Avatar || item.ImportToAssetsRoot || GetCategoryImportSetting(item.Category).ImportToAssetsRoot;

    private string GetImportFolderName(string category) => GetCategoryImportSetting(category).FolderName;

    private void SetActivePage(bool showLibrary)
    {
        var libraryVisibility = showLibrary ? Visibility.Visible : Visibility.Collapsed;
        LibraryHeader.Visibility = libraryVisibility; LibraryToolbar.Visibility = libraryVisibility;
        LibraryContent.Visibility = libraryVisibility; LibraryPager.Visibility = libraryVisibility;
        SettingsPage.Visibility = showLibrary ? Visibility.Collapsed : Visibility.Visible;
        LibraryMenuButton.Background = new SolidColorBrush(showLibrary ? BoothAccentColor : Microsoft.UI.Colors.Transparent);
        LibraryMenuButton.Foreground = new SolidColorBrush(showLibrary ? Microsoft.UI.Colors.White : BoothAccentColor);
        SettingsMenuButton.Background = new SolidColorBrush(showLibrary ? Microsoft.UI.Colors.Transparent : BoothAccentColor);
        SettingsMenuButton.Foreground = new SolidColorBrush(showLibrary ? BoothAccentColor : Microsoft.UI.Colors.White);
    }

    private async void DetailDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_detailItem is null) return;
        _detailItem.Category = DetailCategory.SelectedItem as string ?? AssetCategories.Unclassified;
        var stoppedBeingAvatar = _categoryAtEditStart == AssetCategories.Avatar
            && _detailItem.Category != AssetCategories.Avatar;
        var categoryUsesRoot = GetCategoryImportSetting(_detailItem.Category).ImportToAssetsRoot;
        _detailItem.ImportToAssetsRoot = _detailItem.Category == AssetCategories.Avatar
            || (!categoryUsesRoot && DetailImportRoot.IsChecked == true);
        var becameCompatibilityCategory = !IsCompatibilityCategory(_categoryAtEditStart) && IsCompatibilityCategory(_detailItem.Category);
        if (becameCompatibilityCategory)
        {
            await Task.Run(() => _metadataStore.ResetCompatibilityOverrides(_detailItem.RegistrationId));
            _detailItem.SupportsAllAvatars = false;
        }
        else
        {
            _detailItem.SupportsAllAvatars = IsCompatibilityCategory(_detailItem.Category) && AllAvatarsCheckBox.IsChecked == true;
        }
        await Task.Run(() => _metadataStore.Save(_detailItem));
        if (stoppedBeingAvatar)
        {
            await Task.Run(() => _metadataStore.SyncAvatarDefaults(_allItems));
            _avatarProfiles = await Task.Run(_metadataStore.ReadAvatarProfiles);
            _sharedBodyRelations = await Task.Run(_metadataStore.ReadSharedBodyRelations);
            _compatibilityFilterCache.Clear();
            ApplyTitleCleanupIdentifiers();
            PopulateAvatarFilters();
        }
        if (_detailItem.Category == AssetCategories.Avatar && _avatarProfiles.FirstOrDefault(x => x.RegistrationId == _detailItem.RegistrationId) is { } avatar)
        {
            var primary = AvatarPrimaryIdentifierBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(primary)) { args.Cancel = true; return; }
            var identifiers = _avatarIdentifierBoxes.Select(x => x.Text.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !x.Equals(primary, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            await Task.Run(() => _metadataStore.SaveAvatarProfile(avatar with { PrimaryIdentifier = primary, Identifiers = identifiers }));
            var sharedIds = _sharedBodyOptions.Where(x => x.IsSelected).Select(x => x.Profile.RegistrationId).ToList();
            await Task.Run(() => _metadataStore.SaveSharedBodyRelations(avatar.RegistrationId, sharedIds));
            _avatarProfiles = await Task.Run(_metadataStore.ReadAvatarProfiles);
            _sharedBodyRelations = await Task.Run(_metadataStore.ReadSharedBodyRelations);
            _compatibilityFilterCache.Clear();
            ApplyTitleCleanupIdentifiers();
            PopulateAvatarFilters();
        }
        if (IsCompatibilityCategory(_detailItem.Category) && !becameCompatibilityCategory)
        {
            var automaticIds = AvatarCompatibilityService.Detect(_detailItem, _avatarProfiles, _sharedBodyRelations).Select(x => x.AvatarRegistrationId).ToHashSet();
            var selectedIds = _compatibilityOptions.Where(x => x.IsSelected)
                .Select(x => x.Profile.RegistrationId).ToHashSet();
            var states = new Dictionary<string, int>();
            foreach (var profile in _avatarProfiles)
            {
                var isAutomatic = automaticIds.Contains(profile.RegistrationId);
                var isSelected = selectedIds.Contains(profile.RegistrationId);
                if (isSelected && !isAutomatic) states[profile.RegistrationId] = 1;
                else if (!isSelected && isAutomatic) states[profile.RegistrationId] = -1;
            }
            await Task.Run(() => _metadataStore.SaveCompatibilityOverrides(_detailItem.RegistrationId, states));
        }
        _compatibilityOverrides = await Task.Run(_metadataStore.ReadAllCompatibilityOverrides);
        _compatibilityFilterCache.Clear();
        RefreshCompatibilityCounts();
        PopulateCategories();
        ApplyFilter();
        UpdateDetailPanel();
    }

    private IReadOnlyList<CompatibilityMatch> GetEffectiveCompatibilityMatches(LibraryItem item)
    {
        var automatic = AvatarCompatibilityService.Detect(item, _avatarProfiles, _sharedBodyRelations).ToDictionary(x => x.AvatarRegistrationId);
        IReadOnlyDictionary<string, int> overrides = _compatibilityOverrides.TryGetValue(item.RegistrationId, out var savedOverrides)
            ? savedOverrides : new Dictionary<string, int>();
        var result = new List<CompatibilityMatch>();
        foreach (var profile in _avatarProfiles)
        {
            if (overrides.TryGetValue(profile.RegistrationId, out var state))
            {
                if (state == 1) result.Add(new CompatibilityMatch(profile.RegistrationId, profile.PrimaryIdentifier, "手動確定・追加", false));
                continue;
            }
            if (automatic.TryGetValue(profile.RegistrationId, out var match)) result.Add(match);
        }
        return result;
    }

    private string GetAvatarPrimaryIdentifier(string registrationId) =>
        _avatarProfiles.FirstOrDefault(x => x.RegistrationId == registrationId)?.PrimaryIdentifier ?? registrationId;

    private void DetailCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var category = DetailCategory.SelectedItem as string ?? AssetCategories.Other;
        UpdateImportRootEditor(category, false);
        CompatibilityEditor.Visibility = IsCompatibilityCategory(category) ? Visibility.Visible : Visibility.Collapsed;
        AvatarIdentifierEditor.Visibility = category == AssetCategories.Avatar ? Visibility.Visible : Visibility.Collapsed;
        UpdateEditImportDestination();
    }

    private void DetailImportRoot_Changed(object sender, RoutedEventArgs e) => UpdateEditImportDestination();

    private void UpdateEditImportDestination()
    {
        if (EditImportDestinationText is null || DetailCategory is null || DetailImportRoot is null) return;
        var category = DetailCategory.SelectedItem as string ?? AssetCategories.Other;
        var categorySetting = GetCategoryImportSetting(category);
        EditImportDestinationText.Text = category == AssetCategories.Avatar || categorySetting.ImportToAssetsRoot || DetailImportRoot.IsChecked == true
            ? "現在の配置先: Assets直下"
            : $"現在の配置先: Assets/{categorySetting.FolderName}";
    }

    private void UpdateImportRootEditor(string category, bool currentValue)
    {
        var isAvatar = category == AssetCategories.Avatar;
        var categoryUsesRoot = GetCategoryImportSetting(category).ImportToAssetsRoot;
        DetailImportRoot.IsChecked = isAvatar || categoryUsesRoot || currentValue;
        DetailImportRoot.IsEnabled = !isAvatar && !categoryUsesRoot;
        DetailImportRoot.Content = isAvatar
            ? "Assets直下に配置する（アバターは変更できません）"
            : categoryUsesRoot
            ? "Assets直下に配置する（分類設定で指定されています）"
            : "Assets直下に配置する";
        UpdateEditImportDestination();
    }

    private void DetailOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem is null) return;
        if (!Directory.Exists(_detailItem.FolderPath))
        {
            ShowOperationPopup(OperationPopupKind.Error, "フォルダーが見つかりません", _detailItem.FolderPath);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", _detailItem.FolderPath) { UseShellExecute = true });
    }

    private void DetailOpenBooth_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem?.BoothUrl is not string url) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void DetailShop_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem is null || string.IsNullOrWhiteSpace(_detailItem.ShopName)) return;
        SearchBox.Text = string.Empty;
        _selectedCategory = AllCategories;
        _selectedAvatarFilterId = null;
        AvatarFilterButtonText.Text = "すべての対応アバター";
        _selectedShopFilter = _detailItem.ShopName;
        ShopFilterButtonText.Text = _detailItem.ShopName;
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        _currentPage = 1;
        ApplyFilter();
    }

    private void DetailShop_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element) element.Opacity = 0.72;
    }

    private void DetailShop_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element) element.Opacity = 1;
    }

    private void IdentifierChip_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DetailTagChip { IsClickable: true, AvatarRegistrationId: not null } chip }) return;
        _selectedAvatarFilterId = chip.AvatarRegistrationId;
        AvatarFilterButtonText.Text = GetAvatarPrimaryIdentifier(chip.AvatarRegistrationId);
        SearchBox.Text = string.Empty;
        _selectedShopFilter = null;
        ShopFilterButtonText.Text = "すべてのショップ";
        _selectedCategory = AllCategories;
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        _currentPage = 1;
        ApplyFilter();
    }

    private List<DetailTagChip> GetAvatarDetailTags(AvatarProfile avatar)
    {
        var tags = new[] { avatar.PrimaryIdentifier }.Concat(avatar.Identifiers)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select((x, index) => new DetailTagChip(x, index == 0 ? avatar.RegistrationId : null, index == 0)).ToList();
        if (_sharedBodyRelations.TryGetValue(avatar.RegistrationId, out var relatedIds))
            tags.AddRange(_avatarProfiles
                .Where(x => relatedIds.Contains(x.RegistrationId))
                .Select(x => new DetailTagChip($"{x.PrimaryIdentifier}と共通素体", null, false)));
        return tags.DistinctBy(x => x.Text, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void SharedBodyOption_Changed(object sender, RoutedEventArgs e) => UpdateSharedBodySelectionSummary();

    private void SharedBodySearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySharedBodyOptionSearch();

    private void ApplySharedBodyOptionSearch()
    {
        if (SharedBodySearchBox is null) return;
        var query = SharedBodySearchBox.Text.Trim();
        VisibleSharedBodyOptions.Clear();
        foreach (var option in _sharedBodyOptions.Where(x => string.IsNullOrWhiteSpace(query)
                     || Contains(x.DisplayName, query) || Contains(x.PrimaryIdentifier, query)))
            VisibleSharedBodyOptions.Add(option);
    }

    private void UpdateSharedBodySelectionSummary()
    {
        if (SharedBodySelectionSummary is null) return;
        var selected = _sharedBodyOptions.Where(x => x.IsSelected).Select(x => x.PrimaryIdentifier).ToList();
        SharedBodySelectionSummary.Text = selected.Count switch
        {
            0 => "選択なし",
            <= 2 => string.Join("、", selected),
            _ => $"{string.Join("、", selected.Take(2))} ほか{selected.Count - 2}件"
        };
    }

    private void CompatibilityOption_Changed(object sender, RoutedEventArgs e) => UpdateCompatibilitySelectionSummary();

    private void CompatibilitySearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyCompatibilityOptionSearch();

    private void ApplyCompatibilityOptionSearch()
    {
        if (CompatibilitySearchBox is null) return;
        var query = CompatibilitySearchBox.Text.Trim();
        VisibleCompatibilityOptions.Clear();
        foreach (var option in _compatibilityOptions.Where(x => string.IsNullOrWhiteSpace(query)
                     || Contains(x.DisplayName, query) || Contains(x.PrimaryIdentifier, query)))
            VisibleCompatibilityOptions.Add(option);
    }

    private void AllAvatarsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (CompatibilitySelectionButton is null) return;
        CompatibilitySelectionButton.IsEnabled = AllAvatarsCheckBox.IsChecked != true;
        UpdateCompatibilitySelectionSummary();
    }

    private void UpdateCompatibilitySelectionSummary()
    {
        if (CompatibilitySelectionSummary is null || CompatibilitySelectionButton is null) return;
        CompatibilitySelectionButton.IsEnabled = AllAvatarsCheckBox?.IsChecked != true;
        if (AllAvatarsCheckBox?.IsChecked == true)
        {
            CompatibilitySelectionSummary.Text = "全アバター対応";
            return;
        }
        var selected = _compatibilityOptions.Where(x => x.IsSelected).Select(x => x.PrimaryIdentifier).ToList();
        CompatibilitySelectionSummary.Text = selected.Count switch
        {
            0 => "選択なし",
            <= 2 => string.Join("、", selected),
            _ => $"{string.Join("、", selected.Take(2))} ほか{selected.Count - 2}件"
        };
    }

    private async void ReloadItem_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem is null) return;
        DetailOperationsFlyout.Hide();
        ShowOperationPopup(OperationPopupKind.Progress, "商品データを更新中", "BOOTH Library Manager のデータを再取得しています…");
        try
        {
            var snapshot = await Task.Run(() => _reader.Read());
            var refreshed = snapshot.Items.FirstOrDefault(x => x.RegistrationId == _detailItem.RegistrationId);
            if (refreshed is not null) CopySourceInformation(refreshed, _detailItem);
            if (_detailItem.Category == AssetCategories.Avatar)
                await Task.Run(() => _metadataStore.ResetAutomaticAvatarIdentifiers(_detailItem));
            await Task.Run(() => _metadataStore.SyncAvatarDefaults(_allItems));
            _avatarProfiles = await Task.Run(_metadataStore.ReadAvatarProfiles);
            _sharedBodyRelations = await Task.Run(_metadataStore.ReadSharedBodyRelations);
            _compatibilityOverrides = await Task.Run(_metadataStore.ReadAllCompatibilityOverrides);
            _compatibilityFilterCache.Clear();
            ApplyTitleCleanupIdentifiers();
            RefreshCompatibilityCounts();
            PopulateAvatarFilters();
            PopulateShopFilters();
            UpdateDetailPanel();
            ShowOperationPopup(OperationPopupKind.Success, "更新が完了しました", "商品データを再取得しました。", autoDismiss: true);
        }
        catch (Exception ex)
        {
            if (_isClosing) return;
            ShowOperationPopup(OperationPopupKind.Error, "再読み込みに失敗しました", ex.Message);
        }
    }

    private async void ResetCompatibility_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem is null) return;
        DetailOperationsFlyout.Hide();
        await Task.Run(() => _metadataStore.ResetCompatibilityOverrides(_detailItem.RegistrationId));
        _detailItem.SupportsAllAvatars = false;
        await Task.Run(() => _metadataStore.Save(_detailItem));
        _compatibilityOverrides = await Task.Run(_metadataStore.ReadAllCompatibilityOverrides);
        _compatibilityFilterCache.Clear();
        RefreshCompatibilityCounts();
        UpdateDetailPanel();
        ApplyFilter();
        ShowOperationPopup(OperationPopupKind.Success, "対応設定をリセットしました", "手動追加・除外と全アバター対応設定を初期化しました。", autoDismiss: true);
    }

    private async void CheckDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (_detailItem is null) return;
        DetailOperationsFlyout.Hide();
        ShowOperationPopup(OperationPopupKind.Progress, "重複を確認中", "ダウンロード済みファイルを調べています…");
        await CheckDuplicatesAsync(_detailItem);
    }

    private async Task CheckDuplicatesAsync(LibraryItem item)
    {
        var duplicates = await Task.Run(() => _duplicateDownloadService.FindDuplicateDownloads(item.FolderPath));
        if (duplicates.Count == 0) { ShowOperationPopup(OperationPopupKind.Information, "重複はありません", "重複サフィックス付きのダウンロードは見つかりませんでした。", autoDismiss: true); return; }
        var names = string.Join("\n", duplicates.Select(x => $"• 削除: {Path.GetFileName(x.DeletePath)}").Distinct());
        var kept = string.Join("\n", duplicates.GroupBy(x => x.KeepPath).Select(x => $"• 保持: {Path.GetFileName(x.Key)} → {Path.GetFileName(x.First().TargetPath)}"));
        var dialog = new ContentDialog { XamlRoot = RootLayout.XamlRoot, Title = $"重複ダウンロードが {duplicates.Count} 件あります", Content = $"最新のフォルダーだけを残し、重複サフィックスを外します。古いフォルダーはごみ箱へ移動します。\n\n{kept}\n{names}", PrimaryButtonText = "整理する", CloseButtonText = "キャンセル", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await Task.Run(() => _duplicateDownloadService.KeepLatestAndNormalizeName(duplicates));
            ShowOperationPopup(OperationPopupKind.Success, "重複を整理しました", $"最新のみを残し、古い重複{duplicates.Count}件をごみ箱へ移動しました。", autoDismiss: true);
        }
        else ShowOperationPopup(OperationPopupKind.Information, "整理をキャンセルしました", "ファイルは変更されていません。", autoDismiss: true);
    }

    private void ShowOperationPopup(OperationPopupKind kind, string title, string message, double? progress = null, bool autoDismiss = false)
    {
        if (_isClosing) return;
        var version = ++_operationPopupVersion;
        var (glyph, accent, pale) = kind switch
        {
            OperationPopupKind.Success => ("\uE73E", Windows.UI.Color.FromArgb(255, 32, 155, 96), Windows.UI.Color.FromArgb(24, 32, 155, 96)),
            OperationPopupKind.Information => ("\uE946", Windows.UI.Color.FromArgb(255, 47, 126, 213), Windows.UI.Color.FromArgb(24, 47, 126, 213)),
            OperationPopupKind.Error => ("\uEA39", Windows.UI.Color.FromArgb(255, 208, 67, 74), Windows.UI.Color.FromArgb(28, 208, 67, 74)),
            _ => ("\uE895", Windows.UI.Color.FromArgb(255, 91, 105, 166), Windows.UI.Color.FromArgb(24, 91, 105, 166))
        };
        OperationPopupIcon.Glyph = glyph;
        OperationPopupIcon.FontSize = kind == OperationPopupKind.Error ? 34 : 30;
        OperationPopupIcon.Foreground = new SolidColorBrush(accent);
        OperationPopupIconBackground.Background = new SolidColorBrush(pale);
        OperationPopupAccent.Background = new SolidColorBrush(accent);
        OperationPopupProgress.Foreground = new SolidColorBrush(accent);
        OperationPopupTitle.Text = title;
        OperationPopupMessage.Text = message;
        OperationPopupProgress.Visibility = kind == OperationPopupKind.Progress ? Visibility.Visible : Visibility.Collapsed;
        OperationPopupProgress.IsIndeterminate = kind == OperationPopupKind.Progress && progress is null;
        if (progress is not null) OperationPopupProgress.Value = progress.Value;
        OperationPopup.Visibility = Visibility.Visible;
        AnimateOperationPopup(show: true);
        if (autoDismiss) _ = AutoDismissOperationPopupAsync(version);
    }

    private void UpdateOperationPopupProgress(double progress, string message)
    {
        OperationPopupMessage.Text = $"{progress:0}%  {message}";
        OperationPopupProgress.IsIndeterminate = false;
        OperationPopupProgress.Value = progress;
    }

    private async Task AutoDismissOperationPopupAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (!_isClosing && version == _operationPopupVersion) await HideOperationPopupAsync(version);
    }

    private async Task HideOperationPopupAsync(int version)
    {
        if (OperationPopup.Visibility != Visibility.Visible) return;
        AnimateOperationPopup(show: false);
        await Task.Delay(300);
        if (!_isClosing && version == _operationPopupVersion) OperationPopup.Visibility = Visibility.Collapsed;
    }

    private void AnimateOperationPopup(bool show)
    {
        var visual = ElementCompositionPreview.GetElementVisual(OperationPopup);
        var compositor = visual.Compositor;
        ElementCompositionPreview.SetIsTranslationEnabled(OperationPopup, true);
        visual.StopAnimation("Translation");
        visual.StopAnimation("Opacity");

        var quickOut = compositor.CreateCubicBezierEasingFunction(new Vector2(0.12f, 0.72f), new Vector2(0.22f, 1f));
        var settle = compositor.CreateCubicBezierEasingFunction(new Vector2(0.34f, 1.56f), new Vector2(0.64f, 1f));
        var translation = compositor.CreateVector3KeyFrameAnimation();
        translation.Duration = TimeSpan.FromMilliseconds(show ? 440 : 280);
        if (show)
        {
            translation.InsertKeyFrame(0, new Vector3(480, 0, 0));
            translation.InsertKeyFrame(0.68f, new Vector3(-18, 0, 0), quickOut);
            translation.InsertKeyFrame(0.86f, new Vector3(7, 0, 0), settle);
            translation.InsertKeyFrame(1, Vector3.Zero, settle);
        }
        else
        {
            translation.InsertKeyFrame(0, Vector3.Zero);
            translation.InsertKeyFrame(0.18f, new Vector3(-12, 0, 0), settle);
            translation.InsertKeyFrame(1, new Vector3(480, 0, 0), quickOut);
        }
        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.Duration = TimeSpan.FromMilliseconds(show ? 230 : 220);
        opacity.InsertKeyFrame(0, show ? 0 : 1);
        opacity.InsertKeyFrame(1, show ? 1 : 0, quickOut);
        visual.StartAnimation("Translation", translation);
        visual.StartAnimation("Opacity", opacity);
    }

    private async void OperationPopupClose_Click(object sender, RoutedEventArgs e)
    {
        var version = ++_operationPopupVersion;
        await HideOperationPopupAsync(version);
    }

    private static void CopySourceInformation(LibraryItem source, LibraryItem target)
    {
        target.Name = source.Name; target.ShopName = source.ShopName; target.OriginalCategory = source.OriginalCategory;
        target.ShopThumbnailUrl = source.ShopThumbnailUrl;
        target.Tags = source.Tags; target.Description = source.Description; target.VariationNames = source.VariationNames;
        target.DownloadedVariationNames = source.DownloadedVariationNames;
        target.HasBoothVariationRows = source.HasBoothVariationRows;
        target.HasPurchasedVariationOrder = source.HasPurchasedVariationOrder;
        target.ThumbnailUrl = source.ThumbnailUrl; target.FolderPath = source.FolderPath; target.BoothItemId = source.BoothItemId;
        target.RegisteredAt = source.RegisteredAt; target.UpdatedAt = source.UpdatedAt; target.PublishedAt = source.PublishedAt;
        target.HasFileUpdate = source.HasFileUpdate;
    }

    private void ApplyTitleCleanupIdentifiers()
    {
        var identifiers = _avatarProfiles.SelectMany(profile =>
                new[] { profile.PrimaryIdentifier }.Concat(profile.Identifiers))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var item in _allItems) item.SetTitleCleanupIdentifiers(identifiers);
    }

    private void RefreshCompatibilityCounts()
    {
        _compatibilityFilterCache.Clear();
        foreach (var item in _allItems)
        {
            item.PurchasedPackType = PurchasedPackClassifier.Classify(item, _avatarProfiles);
            if (!IsCompatibilityCategory(item.Category) || item.SupportsAllAvatars)
            {
                item.CompatibleAvatarCount = 0;
                continue;
            }
            var ids = GetEffectiveCompatibilityMatches(item).Select(x => x.AvatarRegistrationId).ToHashSet();
            _compatibilityFilterCache[item.RegistrationId] = ids;
            item.CompatibleAvatarCount = ids.Count;
        }
    }

    private static BitmapImage? CreateImageSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
            var uri = Uri.TryCreate(source, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(Path.GetFullPath(source));
            return new BitmapImage(uri);
        }
        catch { return null; }
    }
}
