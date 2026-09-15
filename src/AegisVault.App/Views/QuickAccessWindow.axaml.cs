using AegisVault.App.ViewModels;
using AegisVault.Core.Models;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AegisVault.App.Views;

public partial class QuickAccessWindow : Window
{
    public QuickAccessWindow()
    {
        InitializeComponent();
        Opened += (_, _) => SearchBox.Focus();
        Deactivated += (_, _) => Hide();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not QuickAccessViewModel viewModel)
        {
            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                viewModel.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                viewModel.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    viewModel.CopyPasswordAndOpenCommand.Execute(viewModel.SelectedEntry);
                }
                else
                {
                    viewModel.ActivateCommand.Execute(null);
                }

                e.Handled = true;
                break;
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            default:
                base.OnKeyDown(e);
                break;
        }
    }

    private void OnCopyUsernameClicked(object? sender, RoutedEventArgs e)
        => InvokeForEntry(sender, (viewModel, entry) => viewModel.CopyUsernameCommand.Execute(entry));

    private void OnCopyPasswordClicked(object? sender, RoutedEventArgs e)
        => InvokeForEntry(sender, (viewModel, entry) => viewModel.CopyPasswordCommand.Execute(entry));

    private void OnCopyPasswordAndOpenClicked(object? sender, RoutedEventArgs e)
        => InvokeForEntry(sender, (viewModel, entry) => viewModel.CopyPasswordAndOpenCommand.Execute(entry));

    private void InvokeForEntry(object? sender, Action<QuickAccessViewModel, PasswordEntry> action)
    {
        if (sender is Button { DataContext: PasswordEntry entry } && DataContext is QuickAccessViewModel viewModel)
        {
            action(viewModel, entry);
        }
    }
}
