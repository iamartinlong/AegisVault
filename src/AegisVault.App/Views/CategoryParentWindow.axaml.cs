using System.Collections.Generic;
using AegisVault.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AegisVault.App.Views;

/// <summary>
/// Picks a category from an indented list. Used for "move to…" (a parent) and
/// "merge into…" (a target category), which is why the list is a flat list of
/// choices rather than the sidebar tree itself.
/// </summary>
public partial class CategoryParentWindow : Window
{
    public CategoryParentWindow()
    {
        InitializeComponent();
    }

    public CategoryParentWindow(
        string title,
        string message,
        IReadOnlyList<CategoryChoice> choices,
        Guid? initialSelection,
        string? okText = null,
        string? cancelText = null)
        : this()
    {
        Title = title;
        TitleBar.Title = title;
        MessageText.Text = message;

        ParentList.ItemsSource = choices;
        var index = choices.ToList().FindIndex(choice => choice.Id == initialSelection);
        ParentList.SelectedIndex = index >= 0 ? index : 0;

        if (!string.IsNullOrEmpty(okText))
        {
            OkButton.Content = okText;
        }

        if (!string.IsNullOrEmpty(cancelText))
        {
            CancelButton.Content = cancelText;
        }
    }

    /// <summary>Chosen parent/target category; null means the top level.</summary>
    public Guid? SelectedParentId => (ParentList.SelectedItem as CategoryChoice)?.Id;

    private void OnOkClicked(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}
