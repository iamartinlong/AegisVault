using System.Collections.ObjectModel;
using AegisVault.App.Localization;
using AegisVault.App.Services;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public enum CategoryKind
{
    System,
    Category,
    Tag,
}

public enum EntrySortMode
{
    Name,
    RecentlyUpdated,
}

public sealed record CategoryItem(
    string Key,
    string DisplayName,
    string? Tag,
    bool IsFavorites,
    int Count,
    CategoryKind Kind = CategoryKind.System,
    Guid? CategoryId = null)
{
    public bool IsUserCategory => Kind == CategoryKind.Category;

    public string Glyph => Kind switch
    {
        CategoryKind.Category => "📁",
        CategoryKind.Tag => "#",
        _ => string.Empty,
    };
}

/// <summary>ComboBox item for assigning an entry to a category (null = uncategorized).</summary>
public sealed record CategoryChoice(Guid? Id, string Name);

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const string AllCategoryKey = "all";
    private const string FavoritesCategoryKey = "favorites";
    private const string WeakCategoryKey = "weak";
    private const string StaleCategoryKey = "old";
    private const string CategoryKeyPrefix = "cat:";

    private readonly VaultService _vault;
    private readonly ClipboardService? _clipboard;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _totpTimer;
    private readonly DispatcherTimer _toastTimer;
    private bool _loadingEditor;
    private PasswordStrengthResult? _editPasswordStrength;
    private readonly IUrlLauncher _urlLauncher;
    private VaultHealthReport _health = new(0, 0, 0, new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>());

    public event Action? LockRequested;

    public MainViewModel(VaultService vault, ClipboardService? clipboard = null, TimeProvider? timeProvider = null, IUrlLauncher? urlLauncher = null)
    {
        _vault = vault;
        _clipboard = clipboard;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _urlLauncher = urlLauncher ?? SystemUrlLauncher.Instance;

        Entries = new ObservableCollection<PasswordEntry>(vault.Entries);
        FilteredEntries = [];
        Categories = [];

        RefreshCategories();
        ApplyFilter();

        _totpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _totpTimer.Tick += (_, _) => UpdateTotp();
        _totpTimer.Start();

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _toastTimer.Tick += (_, _) => OnToastTimerTick();

        if (_clipboard is not null)
        {
            _clipboard.CopyStarted += OnClipboardCopyStarted;
        }
    }

    public ObservableCollection<PasswordEntry> Entries { get; }

    public ObservableCollection<PasswordEntry> FilteredEntries { get; }

    public ObservableCollection<CategoryItem> Categories { get; }

    /// <summary>Sidebar sections: smart views, user categories, tag views.</summary>
    public ObservableCollection<CategoryItem> SystemCategories { get; } = [];

    public ObservableCollection<CategoryItem> UserCategories { get; } = [];

    public ObservableCollection<CategoryItem> TagCategories { get; } = [];

    public ObservableCollection<CategoryChoice> CategoryChoices { get; } = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private CategoryItem? selectedCategory;

    [ObservableProperty]
    private CategoryItem? selectedSystemCategory;

    [ObservableProperty]
    private CategoryItem? selectedUserCategory;

    [ObservableProperty]
    private CategoryItem? selectedTagCategory;

    partial void OnSelectedSystemCategoryChanged(CategoryItem? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedCategory = value;
        SelectedUserCategory = null;
        SelectedTagCategory = null;
    }

    partial void OnSelectedUserCategoryChanged(CategoryItem? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedCategory = value;
        SelectedSystemCategory = null;
        SelectedTagCategory = null;
    }

    partial void OnSelectedTagCategoryChanged(CategoryItem? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedCategory = value;
        SelectedSystemCategory = null;
        SelectedUserCategory = null;
    }

    [ObservableProperty]
    private CategoryChoice? selectedCategoryChoice;

    public string CategoryDisplayName => SelectedCategoryChoice?.Name ?? Loc.T("Main_NoCategory");

    partial void OnSelectedCategoryChoiceChanged(CategoryChoice? value) => OnPropertyChanged(nameof(CategoryDisplayName));

    [ObservableProperty]
    private PasswordEntry? selectedEntry;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private bool isPasswordRevealed;

    [ObservableProperty]
    private string editTitle = string.Empty;

    [ObservableProperty]
    private string editUsername = string.Empty;

    [ObservableProperty]
    private string editPassword = string.Empty;

    [ObservableProperty]
    private string editUrls = string.Empty;

    [ObservableProperty]
    private string editNotes = string.Empty;

    [ObservableProperty]
    private string editTotpSecret = string.Empty;

    [ObservableProperty]
    private string editTags = string.Empty;

    [ObservableProperty]
    private bool editIsFavorite;

    /// <summary>Inline validation message under the title field (empty when valid).</summary>
    [ObservableProperty]
    private string titleError = string.Empty;

    /// <summary>Inline validation message under the URL field (empty when valid).</summary>
    [ObservableProperty]
    private string urlError = string.Empty;

    /// <summary>Local-time creation timestamp shown in the detail preview.</summary>
    [ObservableProperty]
    private string createdAtDisplay = string.Empty;

    [ObservableProperty]
    private string totpCode = string.Empty;

    [ObservableProperty]
    private int totpRemaining;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isClipboardToastVisible;

    [ObservableProperty]
    private int clipboardToastRemaining;

    public string ClipboardToastText => Loc.Format("Main_ToastFormat", ClipboardToastRemaining);

    public VaultHealthReport Health => _health;

    public bool HasSecurityIssues => _health.WeakCount > 0 || _health.ReusedCount > 0;

    public string HealthSummary => _health.TotalEntries == 0
        ? Loc.T("Main_HealthEmpty")
        : HasSecurityIssues
            ? Loc.Format("Main_HealthIssues", _health.WeakCount, _health.ReusedCount, _health.OldCount)
            : _health.OldCount > 0
                ? Loc.Format("Main_HealthStale", _health.TotalEntries, _health.OldCount)
                : Loc.Format("Main_HealthOk", _health.TotalEntries);

    public string HealthCompactText =>
        Loc.Format("Main_HealthCompact", _health.WeakCount, _health.ReusedCount, _health.OldCount);

    public bool HasSelection => SelectedEntry is not null;

    public bool HasFilteredEntries => FilteredEntries.Count > 0;

    public bool HasTotp => !string.IsNullOrWhiteSpace(EditTotpSecret);

    public bool IsTotpValid => TotpCode.Length is 6 or 8 && TotpCode.All(char.IsDigit);

    public string PasswordPreview => IsPasswordRevealed
        ? EditPassword
        : new string('●', Math.Clamp(EditPassword.Length, 6, 14));

    public string EditPasswordStrengthSummary => _editPasswordStrength is null
        ? string.Empty
        : StrengthFormatting.FormatSummary(
            _editPasswordStrength.Score,
            _editPasswordStrength.Label,
            _editPasswordStrength.CrackTime);

    public double EditPasswordStrengthPercent => (_editPasswordStrength?.Score ?? 0) * 25;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [ObservableProperty]
    private EntrySortMode sortMode = EntrySortMode.Name;

    public bool IsSortByName => SortMode == EntrySortMode.Name;

    public bool IsSortByRecent => SortMode == EntrySortMode.RecentlyUpdated;

    public bool IsViewAll => SelectedCategory?.Key == AllCategoryKey;

    public bool IsViewFavorites => SelectedCategory?.Key == FavoritesCategoryKey;

    public bool IsViewSecurity => SelectedCategory?.Key == WeakCategoryKey;

    public bool IsViewStale => SelectedCategory?.Key == StaleCategoryKey;

    public string FilteredCountText => Loc.Format("Main_EntryCountFormat", FilteredEntries.Count);

    partial void OnSortModeChanged(EntrySortMode value)
    {
        OnPropertyChanged(nameof(IsSortByName));
        OnPropertyChanged(nameof(IsSortByRecent));
        ApplyFilter();
    }

    [RelayCommand]
    private void SortByName() => SortMode = EntrySortMode.Name;

    [RelayCommand]
    private void SortByRecent() => SortMode = EntrySortMode.RecentlyUpdated;

    [RelayCommand]
    private void SelectView(string? key)
    {
        var category = Categories.FirstOrDefault(item => item.Key == key);
        if (category is not null)
        {
            SelectedCategory = category;
        }
    }

    partial void OnSelectedCategoryChanged(CategoryItem? value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsViewAll));
        OnPropertyChanged(nameof(IsViewFavorites));
        OnPropertyChanged(nameof(IsViewSecurity));
        OnPropertyChanged(nameof(IsViewStale));
    }

    partial void OnSelectedEntryChanged(PasswordEntry? value)
    {
        IsEditing = false;
        IsPasswordRevealed = false;
        LoadEditor(value);
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnEditTotpSecretChanged(string value)
    {
        OnPropertyChanged(nameof(HasTotp));
        UpdateTotp();
    }

    partial void OnTotpCodeChanged(string value) => OnPropertyChanged(nameof(IsTotpValid));

    partial void OnIsPasswordRevealedChanged(bool value) => OnPropertyChanged(nameof(PasswordPreview));

    partial void OnEditPasswordChanged(string value)
    {
        _editPasswordStrength = string.IsNullOrEmpty(value)
            ? null
            : PasswordStrengthEstimator.Evaluate(value);

        OnPropertyChanged(nameof(PasswordPreview));
        OnPropertyChanged(nameof(EditPasswordStrengthSummary));
        OnPropertyChanged(nameof(EditPasswordStrengthPercent));
    }

    partial void OnEditTitleChanged(string value)
    {
        if (TitleError.Length > 0)
        {
            TitleError = string.Empty;
        }
    }

    partial void OnEditUrlsChanged(string value)
    {
        if (UrlError.Length > 0)
        {
            UrlError = string.Empty;
        }
    }

    private static List<string> EffectiveUrls(PasswordEntry entry)
    {
        // Source-generated JSON leaves new collection properties null when the
        // field is missing from older vault files, so never assume non-null.
        if (entry.Urls is { Count: > 0 })
        {
            return entry.Urls.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
        }

        return string.IsNullOrWhiteSpace(entry.Url) ? [] : [entry.Url];
    }

    [RelayCommand]
    private void AddEntry()
    {
        var defaultCategoryId = SelectedCategory is { Kind: CategoryKind.Category, CategoryId: { } categoryId }
            ? categoryId
            : (Guid?)null;

        var entry = _vault.AddEntry(new PasswordEntry
        {
            Title = Loc.T("Main_NewEntryTitle"),
            CategoryId = defaultCategoryId,
        });
        Entries.Add(entry);
        RefreshCategories();
        ApplyFilter();
        SelectedEntry = entry;
        IsEditing = true;
        StatusMessage = Loc.T("Main_StatusNewEntry");
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        LoadEditor(SelectedEntry);
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        LoadEditor(SelectedEntry);
        IsEditing = false;
    }

    /// <summary>URLs of the selected entry, one preview row each.</summary>
    public ObservableCollection<string> UrlItems { get; } = [];

    [RelayCommand]
    private void SaveEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var title = EditTitle.Trim();
        var urls = new List<string>();
        string? urlError = null;
        var ordinal = 0;

        foreach (var line in EditUrls.Replace("\r\n", "\n").Split('\n'))
        {
            var raw = line.Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            ordinal++;
            var normalized = WebUrl.Normalize(raw);
            if (normalized is null)
            {
                urlError ??= Loc.Format("Main_UrlInvalidLine", ordinal);
                continue;
            }

            if (!urls.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                urls.Add(normalized);
            }
        }

        TitleError = title.Length == 0 ? Loc.T("Main_TitleRequired") : string.Empty;
        UrlError = urlError ?? string.Empty;
        if (title.Length == 0 || urlError is not null)
        {
            return;
        }

        var updated = SelectedEntry with
        {
            Title = title,
            Username = EditUsername,
            Password = EditPassword,
            Url = urls.Count > 0 ? urls[0] : string.Empty,
            Urls = urls,
            Notes = EditNotes,
            TotpSecret = EditTotpSecret.Trim(),
            Tags = ParseTags(EditTags),
            IsFavorite = EditIsFavorite,
            CategoryId = SelectedCategoryChoice?.Id,
        };

        if (!_vault.UpdateEntry(updated))
        {
            StatusMessage = Loc.T("Main_StatusEntryMissing");
            return;
        }

        ReplaceInList(updated);
        SelectedEntry = updated;
        IsEditing = false;
        RefreshCategories();
        ApplyFilter();
        StatusMessage = Loc.T("Main_StatusSaved");
    }

    [RelayCommand]
    private void DeleteEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var target = SelectedEntry;
        if (_vault.DeleteEntry(target.Id))
        {
            Entries.Remove(target);
            FilteredEntries.Remove(target);
            SelectedEntry = null;
            IsEditing = false;
            RefreshCategories();
            ApplyFilter();
            StatusMessage = Loc.T("Main_StatusDeleted");
        }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var updated = SelectedEntry with
        {
            IsFavorite = !SelectedEntry.IsFavorite,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        if (!_vault.UpdateEntry(updated))
        {
            return;
        }

        ReplaceInList(updated);
        SelectedEntry = updated;
        RefreshCategories();
        ApplyFilter();
        StatusMessage = updated.IsFavorite ? Loc.T("Main_StatusFavoriteAdded") : Loc.T("Main_StatusFavoriteRemoved");
    }

    [RelayCommand]
    private void TogglePasswordReveal() => IsPasswordRevealed = !IsPasswordRevealed;

    [RelayCommand]
    private void ShowSecurity()
    {
        var category = Categories.FirstOrDefault(item => item.Key == WeakCategoryKey)
            ?? Categories.FirstOrDefault(item => item.Key == StaleCategoryKey);
        if (category is not null)
        {
            SelectedCategory = category;
        }
    }

    [RelayCommand]
    private async Task CopyPasswordAsync()
    {
        if (_clipboard is null || string.IsNullOrEmpty(EditPassword))
        {
            return;
        }

        await _clipboard.CopyAsync(EditPassword);
        StatusMessage = Loc.T("Main_StatusPasswordCopied");
    }

    [RelayCommand]
    private async Task CopyUsernameAsync()
    {
        if (_clipboard is null || string.IsNullOrEmpty(EditUsername))
        {
            return;
        }

        await _clipboard.CopyAsync(EditUsername);
        StatusMessage = Loc.T("Main_StatusUsernameCopied");
    }

    [RelayCommand]
    private async Task CopyTotpAsync()
    {
        if (_clipboard is null || !IsTotpValid)
        {
            return;
        }

        await _clipboard.CopyAsync(TotpCode);
        StatusMessage = Loc.T("Main_StatusTotpCopied");
    }

    [RelayCommand]
    private void OpenUrl(string? url)
    {
        var normalized = WebUrl.Normalize(url);
        if (normalized is null)
        {
            StatusMessage = Loc.T("Main_UrlInvalid");
            return;
        }

        if (normalized.Length == 0)
        {
            StatusMessage = Loc.T("Main_StatusUrlEmpty");
            return;
        }

        if (!_urlLauncher.TryOpen(normalized))
        {
            StatusMessage = Loc.T("Main_StatusUrlOpenFailed");
        }
    }

    [RelayCommand]
    private void Lock()
    {
        _vault.Lock();
        LockRequested?.Invoke();
    }

    /// <summary>Rebuilds the entry list from the vault (e.g. after an import).</summary>
    public void ReloadFromVault()
    {
        SelectedEntry = null;
        Entries.Clear();
        foreach (var entry in _vault.Entries)
        {
            Entries.Add(entry);
        }

        RefreshCategories();
        ApplyFilter();
    }

    public void Dispose()
    {
        _totpTimer.Stop();
        _toastTimer.Stop();

        if (_clipboard is not null)
        {
            _clipboard.CopyStarted -= OnClipboardCopyStarted;
        }
    }

    private void OnClipboardCopyStarted(TimeSpan delay)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
        ClipboardToastRemaining = seconds;
        IsClipboardToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void OnToastTimerTick()
    {
        if (ClipboardToastRemaining <= 1)
        {
            _toastTimer.Stop();
            IsClipboardToastVisible = false;
            ClipboardToastRemaining = 0;
            return;
        }

        ClipboardToastRemaining--;
    }

    [RelayCommand]
    private async Task ClearClipboardNowAsync()
    {
        _toastTimer.Stop();
        IsClipboardToastVisible = false;
        if (_clipboard is not null)
        {
            await _clipboard.ClearIfUnchangedAsync();
        }
    }

    private void RefreshCategories()
    {
        var selectedKey = SelectedCategory?.Key;

        _health = VaultHealth.Analyze(Entries, _timeProvider);
        OnPropertyChanged(nameof(Health));
        OnPropertyChanged(nameof(HasSecurityIssues));
        OnPropertyChanged(nameof(HealthSummary));
        OnPropertyChanged(nameof(HealthCompactText));

        Categories.Clear();
        Categories.Add(new CategoryItem(AllCategoryKey, Loc.T("Main_CategoryAll"), null, false, Entries.Count));
        Categories.Add(new CategoryItem(
            FavoritesCategoryKey,
            Loc.T("Main_CategoryFavorites"),
            null,
            true,
            Entries.Count(entry => entry.IsFavorite)));

        var issueCount = _health.IssueCount;
        if (issueCount > 0)
        {
            Categories.Add(new CategoryItem(WeakCategoryKey, Loc.T("Main_CategorySecurity"), null, false, issueCount));
        }

        if (_health.OldCount > 0)
        {
            Categories.Add(new CategoryItem(StaleCategoryKey, Loc.T("Main_CategoryStale"), null, false, _health.OldCount));
        }

        foreach (var category in _vault.Categories)
        {
            Categories.Add(new CategoryItem(
                CategoryKeyPrefix + category.Id.ToString("D"),
                category.Name,
                null,
                false,
                Entries.Count(entry => entry.CategoryId == category.Id),
                CategoryKind.Category,
                category.Id));
        }

        if (_vault.Categories.Count > 0 && Entries.Any(entry => entry.CategoryId is null))
        {
            Categories.Add(new CategoryItem(
                CategoryKeyPrefix,
                Loc.T("Main_Uncategorized"),
                null,
                false,
                Entries.Count(entry => entry.CategoryId is null),
                CategoryKind.Category));
        }

        var tags = Entries
            .SelectMany(entry => entry.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            Categories.Add(new CategoryItem(
                "tag:" + tag,
                tag,
                tag,
                false,
                Entries.Count(entry => entry.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)),
                CategoryKind.Tag));
        }

        SelectedCategory = Categories.FirstOrDefault(category => category.Key == selectedKey) ?? Categories[0];
        RebuildCategorySections(SelectedCategory);
        RefreshCategoryChoices();
    }

    private void RebuildCategorySections(CategoryItem? selected)
    {
        SystemCategories.Clear();
        UserCategories.Clear();
        TagCategories.Clear();

        foreach (var category in Categories)
        {
            switch (category.Kind)
            {
                case CategoryKind.System:
                    SystemCategories.Add(category);
                    break;
                case CategoryKind.Category:
                    UserCategories.Add(category);
                    break;
                default:
                    TagCategories.Add(category);
                    break;
            }
        }

        SelectedSystemCategory = null;
        SelectedUserCategory = null;
        SelectedTagCategory = null;

        switch (selected?.Kind)
        {
            case CategoryKind.System:
                SelectedSystemCategory = SystemCategories.FirstOrDefault(category => category.Key == selected.Key);
                break;
            case CategoryKind.Category:
                SelectedUserCategory = UserCategories.FirstOrDefault(category => category.Key == selected.Key);
                break;
            case CategoryKind.Tag:
                SelectedTagCategory = TagCategories.FirstOrDefault(category => category.Key == selected.Key);
                break;
        }
    }

    private void RefreshCategoryChoices()
    {
        var selectedId = SelectedCategoryChoice?.Id;

        CategoryChoices.Clear();
        CategoryChoices.Add(new CategoryChoice(null, Loc.T("Main_NoCategory")));
        foreach (var category in _vault.Categories)
        {
            CategoryChoices.Add(new CategoryChoice(category.Id, category.Name));
        }

        SelectedCategoryChoice = CategoryChoices.FirstOrDefault(choice => choice.Id == selectedId) ?? CategoryChoices[0];
    }

    /// <summary>Creates a user category; returns false with a localized reason when invalid.</summary>
    public bool TryCreateCategory(string? name, out string? error)
    {
        error = ValidateCategoryName(name, excludeId: null);
        if (error is not null)
        {
            return false;
        }

        var category = _vault.AddCategory(name!.Trim());
        RefreshCategories();
        SelectedCategoryChoice = CategoryChoices.First(choice => choice.Id == category.Id);
        StatusMessage = Loc.T("Main_StatusCategoryCreated");
        return true;
    }

    /// <summary>Renames a user category; returns false with a localized reason when invalid.</summary>
    public bool TryRenameCategory(Guid id, string? name, out string? error)
    {
        error = ValidateCategoryName(name, excludeId: id);
        if (error is not null)
        {
            return false;
        }

        if (!_vault.RenameCategory(id, name!.Trim()))
        {
            error = Loc.T("Main_CategoryMissing");
            return false;
        }

        RefreshCategories();
        StatusMessage = Loc.T("Main_StatusCategoryRenamed");
        return true;
    }

    public void DeleteCategory(Guid id)
    {
        if (!_vault.DeleteCategory(id))
        {
            return;
        }

        ReloadFromVault();
        StatusMessage = Loc.T("Main_StatusCategoryDeleted");
    }

    /// <summary>Validates a category name for the create/rename dialogs (null = valid).</summary>
    public string? ValidateCategoryName(string? name, Guid? excludeId = null)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Loc.T("Main_CategoryNameRequired");
        }

        var duplicate = _vault.Categories.Any(category =>
            category.Id != excludeId &&
            string.Equals(category.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        return duplicate ? Loc.T("Main_CategoryNameDuplicate") : null;
    }

    private void ApplyFilter()
    {
        if (_loadingEditor)
        {
            return;
        }

        FilteredEntries.Clear();
        var matches = Entries.Where(entry => MatchesCategory(entry) && MatchesSearch(entry));
        var ordered = SortMode == EntrySortMode.RecentlyUpdated
            ? matches.OrderByDescending(static entry => entry.UpdatedAt)
            : matches.OrderByDescending(static entry => entry.IsFavorite)
                .ThenBy(static entry => entry.Title, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ordered)
        {
            FilteredEntries.Add(entry);
        }

        if (SelectedEntry is not null && !FilteredEntries.Contains(SelectedEntry))
        {
            SelectedEntry = null;
        }

        OnPropertyChanged(nameof(HasFilteredEntries));
        OnPropertyChanged(nameof(FilteredCountText));
    }

    private bool MatchesCategory(PasswordEntry entry)
    {
        if (SelectedCategory is null || SelectedCategory.Key == AllCategoryKey)
        {
            return true;
        }

        if (SelectedCategory.Key == WeakCategoryKey)
        {
            return _health.WeakEntryIds.Contains(entry.Id) || _health.ReusedEntryIds.Contains(entry.Id);
        }

        if (SelectedCategory.Key == StaleCategoryKey)
        {
            return _health.OldEntryIds.Contains(entry.Id);
        }

        if (SelectedCategory.Key.StartsWith(CategoryKeyPrefix, StringComparison.Ordinal))
        {
            var suffix = SelectedCategory.Key[CategoryKeyPrefix.Length..];
            return suffix.Length == 0
                ? entry.CategoryId is null
                : entry.CategoryId == Guid.Parse(suffix);
        }

        if (SelectedCategory.IsFavorites)
        {
            return entry.IsFavorite;
        }

        return SelectedCategory.Tag is { } tag &&
               entry.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    private bool MatchesSearch(PasswordEntry entry)
    {
        var query = SearchText?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return true;
        }

        return Contains(entry.Title, query) ||
               Contains(entry.Username, query) ||
               Contains(entry.Url, query) ||
               (entry.Urls?.Any(url => Contains(url, query)) ?? false) ||
               entry.Tags.Any(tag => Contains(tag, query));
    }

    private void LoadEditor(PasswordEntry? entry)
    {
        _loadingEditor = true;
        try
        {
            EditTitle = entry?.Title ?? string.Empty;
            EditUsername = entry?.Username ?? string.Empty;
            EditPassword = entry?.Password ?? string.Empty;
            var urls = entry is null ? [] : EffectiveUrls(entry);
            EditUrls = string.Join("\n", urls);
            UrlItems.Clear();
            foreach (var url in urls)
            {
                UrlItems.Add(url);
            }

            EditNotes = entry?.Notes ?? string.Empty;
            EditTotpSecret = entry?.TotpSecret ?? string.Empty;
            EditTags = entry is null ? string.Empty : string.Join(", ", entry.Tags);
            EditIsFavorite = entry?.IsFavorite ?? false;
            SelectedCategoryChoice = CategoryChoices.FirstOrDefault(choice => choice.Id == entry?.CategoryId)
                ?? CategoryChoices.FirstOrDefault();
            TitleError = string.Empty;
            UrlError = string.Empty;
            CreatedAtDisplay = entry is null
                ? string.Empty
                : entry.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            StatusMessage = null;
        }
        finally
        {
            _loadingEditor = false;
        }

        IsPasswordRevealed = false;
        OnPropertyChanged(nameof(PasswordPreview));
        UpdateTotp();
    }

    private void ReplaceInList(PasswordEntry updated)
    {
        var previous = SelectedEntry;
        if (previous is null)
        {
            return;
        }

        var allIndex = Entries.IndexOf(previous);
        if (allIndex >= 0)
        {
            Entries[allIndex] = updated;
        }

        var filteredIndex = FilteredEntries.IndexOf(previous);
        if (filteredIndex >= 0)
        {
            FilteredEntries[filteredIndex] = updated;
        }
    }

    partial void OnClipboardToastRemainingChanged(int value) => OnPropertyChanged(nameof(ClipboardToastText));

    private void UpdateTotp()
    {
        if (_loadingEditor)
        {
            return;
        }

        if (!HasTotp)
        {
            TotpCode = string.Empty;
            TotpRemaining = 0;
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            TotpCode = TotpService.GenerateCode(EditTotpSecret, now);
            TotpRemaining = TotpService.GetRemainingSeconds(now);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            TotpCode = Loc.T("Main_StatusInvalidTotp");
            TotpRemaining = 0;
        }
    }

    private static bool Contains(string? value, string query)
        => value is not null && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static List<string> ParseTags(string text)
        => [.. text.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];
}
