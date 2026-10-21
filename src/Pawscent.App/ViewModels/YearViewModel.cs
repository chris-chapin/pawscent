using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;

namespace Pawscent.App.ViewModels;

/// <summary>One year tab: that year's expenses, oldest first, and their totals per category.</summary>
public sealed partial class YearViewModel : ObservableObject
{
    private readonly ProfileDocument _document;
    private readonly ProfileViewModel _owner;
    private bool _updatingFilters;

    public YearViewModel(ProfileDocument document, ProfileViewModel owner, int year)
    {
        _document = document;
        _owner = owner;
        Year = year;
        Refresh();
    }

    public int Year { get; }

    public string Header => Year.ToString();

    public ObservableCollection<ExpenseRow> Expenses { get; } = [];

    public ObservableCollection<CategoryTotalRow> CategoryTotals { get; } = [];

    public ObservableCollection<CategoryFilterItem> CategoryFilters { get; } = [];

    public string TotalsText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public string FilterText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public ExpenseRow? SelectedExpense
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                EditExpenseCommand.NotifyCanExecuteChanged();
                DeleteExpenseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Rebuilds the filters and tables from the profile, keeping the selection and filter choices.</summary>
    public void Refresh()
    {
        SyncFilters();
        RebuildTables();
    }

    [RelayCommand]
    private Task AddExpenseAsync() => _owner.AddExpenseAsync(Year);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task EditExpenseAsync() =>
        SelectedExpense is null ? Task.CompletedTask : _owner.EditExpenseAsync(SelectedExpense.Expense);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task DeleteExpenseAsync() =>
        SelectedExpense is null ? Task.CompletedTask : _owner.DeleteExpenseAsync(SelectedExpense.Expense);

    [RelayCommand]
    private void SelectAllCategories() => SetAllFilters(true);

    [RelayCommand]
    private void ClearCategories() => SetAllFilters(false);

    private bool HasSelection() => SelectedExpense is not null;

    private void SetAllFilters(bool isSelected)
    {
        _updatingFilters = true;
        foreach (var filter in CategoryFilters)
            filter.IsSelected = isSelected;
        _updatingFilters = false;
        RebuildTables();
    }

    // New categories start checked; deleted ones drop out; renamed ones keep their checked state.
    private void SyncFilters()
    {
        var selected = CategoryFilters.ToDictionary(f => f.CategoryId, f => f.IsSelected);
        foreach (var filter in CategoryFilters)
            filter.PropertyChanged -= Filter_PropertyChanged;
        CategoryFilters.Clear();
        foreach (var category in _document.Categories.Items.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var filter = new CategoryFilterItem(category.Id, category.Name, selected.GetValueOrDefault(category.Id, true));
            filter.PropertyChanged += Filter_PropertyChanged;
            CategoryFilters.Add(filter);
        }
    }

    private void Filter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_updatingFilters && e.PropertyName == nameof(CategoryFilterItem.IsSelected))
            RebuildTables();
    }

    private void RebuildTables()
    {
        var included = CategoryFilters.Where(f => f.IsSelected).Select(f => f.CategoryId).ToHashSet();
        var expenses = ExpenseSummary.ForYear(_document.Expenses.Items, Year, included.Contains);
        var selectedId = SelectedExpense?.Expense.Id;

        Expenses.Clear();
        foreach (var expense in expenses)
            Expenses.Add(_owner.CreateRow(expense));
        SelectedExpense = Expenses.FirstOrDefault(r => r.Expense.Id == selectedId);

        CategoryTotals.Clear();
        foreach (var total in ExpenseSummary.ByCategory(expenses, _document.Categories))
            CategoryTotals.Add(new CategoryTotalRow(total));

        var totals = ExpenseSummary.ByCurrency(expenses);
        TotalsText = totals.Count == 0
            ? "No expenses shown."
            : $"{expenses.Count} expense(s), total " + string.Join(" + ", totals.Select(t => $"{t.Total:N2} {t.Currency}"));

        FilterText = included.Count == CategoryFilters.Count
            ? "All categories"
            : $"{included.Count} of {CategoryFilters.Count} categories";
    }
}
