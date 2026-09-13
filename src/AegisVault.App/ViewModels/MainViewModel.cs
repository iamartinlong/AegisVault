using System.Collections.ObjectModel;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly VaultService _vault;
    private readonly DispatcherTimer _totpTimer;
    private bool _loadingEditor;

    public event Action? LockRequested;

    public MainViewModel(VaultService vault)
    {
        _vault = vault;

        Entries = new ObservableCollection<PasswordEntry>(vault.Entries);
        FilteredEntries = new ObservableCollection<PasswordEntry>(Entries);
        ApplyFilter();

        _totpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _totpTimer.Tick += (_, _) => UpdateTotp();
        _totpTimer.Start();
    }

    public ObservableCollection<PasswordEntry> Entries { get; }

    public ObservableCollection<PasswordEntry> FilteredEntries { get; }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PasswordEntry? selectedEntry;

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
    private string totpCode = string.Empty;

    [ObservableProperty]
    private int totpRemaining;

    [ObservableProperty]
    private string? statusMessage;

    public bool HasSelection => SelectedEntry is not null;

    public bool HasTotp => !string.IsNullOrWhiteSpace(EditTotpSecret);

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedEntryChanged(PasswordEntry? value) => LoadEditor(value);

    partial void OnEditTotpSecretChanged(string value)
    {
        OnPropertyChanged(nameof(HasTotp));
        UpdateTotp();
    }

    [RelayCommand]
    private void AddEntry()
    {
        var entry = _vault.AddEntry(new PasswordEntry { Title = "新条目" });
        Entries.Add(entry);
        if (MatchesFilter(entry))
        {
            FilteredEntries.Add(entry);
        }

        SelectedEntry = entry;
        StatusMessage = "已创建新条目。";
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
        };

        if (!_vault.UpdateEntry(updated))
        {
            StatusMessage = "条目已不存在。";
            return;
        }

        ReplaceInList(updated);
        SelectedEntry = updated;
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
            StatusMessage = "已删除条目。";
        }
    }

    [RelayCommand]
    private void Lock()
    {
        _vault.Lock();
        LockRequested?.Invoke();
    }

    public void Dispose()
    {
        _totpTimer.Stop();
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
            StatusMessage = null;
        }
        finally
        {
            _loadingEditor = false;
        }

        OnPropertyChanged(nameof(HasSelection));
        UpdateTotp();
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        foreach (var entry in Entries.Where(MatchesFilter))
        {
            FilteredEntries.Add(entry);
        }
    }

    private bool MatchesFilter(PasswordEntry entry)
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
