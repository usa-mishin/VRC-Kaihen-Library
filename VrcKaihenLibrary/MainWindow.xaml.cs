using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
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
public sealed class ShopFilterOption : INotifyPropertyChanged
{
    public ShopFilterOption(string? shopName, string displayName, string? thumbnailUrl = null) { ShopName = shopName; DisplayName = displayName; ThumbnailUrl = thumbnailUrl; }
    public string? ShopName { get; }
    public string DisplayName { get; }
    public string? ThumbnailUrl { get; }
    private double _cardWidth = 260;
    public double CardWidth { get => _cardWidth; set { if (Math.Abs(_cardWidth - value) > 0.1) { _cardWidth = value; PropertyChanged?.Invoke(this, new(nameof(CardWidth))); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class BoothTagSummary : INotifyPropertyChanged
{
    public BoothTagSummary() { }
    public BoothTagSummary(string name, int count) { Name = name; Count = count; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public string CountText => $"{Count:N0}件";
    private double _cardWidth = 250;
    public double CardWidth { get => _cardWidth; set { if (Math.Abs(_cardWidth - value) > 0.1) { _cardWidth = value; PropertyChanged?.Invoke(this, new(nameof(CardWidth))); } } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class AvatarCardItem : INotifyPropertyChanged
{
    public AvatarCardItem() { }
    public AvatarCardItem(AvatarProfile profile, string displayName, string shopName, string? thumbnailUrl, LibraryItem? sourceItem)
    {
        Profile = profile; DisplayName = displayName; ShopName = shopName; ThumbnailUrl = thumbnailUrl; SourceItem = sourceItem;
    }
    public AvatarProfile Profile { get; set; } = new(string.Empty, null, string.Empty, string.Empty, [], null);
    public string DisplayName { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public LibraryItem? SourceItem { get; set; }
    public Visibility UnpurchasedBadgeVisibility => Profile.IsUnpurchased ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FileUpdateBadgeVisibility => SourceItem?.FileUpdateBadgeVisibility ?? Visibility.Collapsed;
    public DateTimeOffset? RegisteredAt => SourceItem?.RegisteredAt;
    public DateTimeOffset? UpdatedAt => SourceItem?.UpdatedAt;
    private double _cardWidth = 210;
    public double CardWidth { get => _cardWidth; set { if (Math.Abs(_cardWidth - value) > 0.1) { _cardWidth = value; PropertyChanged?.Invoke(this, new(nameof(CardWidth))); PropertyChanged?.Invoke(this, new(nameof(CardHeight))); } } }
    public double CardHeight => CardWidth + 86;
    public event PropertyChangedEventHandler? PropertyChanged;
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
public sealed class DownloadFileEntry : INotifyPropertyChanged
{
    private BitmapImage? _thumbnailImage;
    private BitmapImage? _hoverPreviewImage;
    private bool _thumbnailLoadCompleted;
    public string CategoryKey { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DirectoryText { get; set; } = string.Empty;
    public DateTime LastWriteTime { get; set; }
    public string LastWriteTimeText => LastWriteTime.ToString("yyyy/MM/dd HH:mm");
    public bool HasDuplicateName { get; set; }
    public bool IsOldVersion { get; set; }
    public bool IsDuplicateDownloadCopy { get; set; }
    public string? NewerVersionPath { get; set; }
    public IReadOnlyList<DownloadFileEntry> OlderVersions { get; set; } = [];
    public IReadOnlyList<string> AvatarBadgeNames { get; set; } = [];
    public int DirectoryDepth { get; set; }
    public Thickness TreeIndentMargin => new((DirectoryDepth + 1) * 14d, 0, 0, 0);
    public Brush? AccentBrush { get; set; }
    public Visibility DuplicateBadgeVisibility => HasDuplicateName ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OldVersionBadgeVisibility => IsOldVersion ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MaterialBadgeVisibility => CategoryKey == "UnityPackage"
        && FileName.Contains("material", StringComparison.OrdinalIgnoreCase)
        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BadgeRowVisibility => MaterialBadgeVisibility == Visibility.Visible || HasDuplicateName || IsOldVersion || AvatarBadgeNames.Count > 0
        ? Visibility.Visible : Visibility.Collapsed;
    public Brush RowBackground => IsOldVersion || IsDuplicateDownloadCopy
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 241, 243))
        : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    public BitmapImage? ThumbnailImage
    {
        get => _thumbnailImage;
        set
        {
            if (ReferenceEquals(_thumbnailImage, value)) return;
            _thumbnailImage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailImage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HoverPreviewImage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailVisibility)));
        }
    }
    public BitmapImage? HoverPreviewImage
    {
        get => _hoverPreviewImage ?? ThumbnailImage;
        set
        {
            if (ReferenceEquals(_hoverPreviewImage, value)) return;
            _hoverPreviewImage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HoverPreviewImage)));
        }
    }
    public bool HasLoadedHoverPreview { get; set; }
    public bool SupportsThumbnail => ImagePreviewService.SupportsPreview(FilePath);
    public Visibility PreviewColumnVisibility => SupportsThumbnail ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ThumbnailVisibility => ThumbnailImage is not null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ThumbnailLoadingVisibility => SupportsThumbnail && !_thumbnailLoadCompleted
        ? Visibility.Visible : Visibility.Collapsed;
    public bool IsThumbnailLoading => SupportsThumbnail && !_thumbnailLoadCompleted;
    public Visibility ThumbnailFallbackVisibility => SupportsThumbnail && _thumbnailLoadCompleted && ThumbnailImage is null
        ? Visibility.Visible : Visibility.Collapsed;
    public void CompleteThumbnailLoad(BitmapImage? image)
    {
        ThumbnailImage = image;
        _thumbnailLoadCompleted = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailLoadingVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsThumbnailLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailFallbackVisibility)));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
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
    public ObservableCollection<DownloadDirectoryGroup> VisibleDirectories { get; } = [];
    public IReadOnlyList<DownloadDirectoryGroup> DirectoryRoots { get; set; } = [];
    public bool UsesDirectoryTree => Key != "UnityPackage";
    public Visibility FileListVisibility => IsExpanded && !UsesDirectoryTree ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DirectoryListVisibility => IsExpanded && UsesDirectoryTree ? Visibility.Visible : Visibility.Collapsed;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileListVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DirectoryListVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChevronGlyph)));
        }
    }
    public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;
    public string ChevronGlyph => IsExpanded ? "\uE70E" : "\uE70D";
    public void RefreshVisibleDirectories()
    {
        VisibleDirectories.Clear();
        foreach (var root in DirectoryRoots) AddVisibleDirectory(root);
    }
    private void AddVisibleDirectory(DownloadDirectoryGroup directory)
    {
        VisibleDirectories.Add(directory);
        if (!directory.IsExpanded) return;
        foreach (var child in directory.Children) AddVisibleDirectory(child);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
public sealed class DownloadDirectoryGroup : INotifyPropertyChanged
{
    private bool _isExpanded;
    public string DisplayName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public Brush AccentBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    public IReadOnlyList<DownloadFileEntry> Files { get; set; } = [];
    public IReadOnlyList<DownloadDirectoryGroup> Children { get; set; } = [];
    public DownloadFileCategory? Owner { get; set; }
    public DownloadDirectoryGroup? Parent { get; set; }
    public int Depth { get; set; }
    public Thickness IndentMargin => new(Depth * 14d, 0, 0, 0);
    public string CountText => $"{Files.Count}個";
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
    private enum AppPage { Avatar, Library, Shop, BoothTags, ImportSettings, Settings, Help, Changelog, Privacy }
    private enum OperationPopupKind { Progress, Success, Information, Error }
    private sealed record UnityEditorTarget(int ProcessId, IntPtr WindowHandle);
    private sealed record ImportSettingEditor(string Category, TextBox FolderBox, CheckBox RootCheckBox);
    private const uint GwHwndNext = 2;
    private const string AllCategories = "すべて";
    // BOOTH-inspired magenta, matched to the current app icon.
    private static readonly Windows.UI.Color BoothAccentColor = Windows.UI.Color.FromArgb(255, 201, 79, 120);
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
    private readonly List<(Button Button, FontIcon Check, string Category)> _categorySelectorButtons = [];
    private readonly List<(Button Button, FontIcon Check, string Value, string Label)> _sortOptionButtons = [];
    private readonly List<TextBox> _avatarIdentifierBoxes = [];
    private readonly List<TextBox> _unpurchasedAvatarIdentifierBoxes = [];
    private AvatarProfile? _detailAvatarProfile;
    private AvatarProfile? _editingUnpurchasedAvatar;
    private int _currentPage = 1;
    private int _pageSize = 50;
    private int _filteredItemCount;
    private string _sortKey = "Registered";
    private bool _sortDescending = true;
    private List<AvatarSelectionOption> _compatibilityOptions = [];
    private List<AvatarSelectionOption> _sharedBodyOptions = [];
    private string? _selectedAvatarFilterId;
    private bool _excludeSharedBodyMatches;
    private string? _selectedShopFilter;
    private string? _selectedBoothTag;
    private string? _selectedPurchasedPackType;
    private bool _showFileUpdatesOnly;
    private int _avatarSortIndex = 4;
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
    private bool _isLibraryLoading;
    private bool _hasCompletedInitialLoad;
    private const double MinimumDetailPanelWidth = 300d;
    private const double MaximumDetailPanelWidth = 720d;
    private const double SmallDetailPanelWidth = 340d;
    private const double MediumDetailPanelWidth = 420d;
    private const double LargeDetailPanelWidth = 560d;
    private const string GitHubReleasesUrl = "https://github.com/usa-mishin/VRC-Kaihen-Library/releases";
    private const string GitHubLatestReleaseApiUrl = "https://api.github.com/repos/usa-mishin/VRC-Kaihen-Library/releases/latest";
    private double? _preferredDetailPanelWidth;
    private bool _isApplyingDetailPanelSizeSetting = true;
    private string _cardSizePreset = "Medium";
    private bool _isApplyingCardSizeSetting = true;
    private bool _isResizingDetailPanel;
    private double _detailResizeStartX;
    private double _detailResizeStartWidth;

    public ObservableCollection<LibraryItem> VisibleItems { get; } = [];
    public ObservableCollection<AvatarCardItem> VisibleAvatarCards { get; } = [];
    public ObservableCollection<AvatarFilterOption> AvatarFilterOptions { get; } = [];
    public ObservableCollection<AvatarFilterOption> VisibleAvatarFilterOptions { get; } = [];
    public ObservableCollection<AvatarSelectionOption> VisibleCompatibilityOptions { get; } = [];
    public ObservableCollection<AvatarSelectionOption> VisibleSharedBodyOptions { get; } = [];
    public ObservableCollection<ShopFilterOption> ShopFilterOptions { get; } = [];
    public ObservableCollection<ShopFilterOption> VisibleShopFilterOptions { get; } = [];
    public ObservableCollection<ShopFilterOption> VisibleShopCards { get; } = [];
    public ObservableCollection<BoothTagSummary> VisibleBoothTags { get; } = [];
    public ObservableCollection<BoothTagSummary> BoothTagFilterOptions { get; } = [];
    public ObservableCollection<BoothTagSummary> VisibleBoothTagFilterOptions { get; } = [];
    public ObservableCollection<UnityPackageEntry> UnityPackages { get; } = [];
    public ObservableCollection<DownloadFileCategory> DownloadFileCategories { get; } = [];
    public ObservableCollection<DownloadFileEntry> VisibleDownloadFiles { get; } = [];
    public ObservableCollection<string> DownloadedProductNames { get; } = [];
    public IReadOnlyList<string> DetailCategories => AssetCategories.All;

    public MainWindow()
    {
        InitializeComponent();
        AppVersionText.Text = $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "1.0.0.0"}";
        var savedDetailPanelWidth = _metadataStore.ReadDetailPanelWidth();
        if (savedDetailPanelWidth is >= MinimumDetailPanelWidth and <= MaximumDetailPanelWidth)
            _preferredDetailPanelWidth = savedDetailPanelWidth;
        DetailPanelSizeBox.SelectedIndex = GetDetailPanelSizeIndex(
            savedDetailPanelWidth ?? MediumDetailPanelWidth);
        _isApplyingDetailPanelSizeSetting = false;
        _cardSizePreset = _metadataStore.ReadCardSizePreset();
        CardSizeBox.SelectedIndex = _cardSizePreset switch { "Small" => 0, "Large" => 2, _ => 1 };
        _isApplyingCardSizeSetting = false;
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
        await WaitForRootLayoutLoadedAsync();
        if (!await EnsureDataAccessConsentAsync())
        {
            Close();
            return;
        }
        await LoadLibraryAsync();
    }

    private Task WaitForRootLayoutLoadedAsync()
    {
        if (RootLayout.IsLoaded && RootLayout.XamlRoot is not null) return Task.CompletedTask;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            RootLayout.Loaded -= loaded;
            completion.TrySetResult();
        };
        RootLayout.Loaded += loaded;
        return completion.Task;
    }

    private async Task<bool> EnsureDataAccessConsentAsync()
    {
        if (_metadataStore.HasCurrentDataAccessConsent()) return true;

        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\VrcKaihenLibrary"))
        {
            if (key?.GetValue("DataAccessConsentVersion") is int version
                && version >= UserMetadataStore.CurrentDataAccessConsentVersion)
            {
                _metadataStore.SaveCurrentDataAccessConsent();
                return true;
            }
        }

        var confirmation = new CheckBox
        {
            Content = new TextBlock
            {
                Text = "内容を確認し、PC内の商品情報を読み取り専用で参照することに同意します",
                TextWrapping = TextWrapping.Wrap
            },
            IsChecked = false
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = @"本アプリは、BOOTH Library Manager があなたのPC内に保存した商品情報（保存場所：%APPDATA%\pm.booth.library-manager\data.db）から、商品情報、購入・ダウンロード済みバリエーション、更新日時、商品保存先を読み取り専用で参照します。元の商品情報の変更・置換・削除は行いません。",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = "氏名、住所、メールアドレス、パスワード、Cookie、ブラウザ履歴は読み取りません。独自サーバーへの送信、広告、テレメトリー、クラッシュ自動送信もありません。サムネイルだけをBOOTH公式HTTPSドメインから取得する場合があります。",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });
        content.Children.Add(new TextBlock
        {
            Text = "本アプリはBOOTHおよびBOOTH Library Managerの非公式ツールであり、運営元による提供・保証・提携を受けていません。",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });
        content.Children.Add(confirmation);

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "ローカルデータの参照について",
            Content = content,
            PrimaryButtonText = "同意して開始",
            CloseButtonText = "同意しない（終了）",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false
        };
        confirmation.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmation.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
        _metadataStore.SaveCurrentDataAccessConsent();
        return true;
    }

    private async void RevokeDataAccessConsent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "同意を取り消しますか？",
            Content = "同意記録をこのPCから削除し、BLMの商品情報を画面上から消去してアプリを終了します。BLMのDB、商品ファイル、本アプリの分類設定は削除しません。次回起動時に同意画面を再表示します。",
            PrimaryButtonText = "取り消して終了",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        _metadataStore.ClearDataAccessConsent();
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\VrcKaihenLibrary", writable: true))
            key?.DeleteValue("DataAccessConsentVersion", throwOnMissingValue: false);
        _allItems = [];
        VisibleItems.Clear();
        VisibleAvatarCards.Clear();
        VisibleShopCards.Clear();
        VisibleBoothTags.Clear();
        Close();
    }

    private void RootLayout_Loaded(object sender, RoutedEventArgs e)
        => QueueCompactVerticalScrollBars(RootLayout);

    private void CompactScrollContainer_Loaded(object sender, RoutedEventArgs e)
        => QueueCompactVerticalScrollBars((DependencyObject)sender);

    private void QueueCompactVerticalScrollBars(DependencyObject root)
    {
        DispatcherQueue.TryEnqueue(() => ApplyCompactVerticalScrollBars(root));
    }

    private static void ApplyCompactVerticalScrollBars(DependencyObject root)
    {
        if (root is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar
            {
                Orientation: Orientation.Vertical
            } scrollBar)
        {
            SetVerticalScrollBarEndHeight(scrollBar, "VerticalSmallDecrease");
            SetVerticalScrollBarEndHeight(scrollBar, "VerticalSmallIncrease");
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            ApplyCompactVerticalScrollBars(VisualTreeHelper.GetChild(root, index));
    }

    private static void SetVerticalScrollBarEndHeight(
        Microsoft.UI.Xaml.Controls.Primitives.ScrollBar scrollBar,
        string elementName)
    {
        if (FindVisualDescendantByName(scrollBar, elementName)
            is Microsoft.UI.Xaml.Controls.Primitives.RepeatButton button)
        {
            button.MinHeight = 0;
            button.Height = 0;
        }
    }

    private static FrameworkElement? FindVisualDescendantByName(DependencyObject root, string name)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is FrameworkElement { Name: var childName } element && childName == name)
                return element;
            var match = FindVisualDescendantByName(child, name);
            if (match is not null) return match;
        }

        return null;
    }

    private async Task LoadLibraryAsync()
    {
        // Consent is checked again at the read boundary so that a future UI event
        // cannot accidentally access the BLM data before explicit user consent.
        if (!_metadataStore.HasCurrentDataAccessConsent()) return;
        if (_isLibraryLoading) return;
        _isLibraryLoading = true;
        var showInitialLoading = !_hasCompletedInitialLoad;
        if (showInitialLoading)
        {
            InitialLoadingTitle.Text = "BOOTH Library Managerを読み込み中";
            InitialLoadingDescription.Text = "商品データを準備しています。しばらくお待ちください。";
            InitialLoadingOverlay.Visibility = Visibility.Visible;
        }
        ShowOperationPopup(OperationPopupKind.Progress, "ライブラリを同期中", "BOOTH Library Manager のデータを読み込んでいます…");
        try
        {
            var snapshot = await ReadLibraryWithRetryAsync(showInitialLoading);
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
            PopulateAvatarCards();
            PopulateShopCards();
            PopulateBoothTags();
            PopulateBoothTagFilterOptions();
            PopulateCategories();
            ApplyFilter();
            DispatcherQueue.TryEnqueue(() => UpdateItemCardWidths(ItemsGrid.ActualWidth));
            if (await Task.Run(_metadataStore.CompleteFirstLaunchAndShouldShowHelp))
                SetActivePage(AppPage.Help);
            _isApplyingSmartTitleSetting = false;
            _hasCompletedInitialLoad = true;
            InitialLoadingOverlay.Visibility = Visibility.Collapsed;
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
            InitialLoadingOverlay.Visibility = Visibility.Collapsed;
            ShowOperationPopup(OperationPopupKind.Error, "ライブラリを読み込めませんでした", ex.Message);
            _isApplyingSmartTitleSetting = false;
        }
        finally
        {
            _isLibraryLoading = false;
        }
    }

    private async Task<BoothLibrarySnapshot> ReadLibraryWithRetryAsync(bool updateInitialLoading)
    {
        const int maximumAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await Task.Run(() => _reader.Read());
            }
            catch (Exception ex) when (attempt < maximumAttempts && IsTransientLibraryReadFailure(ex))
            {
                if (updateInitialLoading)
                {
                    InitialLoadingTitle.Text = "BOOTH Library Managerの準備を待っています";
                    InitialLoadingDescription.Text = $"BOOTH Library Manager が商品情報を準備・更新中の可能性があります。自動的に再試行します（{attempt}/{maximumAttempts}）";
                }
                OperationPopupMessage.Text = $"BOOTH Library Manager の準備を待っています（{attempt}/{maximumAttempts}）";
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    private static bool IsTransientLibraryReadFailure(Exception exception)
    {
        if (exception is FileNotFoundException or DirectoryNotFoundException)
            return true;

        if (exception is not SqliteException sqliteException)
            return false;

        if (sqliteException.SqliteErrorCode is 5 or 6 or 14)
            return true;

        return sqliteException.SqliteErrorCode == 1
            && sqliteException.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase);
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
        ShowOperationPopup(OperationPopupKind.Success, "表示設定を変更しました",
            enabled ? "商品名スマート短縮をオンにしました。" : "商品名スマート短縮をオフにしました。",
            autoDismiss: true);
    }

    private async void DetailPanelSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingDetailPanelSizeSetting || DetailPanelSizeBox.SelectedIndex < 0) return;
        if (DetailPanelSizeBox.SelectedIndex == 3)
        {
            return;
        }
        var width = DetailPanelSizeBox.SelectedIndex switch
        {
            0 => SmallDetailPanelWidth,
            2 => LargeDetailPanelWidth,
            _ => MediumDetailPanelWidth
        };
        _preferredDetailPanelWidth = width;
        if (DetailPanel.Visibility == Visibility.Visible)
            UpdateDetailPanelSize(RootLayout.ActualWidth);
        await Task.Run(() => _metadataStore.SaveDetailPanelWidth(width));
        ShowOperationPopup(OperationPopupKind.Success, "表示設定を変更しました",
            $"右詳細パネルのサイズを「{DetailPanelSizeBox.SelectedItem}」に変更しました。", autoDismiss: true);
    }

    private static int GetDetailPanelSizeIndex(double width)
    {
        const double tolerance = 0.5d;
        if (Math.Abs(width - SmallDetailPanelWidth) <= tolerance) return 0;
        if (Math.Abs(width - MediumDetailPanelWidth) <= tolerance) return 1;
        if (Math.Abs(width - LargeDetailPanelWidth) <= tolerance) return 2;
        return 3;
    }

    private async void CardSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingCardSizeSetting || CardSizeBox.SelectedIndex < 0) return;
        _cardSizePreset = CardSizeBox.SelectedIndex switch { 0 => "Small", 2 => "Large", _ => "Medium" };
        if (ItemsGrid.ActualWidth > 0) UpdateItemCardWidths(ItemsGrid.ActualWidth);
        if (AvatarGrid.ActualWidth > 0) UpdateOverviewCardWidths(AvatarGrid, AvatarGrid.ActualWidth);
        await Task.Run(() => _metadataStore.SaveCardSizePreset(_cardSizePreset));
        ShowOperationPopup(OperationPopupKind.Success, "表示設定を変更しました",
            $"アバター・アイテムカードのサイズを「{CardSizeBox.SelectedItem}」に変更しました。", autoDismiss: true);
    }

    private void PopulateCategories()
    {
        CategoryTabs.Children.Clear();
        CategorySelectorOptions.Children.Clear();
        _categoryTabButtons.Clear();
        _categorySelectorButtons.Clear();
        AddCategoryTab(AllCategories);
        foreach (var category in AssetCategories.All.Where(x => x != AssetCategories.Avatar))
            AddCategoryTab(category);
        DispatcherQueue.TryEnqueue(() => UpdateCategoryToolbarLayout(RootLayout.ActualWidth));
    }

    private void PopulateAvatarFilters()
    {
        AvatarFilterOptions.Clear();
        AvatarFilterOptions.Add(new AvatarFilterOption(null, "すべての対応アバター", "すべての対応アバター"));
        foreach (var profile in _avatarProfiles.OrderBy(x => x.PrimaryIdentifier, StringComparer.CurrentCultureIgnoreCase))
        {
            var avatarItem = _allItems.FirstOrDefault(x => x.RegistrationId == profile.RegistrationId);
            AvatarFilterOptions.Add(new AvatarFilterOption(
                profile.RegistrationId,
                avatarItem?.DisplayName ?? profile.PrimaryIdentifier,
                profile.PrimaryIdentifier,
                BoothNetworkPolicy.FilterImageSource(avatarItem?.ThumbnailUrl ?? profile.ThumbnailUrl)));
        }
        var selected = AvatarFilterOptions.FirstOrDefault(x => x.RegistrationId == _selectedAvatarFilterId) ?? AvatarFilterOptions[0];
        _selectedAvatarFilterId = selected.RegistrationId;
        AvatarFilterButtonText.Text = selected.PrimaryIdentifier;
        UpdateAvatarFilterDependentControls();
        ApplyAvatarFilterOptionSearch();
        PopulateAvatarCards();
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
        PopulateShopCards();
    }

    private void PopulateAvatarCards()
    {
        var filter = AvatarPurchaseFilterBox?.SelectedIndex ?? 0;
        VisibleAvatarCards.Clear();
        var cards = _avatarProfiles
            .Where(x => filter == 0 || (filter == 1 && !x.IsUnpurchased) || (filter == 2 && x.IsUnpurchased))
            .Select(profile =>
            {
                var item = _allItems.FirstOrDefault(x => x.RegistrationId == profile.RegistrationId);
                return new AvatarCardItem(profile, item?.DisplayName ?? profile.Name,
                    item?.ShopName ?? profile.ShopName ?? string.Empty,
                    BoothNetworkPolicy.FilterImageSource(item?.ThumbnailUrl ?? profile.ThumbnailUrl), item);
            });
        cards = _avatarSortIndex switch
        {
            0 => cards.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            1 => cards.OrderByDescending(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            2 => cards.OrderBy(x => x.ShopName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.DisplayName),
            3 => cards.OrderByDescending(x => x.ShopName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.DisplayName),
            5 => cards.OrderBy(x => x.RegisteredAt ?? DateTimeOffset.MaxValue),
            6 => cards.OrderByDescending(x => x.UpdatedAt ?? DateTimeOffset.MinValue),
            7 => cards.OrderBy(x => x.UpdatedAt ?? DateTimeOffset.MaxValue),
            _ => cards.OrderByDescending(x => x.RegisteredAt ?? DateTimeOffset.MinValue)
        };
        foreach (var card in cards) VisibleAvatarCards.Add(card);
        if (AvatarGrid?.ActualWidth > 0) UpdateOverviewCardWidths(AvatarGrid, AvatarGrid.ActualWidth);
    }

    private void PopulateShopCards()
    {
        var query = ShopPageSearchBox?.Text?.Trim() ?? string.Empty;
        VisibleShopCards.Clear();
        foreach (var shop in ShopFilterOptions.Skip(1).Where(x => string.IsNullOrEmpty(query) || Contains(x.DisplayName, query)))
            VisibleShopCards.Add(shop);
        if (ShopGrid?.ActualWidth > 0) UpdateOverviewCardWidths(ShopGrid, ShopGrid.ActualWidth);
    }

    private void PopulateBoothTags()
    {
        var query = BoothTagSearchBox?.Text?.Trim() ?? string.Empty;
        var counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var item in _allItems.Where(item => item.Category != AssetCategories.Avatar))
        {
            foreach (var tag in item.Tags.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Distinct(StringComparer.CurrentCultureIgnoreCase))
                counts[tag] = counts.GetValueOrDefault(tag) + 1;
        }

        VisibleBoothTags.Clear();
        foreach (var tag in counts.Where(pair => string.IsNullOrEmpty(query) || Contains(pair.Key, query))
                     .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.CurrentCultureIgnoreCase))
            VisibleBoothTags.Add(new BoothTagSummary(tag.Key, tag.Value));
        if (BoothTagGrid?.ActualWidth > 0) UpdateOverviewCardWidths(BoothTagGrid, BoothTagGrid.ActualWidth);
    }

    private void PopulateBoothTagFilterOptions()
    {
        BoothTagFilterOptions.Clear();
        BoothTagFilterOptions.Add(new BoothTagSummary("タグ指定なし", 0));
        var counts = _allItems.Where(item => item.Category != AssetCategories.Avatar)
            .SelectMany(item => item.Tags.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.CurrentCultureIgnoreCase))
            .GroupBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new BoothTagSummary(group.Key, group.Count()))
            .OrderByDescending(tag => tag.Count).ThenBy(tag => tag.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var tag in counts) BoothTagFilterOptions.Add(tag);
        ApplyBoothTagFilterSearch();
    }

    private void BoothTagFilterSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyBoothTagFilterSearch();

    private void ApplyBoothTagFilterSearch()
    {
        if (BoothTagFilterSearchBox is null) return;
        var query = BoothTagFilterSearchBox.Text.Trim();
        VisibleBoothTagFilterOptions.Clear();
        foreach (var option in BoothTagFilterOptions.Where(option => option.Count == 0 || string.IsNullOrEmpty(query) || Contains(option.Name, query)))
            VisibleBoothTagFilterOptions.Add(option);
    }

    private void BoothTagFilterList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BoothTagSummary option) return;
        _selectedBoothTag = option.Count == 0 ? null : option.Name;
        BoothTagFilterButtonText.Text = _selectedBoothTag ?? "タグ指定なし";
        BoothTagFilterFlyout.Hide();
        _currentPage = 1;
        ApplyFilter();
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
        var check = new FontIcon { Glyph = "\uE73E", FontSize = 13, Visibility = Visibility.Collapsed };
        var accent = new Border
        {
            Width = 9,
            Height = 9,
            CornerRadius = new CornerRadius(4.5),
            Background = category == AllCategories
                ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                : LibraryItem.GetCategoryBrush(category)
        };
        var label = new TextBlock { Text = category, VerticalAlignment = VerticalAlignment.Center };
        var content = new Grid { ColumnSpacing = 9 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(label, 1);
        Grid.SetColumn(check, 2);
        content.Children.Add(accent);
        content.Children.Add(label);
        content.Children.Add(check);
        var option = new VrcKaihenLibrary.Controls.HandCursorButton
        {
            Tag = category,
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        option.Click += CategorySelectorOption_Click;
        _categorySelectorButtons.Add((option, check, category));
        CategorySelectorOptions.Children.Add(option);
        UpdateCategoryTabAppearance(tab);
    }

    private void ApplyFilter()
    {
        if (ItemsGrid.ActualWidth > 0) UpdateItemCardWidths(ItemsGrid.ActualWidth);
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var filtered = _allItems.Where(item => item.Category != AssetCategories.Avatar &&
            (string.IsNullOrEmpty(query) || Contains(item.Name, query) || Contains(item.ShopName, query) || Contains(item.Tags, query)) &&
            (_selectedCategory == AllCategories || item.Category == _selectedCategory) &&
            (_selectedShopFilter is null || item.ShopName.Equals(_selectedShopFilter, StringComparison.CurrentCultureIgnoreCase)) &&
            (_selectedBoothTag is null || item.Tags.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(_selectedBoothTag, StringComparer.CurrentCultureIgnoreCase)) &&
            (_selectedPurchasedPackType is null || item.PurchasedPackType == _selectedPurchasedPackType) &&
            (!_showFileUpdatesOnly || item.HasFileUpdate) &&
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
        if (_excludeSharedBodyMatches)
        {
            return GetEffectiveCompatibilityMatches(item).Any(match =>
                match.AvatarRegistrationId == _selectedAvatarFilterId && !match.ThroughBaseBody);
        }
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
        UpdateAvatarFilterDependentControls();
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

    private void AvatarPurchaseFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_avatarProfiles.Count > 0) PopulateAvatarCards();
    }

    private void AvatarSortMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string tag } || !int.TryParse(tag, out var index)) return;
        _avatarSortIndex = index;
        AvatarSortButtonText.Text = ((MenuFlyoutItem)sender).Text;
        if (_avatarProfiles.Count > 0) PopulateAvatarCards();
    }

    private void ShopPageSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_allItems.Count > 0) PopulateShopCards();
    }

    private void BoothTagSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_allItems.Count > 0) PopulateBoothTags();
    }

    private void AvatarGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AvatarCardItem card) return;
        if (card.SourceItem is not null)
        {
            OpenItemDetail(card.SourceItem);
            return;
        }
        OpenUnpurchasedAvatarDetail(card.Profile);
    }

    private void ShopGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ShopFilterOption { ShopName: not null } shop) return;
        SearchBox.Text = string.Empty;
        _selectedCategory = AllCategories;
        _selectedAvatarFilterId = null;
        UpdateAvatarFilterDependentControls();
        _selectedPurchasedPackType = null;
        _showFileUpdatesOnly = false;
        FreeDownloadOnlySwitch.IsOn = false;
        FileUpdateOnlySwitch.IsOn = false;
        CompactFreeDownloadOnlySwitch.IsOn = false;
        CompactFileUpdateOnlySwitch.IsOn = false;
        CompactSearchBox.Text = string.Empty;
        CompactAvatarFilterBox.SelectedIndex = 0;
        CompactShopFilterBox.SelectedIndex = 0;
        CompactBoothTagFilterBox.SelectedIndex = 0;
        AvatarFilterButtonText.Text = "すべての対応アバター";
        _selectedShopFilter = shop.ShopName;
        ShopFilterButtonText.Text = shop.DisplayName;
        _currentPage = 1;
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        SetActivePage(AppPage.Library);
        ApplyFilter();
    }

    private async void AddUnpurchasedAvatar_Click(object sender, RoutedEventArgs e)
    {
        _editingUnpurchasedAvatar = null;
        UnpurchasedAvatarDialog.Title = "未購入アバターを追加";
        UnpurchasedAvatarNameBox.Text = string.Empty;
        UnpurchasedAvatarBoothUrlBox.Text = string.Empty;
        UnpurchasedAvatarBoothUrlBox.IsEnabled = true;
        UnpurchasedAvatarThumbnailBox.Text = string.Empty;
        UnpurchasedAvatarIdentifierBox.Text = string.Empty;
        UnpurchasedAvatarIdentifierRows.Children.Clear();
        _unpurchasedAvatarIdentifierBoxes.Clear();
        PrepareUnpurchasedSharedBodyOptions(null);
        UnpurchasedAvatarValidationText.Text = string.Empty;
        await UnpurchasedAvatarDialog.ShowAsync();
    }

    private void AddUnpurchasedAvatarIdentifier_Click(object sender, RoutedEventArgs e) => AddUnpurchasedAvatarIdentifierRow(string.Empty);

    private void AddUnpurchasedAvatarIdentifierRow(string value)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var textBox = new TextBox { Text = value, PlaceholderText = "識別子を入力" };
        var remove = new Controls.HandCursorButton { Content = new FontIcon { Glyph = "\uE738", FontSize = 14 }, Tag = row, Padding = new Thickness(10) };
        remove.Click += RemoveUnpurchasedAvatarIdentifier_Click;
        Grid.SetColumn(remove, 1);
        row.Children.Add(textBox); row.Children.Add(remove);
        UnpurchasedAvatarIdentifierRows.Children.Add(row);
        _unpurchasedAvatarIdentifierBoxes.Add(textBox);
        if (string.IsNullOrEmpty(value)) textBox.Focus(FocusState.Programmatic);
    }

    private void RemoveUnpurchasedAvatarIdentifier_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Grid row }) return;
        if (row.Children.OfType<TextBox>().FirstOrDefault() is { } box) _unpurchasedAvatarIdentifierBoxes.Remove(box);
        UnpurchasedAvatarIdentifierRows.Children.Remove(row);
    }

    private void PrepareUnpurchasedSharedBodyOptions(AvatarProfile? avatar)
    {
        var relatedIds = avatar is not null && _sharedBodyRelations.TryGetValue(avatar.RegistrationId, out var saved) ? saved : [];
        _sharedBodyOptions = _avatarProfiles.Where(x => x.RegistrationId != avatar?.RegistrationId)
            .Select(profile =>
            {
                var item = _allItems.FirstOrDefault(x => x.RegistrationId == profile.RegistrationId);
                return new AvatarSelectionOption(profile, relatedIds.Contains(profile.RegistrationId), item?.DisplayName ?? profile.Name, item?.ThumbnailUrl ?? profile.ThumbnailUrl);
            }).ToList();
        UnpurchasedSharedBodySearchBox.Text = string.Empty;
        ApplyUnpurchasedSharedBodyOptionSearch();
        UpdateUnpurchasedSharedBodySummary();
    }

    private void UnpurchasedSharedBodyOption_Changed(object sender, RoutedEventArgs e)
    {
        UpdateUnpurchasedSharedBodySummary();
    }

    private void UnpurchasedSharedBodySearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyUnpurchasedSharedBodyOptionSearch();

    private void ApplyUnpurchasedSharedBodyOptionSearch()
    {
        if (UnpurchasedSharedBodySearchBox is null) return;
        var query = UnpurchasedSharedBodySearchBox.Text.Trim();
        VisibleSharedBodyOptions.Clear();
        foreach (var option in _sharedBodyOptions.Where(x => string.IsNullOrWhiteSpace(query)
                     || Contains(x.DisplayName, query) || Contains(x.PrimaryIdentifier, query)))
            VisibleSharedBodyOptions.Add(option);
    }
    private void UpdateUnpurchasedSharedBodySummary()
    {
        var selected = _sharedBodyOptions.Where(x => x.IsSelected).Select(x => x.PrimaryIdentifier).ToList();
        UnpurchasedSharedBodySelectionSummary.Text = selected.Count switch { 0 => "選択なし", <= 2 => string.Join("、", selected), _ => $"{string.Join("、", selected.Take(2))} ほか{selected.Count - 2}件" };
    }

    private async void UnpurchasedAvatarDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var name = UnpurchasedAvatarNameBox.Text.Trim();
        var boothUrl = UnpurchasedAvatarBoothUrlBox.Text.Trim();
        var primary = UnpurchasedAvatarIdentifierBox.Text.Trim();
        if (name.Length == 0 || primary.Length == 0 || !TryGetBoothItemId(boothUrl, out var boothItemId))
        {
            args.Cancel = true;
            UnpurchasedAvatarValidationText.Text = "アバター名、識別名、有効なBOOTH商品URLを入力してください。";
            return;
        }
        var identifiers = _unpurchasedAvatarIdentifierBoxes.Select(x => x.Text.Trim()).Where(x => x.Length > 0)
            .Where(x => !x.Equals(primary, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var profile = new AvatarProfile($"manual-avatar:{boothItemId}", boothItemId, name, primary, identifiers, null,
            true, $"https://booth.pm/ja/items/{boothItemId}", null,
            NullIfWhiteSpace(UnpurchasedAvatarThumbnailBox.Text));
        var deferral = args.GetDeferral();
        try
        {
            await Task.Run(() => _metadataStore.SaveUnpurchasedAvatar(profile));
            var relatedIds = _sharedBodyOptions.Where(x => x.IsSelected).Select(x => x.Profile.RegistrationId).ToList();
            await Task.Run(() => _metadataStore.SaveSharedBodyRelations(profile.RegistrationId, relatedIds));
            _avatarProfiles = await Task.Run(_metadataStore.ReadAvatarProfiles);
            _sharedBodyRelations = await Task.Run(_metadataStore.ReadSharedBodyRelations);
            _compatibilityFilterCache.Clear();
            ApplyTitleCleanupIdentifiers();
            PopulateAvatarFilters();
            PopulateAvatarCards();
            if (_editingUnpurchasedAvatar is not null)
            {
                _detailAvatarProfile = _avatarProfiles.FirstOrDefault(x => x.RegistrationId == profile.RegistrationId);
                if (_detailAvatarProfile is not null) OpenUnpurchasedAvatarDetail(_detailAvatarProfile);
            }
            ShowOperationPopup(OperationPopupKind.Success, _editingUnpurchasedAvatar is null ? "未購入アバターを追加しました" : "未購入アバターを更新しました", name, autoDismiss: true);
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            UnpurchasedAvatarValidationText.Text = ex.Message;
        }
        finally { deferral.Complete(); }
    }

    private static bool TryGetBoothItemId(string value, out long boothItemId)
    {
        boothItemId = 0;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !(uri.Host.Equals("booth.pm", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".booth.pm", StringComparison.OrdinalIgnoreCase)))
            return false;

        // booth.pm/ja/items/123, booth.pm/items/123 and
        // shop-name.booth.pm/items/123 are all official BOOTH item URLs.
        var match = Regex.Match(uri.AbsolutePath,
            @"^/(?:[a-z]{2}/)?items/(?<id>[0-9]+)(?:/.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            && long.TryParse(match.Groups["id"].Value, out boothItemId)
            && boothItemId > 0;
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadLibraryAsync();
    private void FreeDownloadOnlySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _selectedPurchasedPackType = FreeDownloadOnlySwitch.IsOn ? PurchasedPackClassifier.FreeDownload : null;
        if (CompactFreeDownloadOnlySwitch.IsOn != FreeDownloadOnlySwitch.IsOn)
            CompactFreeDownloadOnlySwitch.IsOn = FreeDownloadOnlySwitch.IsOn;
        _currentPage = 1;
        if (_allItems.Count > 0) ApplyFilter();
    }

    private void FileUpdateOnlySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _showFileUpdatesOnly = FileUpdateOnlySwitch.IsOn;
        if (CompactFileUpdateOnlySwitch.IsOn != FileUpdateOnlySwitch.IsOn)
            CompactFileUpdateOnlySwitch.IsOn = FileUpdateOnlySwitch.IsOn;
        _currentPage = 1;
        if (_allItems.Count > 0) ApplyFilter();
    }

    private void ExcludeSharedBodySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _excludeSharedBodyMatches = ExcludeSharedBodySwitch.IsOn;
        if (CompactExcludeSharedBodySwitch.IsOn != ExcludeSharedBodySwitch.IsOn)
            CompactExcludeSharedBodySwitch.IsOn = ExcludeSharedBodySwitch.IsOn;
        _currentPage = 1;
        if (_allItems.Count > 0) ApplyFilter();
    }

    private void UpdateAvatarFilterDependentControls()
    {
        var enabled = _selectedAvatarFilterId is not null;
        ExcludeSharedBodySwitch.IsEnabled = enabled;
        CompactExcludeSharedBodySwitch.IsEnabled = enabled;
        if (enabled) return;

        _excludeSharedBodyMatches = false;
        ExcludeSharedBodySwitch.IsOn = false;
        CompactExcludeSharedBodySwitch.IsOn = false;
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        _selectedCategory = AllCategories;
        _selectedAvatarFilterId = null;
        UpdateAvatarFilterDependentControls();
        _selectedShopFilter = null;
        _selectedBoothTag = null;
        _selectedPurchasedPackType = null;
        _showFileUpdatesOnly = false;
        AvatarFilterButtonText.Text = "すべての対応アバター";
        ShopFilterButtonText.Text = "すべてのショップ";
        BoothTagFilterButtonText.Text = "タグ指定なし";
        AvatarFilterSearchBox.Text = string.Empty;
        ShopFilterSearchBox.Text = string.Empty;
        BoothTagFilterSearchBox.Text = string.Empty;
        FreeDownloadOnlySwitch.IsOn = false;
        FileUpdateOnlySwitch.IsOn = false;
        CompactFreeDownloadOnlySwitch.IsOn = false;
        CompactFileUpdateOnlySwitch.IsOn = false;
        CompactSearchBox.Text = string.Empty;
        CompactAvatarFilterBox.SelectedIndex = 0;
        CompactShopFilterBox.SelectedIndex = 0;
        CompactBoothTagFilterBox.SelectedIndex = 0;
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

    private void CategorySelectorOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string category }) return;
        _selectedCategory = category;
        _currentPage = 1;
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        CategorySelectorFlyout.Hide();
        ApplyFilter();
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
        CategorySelectionLine.Background = selectedBrush;
        tab.Background = selected ? selectedBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        tab.BorderBrush = selected ? selectedBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        tab.Foreground = selected
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        tab.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        tab.BorderThickness = selected ? new Thickness(0, 0, 0, 3) : new Thickness(0, 0, 0, 2);
        if (!selected) return;
        CategorySelectorText.Text = _selectedCategory;
        CategorySelectorButton.Background = selectedBrush;
        CategorySelectorButton.BorderBrush = selectedBrush;
        CategorySelectorButton.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        CategorySelectorButton.BorderThickness = new Thickness(0, 0, 0, 3);
        foreach (var option in _categorySelectorButtons)
        {
            var optionSelected = option.Category.Equals(_selectedCategory, StringComparison.Ordinal);
            option.Check.Visibility = optionSelected ? Visibility.Visible : Visibility.Collapsed;
            option.Button.FontWeight = optionSelected
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;
        }
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
        OpenItemDetail(item);
    }

    private void OpenItemDetail(LibraryItem item)
    {
        try
        {
            _detailAvatarProfile = null;
            _detailItem = item;
            UpdateDetailPanel();
            var wasOpen = DetailPanel.Visibility == Visibility.Visible;
            DetailPanel.Visibility = Visibility.Visible;
            UpdateDetailPanelSize(RootLayout.ActualWidth);
            if (!wasOpen) OpenDetailPanelStoryboard.Begin();
            _ = LoadDownloadFilesAsync(item);
        }
        catch (Exception ex)
        {
            ShowOperationPopup(OperationPopupKind.Error, "詳細を表示できませんでした", ex.Message);
            Debug.WriteLine($"Detail panel error: {ex.GetType().Name}");
        }
    }

    private void OpenUnpurchasedAvatarDetail(AvatarProfile profile)
    {
        _detailAvatarProfile = profile;
        _detailItem = new LibraryItem
        {
            RegistrationId = profile.RegistrationId,
            BoothItemId = profile.BoothItemId,
            Name = profile.Name,
            Category = AssetCategories.Avatar,
            ThumbnailUrl = BoothNetworkPolicy.FilterImageSource(profile.ThumbnailUrl),
            ImportToAssetsRoot = true
        };
        UpdateDetailPanel();
        var wasOpen = DetailPanel.Visibility == Visibility.Visible;
        DetailPanel.Visibility = Visibility.Visible;
        UpdateDetailPanelSize(RootLayout.ActualWidth);
        if (!wasOpen) OpenDetailPanelStoryboard.Begin();
    }

    private static readonly (string Key, string DisplayName, string Glyph, Windows.UI.Color Color)[] DownloadCategoryDefinitions =
    [
        ("UnityPackage", "Unityパッケージ", "\uE7B8", Windows.UI.Color.FromArgb(255, 91, 105, 166)),
        ("Texture", "テクスチャ", "\uEB9F", Windows.UI.Color.FromArgb(255, 67, 145, 181)),
        ("ImageSource", "画像編集データ", "\uE790", Windows.UI.Color.FromArgb(255, 173, 92, 154)),
        ("ThreeD", "3Dデータ", "\uF158", Windows.UI.Color.FromArgb(255, 83, 151, 112)),
        ("Document", "ドキュメント", "\uE8A5", Windows.UI.Color.FromArgb(255, 184, 127, 60)),
        ("Other", "その他", "\uE8B7", Windows.UI.Color.FromArgb(255, 112, 118, 128))
    ];

    private async Task LoadDownloadFilesAsync(LibraryItem item)
    {
        var loadVersion = ++_unityPackageLoadVersion;
        DownloadFileSearchBox.Text = string.Empty;
        _downloadFiles = [];
        DownloadFileCategories.Clear();
        DownloadFileTotalCountText.Text = "検索中";

        var files = await Task.Run(() => FindDownloadFiles(
            item.FolderPath, _avatarProfiles, item.Category != AssetCategories.Avatar));
        if (_isClosing || loadVersion != _unityPackageLoadVersion || _detailItem?.RegistrationId != item.RegistrationId) return;

        _downloadFiles = files;
        ApplyDownloadFileFilter();
        _ = LoadDownloadFileThumbnailsAsync(files, item.RegistrationId);
    }

    private async Task LoadDownloadFileThumbnailsAsync(
        IReadOnlyList<DownloadFileEntry> files, string registrationId)
    {
        foreach (var file in files.Where(x => x.SupportsThumbnail))
        {
            if (_isClosing || _detailItem?.RegistrationId != registrationId) return;
            var thumbnail = await ImagePreviewService.LoadAsync(file.FilePath, 108);
            if (_isClosing || _detailItem?.RegistrationId != registrationId) return;
            file.CompleteThumbnailLoad(thumbnail);
        }
    }

    private void DownloadFileSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyDownloadFileFilter();

    private void ApplyDownloadFileFilter()
    {
        if (DownloadFileSearchBox is null || _detailItem is null) return;
        var query = DownloadFileSearchBox.Text.Trim();
        var files = _downloadFiles.Where(file => string.IsNullOrWhiteSpace(query)
            || Contains(file.FileName, query)
            || Contains(file.DirectoryText, query)).ToList();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var includedPaths = files.Select(x => x.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var oldVersion in files.Where(x => x.NewerVersionPath is not null).ToList())
            {
                var parent = _downloadFiles.FirstOrDefault(x => x.FilePath.Equals(
                    oldVersion.NewerVersionPath, StringComparison.OrdinalIgnoreCase));
                if (parent is not null && includedPaths.Add(parent.FilePath)) files.Add(parent);
            }
        }
        DownloadFileCategories.Clear();
        foreach (var definition in DownloadCategoryDefinitions)
        {
            var categoryFiles = files.Where(x => x.CategoryKey == definition.Key).ToList();
            if (!string.IsNullOrWhiteSpace(query) && categoryFiles.Count == 0) continue;
            var accentBrush = new SolidColorBrush(definition.Color);
            foreach (var file in categoryFiles) file.AccentBrush = accentBrush;
            var category = new DownloadFileCategory
            {
                Key = definition.Key,
                DisplayName = definition.DisplayName,
                Glyph = definition.Glyph,
                AccentBrush = accentBrush,
                Count = categoryFiles.Count,
                Subtitle = definition.Key == "UnityPackage"
                    ? ImportsToAssetsRoot(_detailItem)
                        ? "Unity配置先: Assets直下"
                        : $"Unity配置先: Assets/{GetImportFolderName(_detailItem.Category)}"
                    : string.Empty,
                Files = definition.Key == "UnityPackage"
                    ? categoryFiles.Where(x => x.NewerVersionPath is null
                        || !categoryFiles.Any(parent => parent.FilePath.Equals(
                            x.NewerVersionPath, StringComparison.OrdinalIgnoreCase))).ToList()
                    : categoryFiles
            };
            if (definition.Key != "UnityPackage")
            {
                category.DirectoryRoots = BuildDownloadDirectoryTree(categoryFiles, accentBrush, category);
                category.RefreshVisibleDirectories();
            }
            DownloadFileCategories.Add(category);
        }
        DownloadFileTotalCountText.Text = string.IsNullOrWhiteSpace(query)
            ? $"{files.Count}個"
            : $"{files.Count} / {_downloadFiles.Count}個";
    }

    private static IReadOnlyList<DownloadDirectoryGroup> BuildDownloadDirectoryTree(
        IReadOnlyList<DownloadFileEntry> files, Brush accentBrush, DownloadFileCategory owner)
    {
        const string rootLabel = "保存フォルダー直下";
        var nodes = files
            .GroupBy(x => x.DirectoryText, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => new DownloadDirectoryGroup
            {
                DisplayName = group.Key == rootLabel ? rootLabel : Path.GetFileName(group.Key),
                RelativePath = group.Key,
                FolderPath = Path.GetDirectoryName(group.First().FilePath) ?? string.Empty,
                AccentBrush = accentBrush,
                Files = group.OrderBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase).ToList(),
                Owner = owner
            }, StringComparer.CurrentCultureIgnoreCase);

        var children = nodes.Keys.ToDictionary(x => x, _ => new List<DownloadDirectoryGroup>(),
            StringComparer.CurrentCultureIgnoreCase);
        var roots = new List<DownloadDirectoryGroup>();
        foreach (var (path, node) in nodes.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var parentPath = path == rootLabel ? null : FindNearestDisplayedParent(path, nodes.Keys);
            if (parentPath is not null) children[parentPath].Add(node);
            else roots.Add(node);
        }

        void CompleteNode(DownloadDirectoryGroup node, int depth)
        {
            node.Depth = depth;
            foreach (var file in node.Files) file.DirectoryDepth = depth;
            node.Children = children[node.RelativePath]
                .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (var child in node.Children)
            {
                child.Parent = node;
                CompleteNode(child, depth + 1);
            }
        }
        foreach (var root in roots) CompleteNode(root, 0);
        var duplicateDirectoryNames = nodes.Values
            .GroupBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        foreach (var node in nodes.Values.Where(x => duplicateDirectoryNames.Contains(x.DisplayName)))
        {
            var parentName = Path.GetFileName(Path.GetDirectoryName(node.FolderPath));
            if (!string.IsNullOrWhiteSpace(parentName)) node.DisplayName = $"{parentName} > {node.DisplayName}";
        }
        return roots.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static string? FindNearestDisplayedParent(string path, IEnumerable<string> candidates)
    {
        var parent = Path.GetDirectoryName(path);
        while (!string.IsNullOrWhiteSpace(parent) && parent != ".")
        {
            var match = candidates.FirstOrDefault(x => x.Equals(parent, StringComparison.CurrentCultureIgnoreCase));
            if (match is not null) return match;
            parent = Path.GetDirectoryName(parent);
        }
        return null;
    }

    private static IReadOnlyList<DownloadFileEntry> FindDownloadFiles(
        string rootPath, IReadOnlyList<AvatarProfile> avatarProfiles, bool includeAvatarBadges)
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
        var versionParents = DownloadUpdateCandidateService.FindUnityPackageVersionParents(rootPath,
            paths.Where(path => Path.GetExtension(path).Equals(".unitypackage", StringComparison.OrdinalIgnoreCase)));
        var entries = paths.Select(path =>
            {
                var relativeDirectory = Path.GetRelativePath(rootPath, Path.GetDirectoryName(path) ?? rootPath);
                var hasParent = versionParents.TryGetValue(path, out var newerPath);
                var isDuplicateDownloadCopy = hasParent
                    && DownloadUpdateCandidateService.IsDuplicateDownloadFolderPair(rootPath, path, newerPath!);
                return new DownloadFileEntry
                {
                    CategoryKey = ClassifyDownloadFile(path),
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    DirectoryText = relativeDirectory == "." ? "保存フォルダー直下" : relativeDirectory,
                    LastWriteTime = File.GetLastWriteTime(path),
                    HasDuplicateName = Path.GetExtension(path).Equals(
                        ".unitypackage", StringComparison.OrdinalIgnoreCase)
                        && duplicateNames.Contains(Path.GetFileName(path)),
                    IsOldVersion = hasParent && !isDuplicateDownloadCopy,
                    IsDuplicateDownloadCopy = isDuplicateDownloadCopy,
                    NewerVersionPath = hasParent ? newerPath : null,
                    AvatarBadgeNames = includeAvatarBadges
                        && Path.GetExtension(path).Equals(".unitypackage", StringComparison.OrdinalIgnoreCase)
                        ? AvatarCompatibilityService.DetectAvatarNamesFromFileName(Path.GetFileName(path), avatarProfiles)
                        : []
                };
            })
            .OrderBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(x => x.LastWriteTime)
            .ToList();
        var entriesByPath = entries.ToDictionary(x => x.FilePath, StringComparer.OrdinalIgnoreCase);
        foreach (var parentGroup in entries.Where(x => x.NewerVersionPath is not null)
            .GroupBy(x => x.NewerVersionPath!, StringComparer.OrdinalIgnoreCase))
        {
            if (entriesByPath.TryGetValue(parentGroup.Key, out var parent))
            {
                parent.OlderVersions = parentGroup
                    .OrderByDescending(x => x.LastWriteTime)
                    .ThenBy(x => x.FileName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                var duplicateDownloadGroup = parent.OlderVersions.Any(older =>
                    DownloadUpdateCandidateService.IsDuplicateDownloadFolderPair(
                        rootPath, parent.FilePath, older.FilePath));
                if (!duplicateDownloadGroup)
                {
                    foreach (var sameNameEntry in entries.Where(x => x.FileName.Equals(
                        parent.FileName, StringComparison.OrdinalIgnoreCase)))
                        sameNameEntry.HasDuplicateName = false;
                }
            }
        }
        return entries;
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

    private void DownloadDirectoryGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing || sender is not FrameworkElement { Tag: DownloadDirectoryGroup directory }) return;
        var expand = !directory.IsExpanded;
        if (expand && directory.Owner is not null)
        {
            var siblings = directory.Parent?.Children ?? directory.Owner.DirectoryRoots;
            foreach (var sibling in siblings)
                if (!ReferenceEquals(sibling, directory)) sibling.IsExpanded = false;
        }
        directory.IsExpanded = expand;
        directory.Owner?.RefreshVisibleDirectories();
    }

    private void DownloadDirectoryOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing || sender is not FrameworkElement { Tag: DownloadDirectoryGroup directory }
            || string.IsNullOrWhiteSpace(directory.FolderPath) || !Directory.Exists(directory.FolderPath)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{directory.FolderPath}\"",
            UseShellExecute = true
        });
    }

    private async void DownloadImageThumbnail_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_isClosing || sender is not FrameworkElement { Tag: DownloadFileEntry file } element
            || !file.SupportsThumbnail || file.ThumbnailImage is null) return;
        Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(element);
        if (file.HasLoadedHoverPreview) return;
        file.HasLoadedHoverPreview = true;
        var preview = await ImagePreviewService.LoadAsync(file.FilePath, 720);
        if (!_isClosing && preview is not null) file.HoverPreviewImage = preview;
        else file.HasLoadedHoverPreview = false;
    }

    private void DownloadImageThumbnail_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
            Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.GetAttachedFlyout(element)?.Hide();
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
        var isUnpurchased = _detailAvatarProfile?.IsUnpurchased == true;
        DetailPanelTitle.Text = "アイテム詳細";
        DetailEditButtonText.Text = "アイテム情報編集";
        DetailReloadButton.Visibility = isUnpurchased ? Visibility.Collapsed : Visibility.Visible;
        ResetCompatibilityMenuItem.Visibility = isUnpurchased ? Visibility.Collapsed : Visibility.Visible;
        CheckDuplicatesMenuItem.Visibility = isUnpurchased ? Visibility.Collapsed : Visibility.Visible;
        DeleteUnpurchasedAvatarMenuItem.Visibility = isUnpurchased ? Visibility.Visible : Visibility.Collapsed;
        DetailShopButton.Visibility = isUnpurchased ? Visibility.Collapsed : Visibility.Visible;
        DetailOpenBoothButton.Visibility = isUnpurchased ? Visibility.Collapsed : Visibility.Visible;
        DetailDownloadsSection.Visibility = isUnpurchased ? Visibility.Collapsed : Visibility.Visible;
        DetailName.Text = item.DisplayName;
        DetailShop.Text = item.ShopName;
        DetailShopThumbnail.Source = CreateImageSource(item.ShopThumbnailUrl);
        DetailCategoryText.Text = item.Category;
        DetailCategoryBadge.Background = item.CategoryBadgeBrush;
        var boothTags = item.Tags.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Select(tag => new DetailTagChip(tag, null, false))
            .ToList();
        DetailBoothTags.ItemsSource = boothTags;
        DetailBoothTagsTitle.Text = $"BOOTHタグ（{boothTags.Count}）";
        DetailBoothTagsSection.IsExpanded = false;
        DetailBoothTagsSection.Visibility = boothTags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DetailUpdateNotice.Visibility = !isUnpurchased && item.HasFileUpdate
            ? Visibility.Visible
            : Visibility.Collapsed;
        DetailPlacement.Text = ImportsToAssetsRoot(item)
            ? "Unity配置先: Assets直下"
            : $"Unity配置先: Assets/{GetImportFolderName(item.Category)}";
        var matches = GetEffectiveCompatibilityMatches(item);
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
            : BuildCompatibilityChips(matches);
        DetailCompatibility.ItemsSource = compatibilityChips;
        var evidenceText = item.SupportsAllAvatars
            ? "全アバター対応として設定されています。"
            : string.Join("\n", matches.Select(x => $"• {GetAvatarPrimaryIdentifier(x.AvatarRegistrationId)}（{x.Evidence}）"));
        DetailCompatibilityEvidenceIcon.Visibility = hasCompatibility && (item.SupportsAllAvatars || matches.Count > 0)
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

    private IReadOnlyList<DetailTagChip> BuildCompatibilityChips(IReadOnlyList<CompatibilityMatch> matches)
    {
        var result = new List<DetailTagChip>();
        var directMatches = matches.Where(match => !match.ThroughBaseBody).ToList();
        foreach (var direct in directMatches)
        {
            var sharedNames = matches
                .Where(match => match.ThroughBaseBody
                    && match.Evidence.Equals($"共通素体: {direct.AvatarName}", StringComparison.Ordinal))
                .Select(match => GetAvatarPrimaryIdentifier(match.AvatarRegistrationId))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var directName = GetAvatarPrimaryIdentifier(direct.AvatarRegistrationId);
            var text = sharedNames.Count == 0 ? directName : $"{directName}（{string.Join("・", sharedNames)}）";
            result.Add(new DetailTagChip(text, direct.AvatarRegistrationId, true, result.Count == 0 ? "" : "/"));
        }

        foreach (var shared in matches.Where(match => match.ThroughBaseBody
                     && !directMatches.Any(direct => match.Evidence.Equals($"共通素体: {direct.AvatarName}", StringComparison.Ordinal))))
        {
            result.Add(new DetailTagChip(GetAvatarPrimaryIdentifier(shared.AvatarRegistrationId),
                shared.AvatarRegistrationId, true, result.Count == 0 ? "" : "/"));
        }
        return result;
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
        if (_detailAvatarProfile?.IsUnpurchased == true)
        {
            await ShowUnpurchasedAvatarEditorAsync(_detailAvatarProfile);
            return;
        }
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
                    return new AvatarSelectionOption(profile, relatedIds.Contains(profile.RegistrationId), avatarItem?.DisplayName ?? profile.Name, avatarItem?.ThumbnailUrl ?? profile.ThumbnailUrl);
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
                return new AvatarSelectionOption(profile, effectiveIds.Contains(profile.RegistrationId), avatarItem?.DisplayName ?? profile.Name, avatarItem?.ThumbnailUrl ?? profile.ThumbnailUrl);
            })
            .ToList();
        CompatibilitySearchBox.Text = string.Empty;
        ApplyCompatibilityOptionSearch();
        AllAvatarsCheckBox.IsChecked = _detailItem.SupportsAllAvatars;
        UpdateCompatibilitySelectionSummary();
        await DetailDialog.ShowAsync();
    }

    private async Task ShowUnpurchasedAvatarEditorAsync(AvatarProfile profile)
    {
        _editingUnpurchasedAvatar = profile;
        UnpurchasedAvatarDialog.Title = "未購入アバターを編集";
        UnpurchasedAvatarNameBox.Text = profile.Name;
        UnpurchasedAvatarBoothUrlBox.Text = profile.BoothUrl ?? string.Empty;
        UnpurchasedAvatarBoothUrlBox.IsEnabled = false;
        UnpurchasedAvatarThumbnailBox.Text = profile.ThumbnailUrl ?? string.Empty;
        UnpurchasedAvatarIdentifierBox.Text = profile.PrimaryIdentifier;
        UnpurchasedAvatarIdentifierRows.Children.Clear();
        _unpurchasedAvatarIdentifierBoxes.Clear();
        foreach (var identifier in profile.Identifiers) AddUnpurchasedAvatarIdentifierRow(identifier);
        PrepareUnpurchasedSharedBodyOptions(profile);
        UnpurchasedAvatarValidationText.Text = string.Empty;
        await UnpurchasedAvatarDialog.ShowAsync();
    }

    private async void DeleteUnpurchasedAvatar_Click(object sender, RoutedEventArgs e)
    {
        if (_detailAvatarProfile?.IsUnpurchased != true) return;
        DetailOperationsFlyout.Hide();
        var profile = _detailAvatarProfile;
        var dialog = new ContentDialog { XamlRoot = RootLayout.XamlRoot, Title = "未購入アバターの登録を削除しますか？", Content = $"{profile.Name}\n\n識別子、共通素体、対応設定も削除されます。", PrimaryButtonText = "登録削除", CloseButtonText = "キャンセル", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await Task.Run(() => _metadataStore.DeleteUnpurchasedAvatar(profile.RegistrationId));
        _avatarProfiles = await Task.Run(_metadataStore.ReadAvatarProfiles);
        _sharedBodyRelations = await Task.Run(_metadataStore.ReadSharedBodyRelations);
        _compatibilityOverrides = await Task.Run(_metadataStore.ReadAllCompatibilityOverrides);
        _compatibilityFilterCache.Clear();
        PopulateAvatarFilters();
        await CloseDetailPanelAsync();
        _detailAvatarProfile = null;
        _detailItem = null;
        ShowOperationPopup(OperationPopupKind.Success, "未購入アバターを削除しました", profile.Name, autoDismiss: true);
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
        => UpdateItemCardWidths(e.NewSize.Width);

    private void OverviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is GridView grid) UpdateOverviewCardWidths(grid, e.NewSize.Width);
    }

    private void UpdateOverviewCardWidths(GridView grid, double gridWidth)
    {
        const double cardOuterSpacing = 14;
        var minimumCardWidth = grid == AvatarGrid ? GetCardMinimumWidth() : grid == ShopGrid ? 240d : 220d;
        var usableWidth = Math.Max(minimumCardWidth + cardOuterSpacing, gridWidth - 2);
        var columns = Math.Max(1, (int)Math.Floor(usableWidth / (minimumCardWidth + cardOuterSpacing)));
        var cardWidth = Math.Max(minimumCardWidth, Math.Floor(usableWidth / columns) - cardOuterSpacing);

        if (grid == AvatarGrid)
            foreach (var card in VisibleAvatarCards) card.CardWidth = cardWidth;
        else if (grid == ShopGrid)
            foreach (var card in VisibleShopCards) card.CardWidth = cardWidth;
        else if (grid == BoothTagGrid)
            foreach (var card in VisibleBoothTags) card.CardWidth = cardWidth;
    }

    private void UpdateItemCardWidths(double gridWidth)
    {
        var minimumCardWidth = GetCardMinimumWidth();
        const double cardOuterSpacing = 8;
        var usableWidth = Math.Max(minimumCardWidth + cardOuterSpacing, gridWidth - 2);
        var columns = Math.Max(1, (int)Math.Floor(usableWidth / (minimumCardWidth + cardOuterSpacing)));
        var cardWidth = Math.Floor(usableWidth / columns) - cardOuterSpacing;
        foreach (var item in _allItems) item.CardWidth = Math.Max(minimumCardWidth, cardWidth);
    }

    private double GetCardMinimumWidth() => _cardSizePreset switch
    {
        "Small" => 150d,
        "Large" => 230d,
        _ => 180d
    };

    private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLibraryHeaderLayout(e.NewSize.Width);
        UpdateCategoryToolbarLayout(e.NewSize.Width);
        if (DetailPanel.Visibility == Visibility.Visible) UpdateDetailPanelSize(e.NewSize.Width);
    }

    private void CategoryTabs_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCategoryToolbarLayout(RootLayout.ActualWidth);

    private void LibraryToolbar_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateCategoryToolbarLayout(RootLayout.ActualWidth);

    private void UpdateCategoryToolbarLayout(double windowWidth)
    {
        if (windowWidth <= 0 || _categoryTabButtons.Count == 0) return;
        var gimmickIndex = _categoryTabButtons.FindIndex(tab => Equals(tab.Tag, "ギミック"));
        var requiredTabCount = gimmickIndex >= 0 ? gimmickIndex + 1 : _categoryTabButtons.Count;
        var requiredVisibleWidth = _categoryTabButtons.Take(requiredTabCount)
            .Sum(tab => Math.Max(tab.ActualWidth, tab.DesiredSize.Width))
            + Math.Max(0, requiredTabCount - 1) * CategoryTabs.Spacing;
        if (requiredVisibleWidth <= 0) return;
        var refreshWidth = Math.Max(RefreshLibraryButton.ActualWidth, RefreshLibraryButton.DesiredSize.Width);
        if (refreshWidth <= 0) refreshWidth = 150;
        const double toolbarHorizontalPadding = 48;
        const double toolbarColumnSpacing = 24;
        var toolbarWidth = LibraryToolbar.ActualWidth > 0
            ? LibraryToolbar.ActualWidth
            : Math.Max(0, windowWidth - 76);
        var availableTabWidth = Math.Max(0,
            toolbarWidth - toolbarHorizontalPadding - SortButton.Width - refreshWidth - toolbarColumnSpacing);
        var useSelector = availableTabWidth < requiredVisibleWidth;
        CategoryTabsScroller.Visibility = useSelector ? Visibility.Collapsed : Visibility.Visible;
        CategorySelectorButton.Visibility = useSelector ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateLibraryHeaderLayout(double windowWidth)
    {
        var popup = windowWidth < 900;
        var compact = windowWidth < 1400;
        var columns = LibraryHeader.ColumnDefinitions;
        CompactFiltersButton.Visibility = popup ? Visibility.Visible : Visibility.Collapsed;
        foreach (var control in new UIElement[] { SearchBox, AvatarFilterButton, ShopFilterButton, BoothTagFilterButton, QuickFilterSwitches })
            control.Visibility = popup ? Visibility.Collapsed : Visibility.Visible;
        if (compact)
        {
            columns[0].Width = new GridLength(1, GridUnitType.Star);
            columns[1].Width = new GridLength(1, GridUnitType.Star);
            columns[2].Width = new GridLength(1, GridUnitType.Star);
            columns[3].Width = new GridLength(1, GridUnitType.Star);
            columns[4].Width = new GridLength(0);
            columns[5].Width = new GridLength(0);
            columns[6].Width = new GridLength(0);
            columns[7].Width = new GridLength(0);
            Grid.SetRow(QuickFilterSwitches, 2);
            Grid.SetColumn(QuickFilterSwitches, 0);
            Grid.SetColumnSpan(QuickFilterSwitches, 8);
        }
        else
        {
            columns[0].Width = new GridLength(240); columns[1].Width = new GridLength(160); columns[2].Width = new GridLength(160); columns[3].Width = new GridLength(160);
            columns[4].Width = GridLength.Auto; columns[5].Width = GridLength.Auto; columns[6].Width = GridLength.Auto; columns[7].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(QuickFilterSwitches, 1);
            Grid.SetColumn(QuickFilterSwitches, 4);
            Grid.SetColumnSpan(QuickFilterSwitches, 3);
        }
    }

    private void CompactSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) { if (SearchBox.Text != sender.Text) SearchBox.Text = sender.Text; }
    private void CompactAvatarFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (CompactAvatarFilterBox.SelectedItem is AvatarFilterOption option) { _selectedAvatarFilterId = option.RegistrationId; AvatarFilterButtonText.Text = option.PrimaryIdentifier; UpdateAvatarFilterDependentControls(); _currentPage = 1; ApplyFilter(); } }
    private void CompactShopFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (CompactShopFilterBox.SelectedItem is ShopFilterOption option) { _selectedShopFilter = option.ShopName; ShopFilterButtonText.Text = option.DisplayName; _currentPage = 1; ApplyFilter(); } }
    private void CompactBoothTagFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (CompactBoothTagFilterBox.SelectedItem is BoothTagSummary option) { _selectedBoothTag = option.Count == 0 ? null : option.Name; BoothTagFilterButtonText.Text = _selectedBoothTag ?? "タグ指定なし"; _currentPage = 1; ApplyFilter(); } }
    private void CompactFreeDownloadOnlySwitch_Toggled(object sender, RoutedEventArgs e) { if (FreeDownloadOnlySwitch.IsOn != CompactFreeDownloadOnlySwitch.IsOn) FreeDownloadOnlySwitch.IsOn = CompactFreeDownloadOnlySwitch.IsOn; }
    private void CompactFileUpdateOnlySwitch_Toggled(object sender, RoutedEventArgs e) { if (FileUpdateOnlySwitch.IsOn != CompactFileUpdateOnlySwitch.IsOn) FileUpdateOnlySwitch.IsOn = CompactFileUpdateOnlySwitch.IsOn; }
    private void CompactExcludeSharedBodySwitch_Toggled(object sender, RoutedEventArgs e) { if (ExcludeSharedBodySwitch.IsOn != CompactExcludeSharedBodySwitch.IsOn) ExcludeSharedBodySwitch.IsOn = CompactExcludeSharedBodySwitch.IsOn; }

    private void UpdateDetailPanelSize(double windowWidth)
    {
        var defaultWidth = windowWidth < 1080 ? 300d : 420d;
        var maximumForWindow = Math.Max(MinimumDetailPanelWidth,
            Math.Min(MaximumDetailPanelWidth, windowWidth - 76d - 360d));
        var panelWidth = Math.Clamp(_preferredDetailPanelWidth ?? defaultWidth,
            MinimumDetailPanelWidth, maximumForWindow);
        // Reserve the resize grip, content padding and scrollbar so right-edge controls are never clipped.
        var contentWidth = panelWidth - 76d;
        DetailPanel.Width = panelWidth;
        DetailThumbnailBorder.Width = contentWidth;
        DetailThumbnailBorder.Height = contentWidth;
        DetailLinkRow.Width = contentWidth;
        DetailEditButtonText.Visibility = panelWidth < 380d ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DetailPanelResizeGrip_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootLayout);
        if (!point.Properties.IsLeftButtonPressed) return;
        _isResizingDetailPanel = DetailPanelResizeGrip.CapturePointer(e.Pointer);
        if (!_isResizingDetailPanel) return;
        _detailResizeStartX = point.Position.X;
        _detailResizeStartWidth = DetailPanel.Width;
        e.Handled = true;
    }

    private void DetailPanelResizeGrip_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizingDetailPanel) return;
        var currentX = e.GetCurrentPoint(RootLayout).Position.X;
        _preferredDetailPanelWidth = Math.Clamp(
            _detailResizeStartWidth + (_detailResizeStartX - currentX),
            MinimumDetailPanelWidth, MaximumDetailPanelWidth);
        UpdateDetailPanelSize(RootLayout.ActualWidth);
        e.Handled = true;
    }

    private void DetailPanelResizeGrip_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizingDetailPanel) return;
        DetailPanelResizeGrip.ReleasePointerCapture(e.Pointer);
        FinishDetailPanelResize();
        e.Handled = true;
    }

    private void DetailPanelResizeGrip_PointerCanceled(object sender, PointerRoutedEventArgs e)
        => FinishDetailPanelResize();

    private void DetailPanelResizeGrip_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        => FinishDetailPanelResize();

    private void FinishDetailPanelResize()
    {
        if (!_isResizingDetailPanel) return;
        _isResizingDetailPanel = false;
        if (_preferredDetailPanelWidth is double width)
        {
            _ = Task.Run(() => _metadataStore.SaveDetailPanelWidth(width));
            _isApplyingDetailPanelSizeSetting = true;
            DetailPanelSizeBox.SelectedIndex = 3;
            _isApplyingDetailPanelSizeSetting = false;
        }
    }

    private void AvatarMenu_Click(object sender, RoutedEventArgs e) => SetActivePage(AppPage.Avatar);
    private void LibraryMenu_Click(object sender, RoutedEventArgs e) => SetActivePage(AppPage.Library);
    private void ShopMenu_Click(object sender, RoutedEventArgs e) => SetActivePage(AppPage.Shop);
    private void BoothTagsMenu_Click(object sender, RoutedEventArgs e) => SetActivePage(AppPage.BoothTags);
    private async void ImportSettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DetailPanel.Visibility == Visibility.Visible) await CloseDetailPanelAsync();
        SetActivePage(AppPage.ImportSettings);
    }
    private async void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DetailPanel.Visibility == Visibility.Visible) await CloseDetailPanelAsync();
        SetActivePage(AppPage.Settings);
        await Task.CompletedTask;
    }

    private async void PrivacyMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DetailPanel.Visibility == Visibility.Visible) await CloseDetailPanelAsync();
        SetActivePage(AppPage.Privacy);
    }

    private async void HelpMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DetailPanel.Visibility == Visibility.Visible) await CloseDetailPanelAsync();
        SetActivePage(AppPage.Help);
    }

    private async void AppVersionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DetailPanel.Visibility == Visibility.Visible) await CloseDetailPanelAsync();
        SetActivePage(AppPage.Changelog);
        await CheckForLatestReleaseAsync();
    }

    private void GitHubReleasesButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(GitHubReleasesUrl) { UseShellExecute = true });
    }

    private async Task CheckForLatestReleaseAsync()
    {
        ShowOperationPopup(OperationPopupKind.Progress, "リリースを確認中", "GitHubの公開リリースを確認しています…");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VrcKaihenLibrary");
            using var document = JsonDocument.Parse(await client.GetStringAsync(GitHubLatestReleaseApiUrl));
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagProperty) ? tagProperty.GetString() : null;
            var url = root.TryGetProperty("html_url", out var urlProperty) ? urlProperty.GetString() : GitHubReleasesUrl;
            var latestText = tag?.Trim().TrimStart('v', 'V');
            var currentText = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
            if (Version.TryParse(latestText, out var latest) && Version.TryParse(currentText, out var current) && latest > current)
            {
                ShowOperationPopup(OperationPopupKind.Information, $"新しいバージョン v{latestText} があります", "GitHub Releasesから最新版をダウンロードできます。ブラウザーでリリースページを開きます。", autoDismiss: true);
                Process.Start(new ProcessStartInfo(url ?? GitHubReleasesUrl) { UseShellExecute = true });
            }
            else
            {
                ShowOperationPopup(OperationPopupKind.Success, "最新バージョンです", $"現在のバージョン v{currentText} を使用しています。リリース一覧をブラウザーで開きます。", autoDismiss: true);
                Process.Start(new ProcessStartInfo(GitHubReleasesUrl) { UseShellExecute = true });
            }
        }
        catch
        {
            ShowOperationPopup(OperationPopupKind.Information, "リリースページを開きます", "最新バージョンの自動確認に失敗したため、GitHubのリリース一覧を開きます。", autoDismiss: true);
            Process.Start(new ProcessStartInfo(GitHubReleasesUrl) { UseShellExecute = true });
        }
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
        ImportSettingsStatus.Text = string.Empty;
        ShowOperationPopup(OperationPopupKind.Success, "Unityインポート先を変更しました",
            "分類ごとのインポート先設定を保存しました。", autoDismiss: true);
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

    private void SetActivePage(AppPage page)
    {
        var libraryVisibility = page == AppPage.Library ? Visibility.Visible : Visibility.Collapsed;
        LibraryHeader.Visibility = libraryVisibility; LibraryToolbar.Visibility = libraryVisibility;
        LibraryContent.Visibility = libraryVisibility; LibraryPager.Visibility = libraryVisibility;
        AvatarPage.Visibility = page == AppPage.Avatar ? Visibility.Visible : Visibility.Collapsed;
        ShopPage.Visibility = page == AppPage.Shop ? Visibility.Visible : Visibility.Collapsed;
        BoothTagsPage.Visibility = page == AppPage.BoothTags ? Visibility.Visible : Visibility.Collapsed;
        ImportSettingsPage.Visibility = page == AppPage.ImportSettings ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == AppPage.Settings ? Visibility.Visible : Visibility.Collapsed;
        HelpPage.Visibility = page == AppPage.Help ? Visibility.Visible : Visibility.Collapsed;
        ChangelogPage.Visibility = page == AppPage.Changelog ? Visibility.Visible : Visibility.Collapsed;
        PrivacyPage.Visibility = page == AppPage.Privacy ? Visibility.Visible : Visibility.Collapsed;
        SetMenuAppearance(AvatarMenuButton, page == AppPage.Avatar);
        SetMenuAppearance(LibraryMenuButton, page == AppPage.Library);
        SetMenuAppearance(ShopMenuButton, page == AppPage.Shop);
        SetMenuAppearance(BoothTagsMenuButton, page == AppPage.BoothTags);
        SetMenuAppearance(ImportSettingsMenuButton, page == AppPage.ImportSettings);
        SetMenuAppearance(SettingsMenuButton, page == AppPage.Settings);
        SetMenuAppearance(HelpMenuButton, page == AppPage.Help);
        SetMenuAppearance(PrivacyMenuButton, page == AppPage.Privacy);
        AppVersionButton.Background = new SolidColorBrush(page == AppPage.Changelog
            ? Windows.UI.Color.FromArgb(48, 208, 87, 92)
            : Windows.UI.Color.FromArgb(22, 208, 87, 92));
        AnimatePageEntrance(page);
    }

    private void AnimatePageEntrance(AppPage page)
    {
        var targets = page switch
        {
            AppPage.Library => new FrameworkElement[] { LibraryHeader, LibraryToolbar, LibraryContent, LibraryPager },
            AppPage.Avatar => [AvatarPage],
            AppPage.Shop => [ShopPage],
            AppPage.BoothTags => [BoothTagsPage],
            AppPage.ImportSettings => [ImportSettingsPage],
            AppPage.Settings => [SettingsPage],
            AppPage.Help => [HelpPage],
            AppPage.Changelog => [ChangelogPage],
            AppPage.Privacy => [PrivacyPage],
            _ => []
        };
        foreach (var target in targets)
        {
            target.Opacity = 0;
            target.RenderTransform = new CompositeTransform { TranslateY = 8 };
            var storyboard = new Storyboard();
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(170), EasingFunction = easing };
            var slide = new DoubleAnimation { From = 8, To = 0, Duration = TimeSpan.FromMilliseconds(220), EasingFunction = easing };
            Storyboard.SetTarget(fade, target);
            Storyboard.SetTargetProperty(fade, "Opacity");
            Storyboard.SetTarget(slide, target);
            Storyboard.SetTargetProperty(slide, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");
            storyboard.Children.Add(fade);
            storyboard.Children.Add(slide);
            storyboard.Begin();
        }

        var activeButton = page switch
        {
            AppPage.Avatar => AvatarMenuButton,
            AppPage.Library => LibraryMenuButton,
            AppPage.Shop => ShopMenuButton,
            AppPage.BoothTags => BoothTagsMenuButton,
            AppPage.ImportSettings => ImportSettingsMenuButton,
            AppPage.Settings => SettingsMenuButton,
            AppPage.Help => HelpMenuButton,
            AppPage.Privacy => PrivacyMenuButton,
            AppPage.Changelog => AppVersionButton,
            _ => null
        };
        if (activeButton is null) return;
        activeButton.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        activeButton.RenderTransform = new CompositeTransform { ScaleX = 0.96, ScaleY = 0.96 };
        var buttonStoryboard = new Storyboard();
        foreach (var property in new[] { "(UIElement.RenderTransform).(CompositeTransform.ScaleX)", "(UIElement.RenderTransform).(CompositeTransform.ScaleY)" })
        {
            var animation = new DoubleAnimation
            {
                From = 0.96,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(190),
                EasingFunction = new BackEase { Amplitude = 0.18, EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, activeButton);
            Storyboard.SetTargetProperty(animation, property);
            buttonStoryboard.Children.Add(animation);
        }
        buttonStoryboard.Begin();
    }

    private static void SetMenuAppearance(Button button, bool active)
    {
        button.Background = new SolidColorBrush(active ? BoothAccentColor : Microsoft.UI.Colors.Transparent);
        button.Foreground = new SolidColorBrush(active ? Microsoft.UI.Colors.White : BoothAccentColor);
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
        UpdateAvatarFilterDependentControls();
        AvatarFilterButtonText.Text = "すべての対応アバター";
        _selectedPurchasedPackType = null;
        FreeDownloadOnlySwitch.IsOn = false;
        _showFileUpdatesOnly = false;
        FileUpdateOnlySwitch.IsOn = false;
        _selectedShopFilter = _detailItem.ShopName;
        ShopFilterButtonText.Text = _detailItem.ShopName;
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        _currentPage = 1;
        SetActivePage(AppPage.Library);
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
        UpdateAvatarFilterDependentControls();
        SearchBox.Text = string.Empty;
        _selectedShopFilter = null;
        ShopFilterButtonText.Text = "すべてのショップ";
        _selectedCategory = AllCategories;
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        _currentPage = 1;
        SetActivePage(AppPage.Library);
        ApplyFilter();
    }

    private void BoothTagChip_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DetailTagChip chip }) return;
        ApplyBoothTagNavigationFilter(chip.Text);
    }

    private void BoothTagGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BoothTagSummary tag) return;
        ApplyBoothTagNavigationFilter(tag.Name);
    }

    private void ApplyBoothTagNavigationFilter(string tagName)
    {
        _selectedBoothTag = tagName;
        BoothTagFilterButtonText.Text = tagName;
        SearchBox.Text = string.Empty;
        _selectedCategory = AllCategories;
        _selectedAvatarFilterId = null;
        AvatarFilterButtonText.Text = "すべての対応アバター";
        UpdateAvatarFilterDependentControls();
        _selectedShopFilter = null;
        ShopFilterButtonText.Text = "すべてのショップ";
        foreach (var tab in _categoryTabButtons) UpdateCategoryTabAppearance(tab);
        _currentPage = 1;
        SetActivePage(AppPage.Library);
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
            if (uri.Scheme is "http" or "https" && !BoothNetworkPolicy.IsTrustedImageUri(uri)) return null;
            return new BitmapImage(uri);
        }
        catch { return null; }
    }
}
