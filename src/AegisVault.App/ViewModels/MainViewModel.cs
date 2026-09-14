using System.Collections.ObjectModel;
using System.Diagnostics;
using AegisVault.App.Services;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public sealed record CategoryItem(string Key, string DisplayName, string? Tag, bool IsFavorites, int Count);

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const string AllCategoryKey = "all";
    private const string FavoritesCategoryKey = "favorites";
    private const string WeakCategoryKey = "weak";

    private readonly VaultService _vault;
    private readonly ClipboardService? _clipboard;
    private readonly DispatcherTimer _totpTimer;
    private readonly DispatcherTimer _toastTimer;
    private bool _loadingEditor;
    private PasswordStrengthResult? _editPasswordStrength;
    private VaultHealthReport _health = new(0, 0, 0, 0, new HashSet<Guid>(), new HashSet<Guid>());

    public event Action? LockRequested;

    public MainViewModel(VaultService vault, ClipboardService? clipboard = null)
    {
        _vault = vault;
        _clipboard = clipboard;

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

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private CategoryItem? selectedCategory;

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
    private string editUrl = string.Empty;

    [ObservableProperty]
    private string editNotes = string.Empty;

    [ObservableProperty]
    private string editTotpSecret = string.Empty;

    [ObservableProperty]
    private string editTags = string.Empty;

    [ObservableProperty]
    private bool editIsFavorite;

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

    public string ClipboardToastText => $"已复制到剪贴板，{ClipboardToastRemaining} 秒后自动清除。";

    public VaultHealthReport Health => _health;

    public bool HasSecurityIssues => _health.WeakCount > 0 || _health.ReusedCount > 0;

    public string HealthSummary => _health.TotalEntries == 0
        ? "密码库为空"
        : HasSecurityIssues
            ? $"安全：{_health.WeakCount} 条弱密码，{_health.ReusedCount} 条重复"
            : $"安全：{_health.TotalEntries} 条密码全部良好";

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

    partial void OnSelectedCategoryChanged(CategoryItem? value) => ApplyFilter();

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

    [RelayCommand]
    private void AddEntry()
    {
        var entry = _vault.AddEntry(new PasswordEntry { Title = "新条目" });
        Entries.Add(entry);
        RefreshCategories();
        ApplyFilter();
        SelectedEntry = entry;
        IsEditing = true;
        StatusMessage = "已创建新条目，请填写信息。";
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

    [RelayCommand]
    private void SaveEntry()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var updated = SelectedEntry with
        {
            Title = EditTitle.Trim(),
            Username = EditUsername,
            Password = EditPassword,
            Url = EditUrl.Trim(),
            Notes = EditNotes,
            TotpSecret = EditTotpSecret.Trim(),
            Tags = ParseTags(EditTags),
            IsFavorite = EditIsFavorite,
        };

        if (!_vault.UpdateEntry(updated))
        {
            StatusMessage = "条目已不存在。";
            return;
        }

        ReplaceInList(updated);
        SelectedEntry = updated;
        IsEditing = false;
        RefreshCategories();
        ApplyFilter();
        StatusMessage = "已保存。";
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
            StatusMessage = "已删除条目。";
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
        StatusMessage = updated.IsFavorite ? "已加入收藏。" : "已取消收藏。";
    }

    [RelayCommand]
    private void TogglePasswordReveal() => IsPasswordRevealed = !IsPasswordRevealed;

    [RelayCommand]
    private void ShowSecurity()
    {
        var category = Categories.FirstOrDefault(item => item.Key == WeakCategoryKey);
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
        StatusMessage = "密码已复制，将按设置自动清除。";
    }

    [RelayCommand]
    private async Task CopyUsernameAsync()
    {
        if (_clipboard is null || string.IsNullOrEmpty(EditUsername))
        {
            return;
        }

        await _clipboard.CopyAsync(EditUsername);
        StatusMessage = "用户名已复制。";
    }

    [RelayCommand]
    private async Task CopyTotpAsync()
    {
        if (_clipboard is null || !IsTotpValid)
        {
            return;
        }

        await _clipboard.CopyAsync(TotpCode);
        StatusMessage = "验证码已复制，将按设置自动清除。";
    }

    [RelayCommand]
    private void OpenUrl()
    {
        var url = EditUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
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

        _health = VaultHealth.Analyze(Entries);
        OnPropertyChanged(nameof(Health));
        OnPropertyChanged(nameof(HasSecurityIssues));
        OnPropertyChanged(nameof(HealthSummary));

        Categories.Clear();
        Categories.Add(new CategoryItem(AllCategoryKey, "全部条目", null, false, Entries.Count));
        Categories.Add(new CategoryItem(
            FavoritesCategoryKey,
            "收藏",
            null,
            true,
            Entries.Count(entry => entry.IsFavorite)));

        var issueCount = _health.IssueCount;
        if (issueCount > 0)
        {
            Categories.Add(new CategoryItem(WeakCategoryKey, "安全", null, false, issueCount));
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
                Entries.Count(entry => entry.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))));
        }

        SelectedCategory = Categories.FirstOrDefault(category => category.Key == selectedKey) ?? Categories[0];
    }

    private void ApplyFilter()
    {
        if (_loadingEditor)
        {
            return;
        }

        FilteredEntries.Clear();
        foreach (var entry in Entries
                     .Where(entry => MatchesCategory(entry) && MatchesSearch(entry))
                     .OrderByDescending(static entry => entry.IsFavorite)
                     .ThenBy(static entry => entry.Title, StringComparer.OrdinalIgnoreCase))
        {
            FilteredEntries.Add(entry);
        }

        if (SelectedEntry is not null && !FilteredEntries.Contains(SelectedEntry))
        {
            SelectedEntry = null;
        }

        OnPropertyChanged(nameof(HasFilteredEntries));
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
            EditUrl = entry?.Url ?? string.Empty;
            EditNotes = entry?.Notes ?? string.Empty;
            EditTotpSecret = entry?.TotpSecret ?? string.Empty;
            EditTags = entry is null ? string.Empty : string.Join(", ", entry.Tags);
            EditIsFavorite = entry?.IsFavorite ?? false;
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
            TotpCode = "无效密钥";
            TotpRemaining = 0;
        }
    }

    private static bool Contains(string? value, string query)
        => value is not null && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static List<string> ParseTags(string text)
        => [.. text.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];
}
