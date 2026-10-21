using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pawscent.App.Services;
using Pawscent.Core.Profiles;

namespace Pawscent.App.ViewModels;

/// <summary>The categories tab: a list of the profile's categories with add, rename and delete.</summary>
public sealed partial class CategoriesViewModel : ObservableObject
{
    private readonly ProfileDocument _document;
    private readonly IDialogService _dialogs;

    public CategoriesViewModel(ProfileDocument document, IDialogService dialogs)
    {
        _document = document;
        _dialogs = dialogs;
        Refresh();
    }

    public ObservableCollection<CategoryRow> Items { get; } = [];

    public CategoryRow? SelectedItem
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RenameCommand.NotifyCanExecuteChanged();
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void Refresh()
    {
        var selectedId = SelectedItem?.Category.Id;
        Items.Clear();
        foreach (var category in _document.Categories.Items.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
            Items.Add(new CategoryRow(category, _document.Expenses.ForCategory(category.Id).Count));
        SelectedItem = Items.FirstOrDefault(r => r.Category.Id == selectedId);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var name = await _dialogs.PromptTextAsync(
            "New category", "Name", string.Empty, "Add", n => _document.Categories.Validate(n));
        if (name is null)
            return;
        var created = _document.Categories.Create(name);
        SelectedItem = Items.FirstOrDefault(r => r.Category.Id == created.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RenameAsync()
    {
        if (SelectedItem is not { } row)
            return;
        var name = await _dialogs.PromptTextAsync(
            "Rename category", "Name", row.Name, "Rename", n => _document.Categories.Validate(n, row.Category.Id));
        if (name is not null && _document.Categories.Contains(row.Category.Id))
            _document.Categories.Update(row.Category.Id, name);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedItem is not { } row)
            return;
        if (row.ExpenseCount > 0)
        {
            await _dialogs.ShowMessageAsync(
                "Category in use",
                $"\"{row.Name}\" is used by {row.ExpenseCount} expense(s). Move them to another category first.");
            return;
        }
        if (await _dialogs.ConfirmAsync("Delete category", $"Delete \"{row.Name}\"?", "Delete")
            && _document.Categories.Contains(row.Category.Id))
        {
            _document.Categories.Delete(row.Category.Id);
        }
    }

    private bool HasSelection() => SelectedItem is not null;
}
