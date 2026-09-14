using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AegisVault.App.ViewModels;

/// <summary>
/// Lightweight view model shown on the main window while the vault is locked
/// in place. Keeps no secret state — the full MainViewModel is disposed on lock.
/// </summary>
public partial class LockViewModel : ObservableObject
{
    /// <summary>Raised when the user asks to unlock the vault.</summary>
    public event Action? UnlockRequested;

    [RelayCommand]
    private void Unlock() => UnlockRequested?.Invoke();
}
