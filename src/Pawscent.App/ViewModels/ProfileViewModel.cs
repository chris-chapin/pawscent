using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pawscent.App.Services;
using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;

namespace Pawscent.App.ViewModels;

/// <summary>The open profile: one tab per year, plus the categories and receipts tabs.</summary>
public sealed partial class ProfileViewModel : ObservableObject, IDisposable
{
    public const int MinYear = 1900;
    public const int MaxYear = 2999;

    private readonly ProfileDocument _document;
    private readonly IDialogService _dialogs;
    private readonly ReceiptFiles _receiptFiles;
    private readonly TimeProvider _timeProvider;

    public ProfileViewModel(
        ProfileDocument document, IDialogService dialogs, ReceiptFiles receiptFiles, TimeProvider timeProvider)
    {
        _document = document;
        _dialogs = dialogs;
        _receiptFiles = receiptFiles;
        _timeProvider = timeProvider;
        Categories = new CategoriesViewModel(document, dialogs);
        Receipts = new ReceiptsViewModel(document, dialogs, receiptFiles);

        // The current year always has a tab so there is somewhere to add the first expense.
        foreach (var year in ExpenseSummary.Years(document.Expenses.Items).Append(Today.Year).Distinct().Order())
            Years.Add(new YearViewModel(document, this, year));

        _document.Changed += Document_Changed;
    }

    /// <summary>Year tabs, oldest first. Tabs are only added while the profile is open, never removed.</summary>
    public ObservableCollection<YearViewModel> Years { get; } = [];

    public CategoriesViewModel Categories { get; }

    public ReceiptsViewModel Receipts { get; }

    public YearViewModel CurrentYear => Years.First(y => y.Year == Today.Year);

    /// <summary>Raised when a year tab should be brought to the front.</summary>
    public event EventHandler<YearViewModel>? YearRequested;

    private DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    public void Dispose() => _document.Changed -= Document_Changed;

    internal ExpenseRow CreateRow(Expense expense) =>
        new(
            expense,
            _document.Categories.Contains(expense.CategoryId) ? _document.Categories.Get(expense.CategoryId).Name : string.Empty,
            _document.Receipts.Contains(expense.ReceiptId)
                ? Path.GetFileName(_document.Receipts.Get(expense.ReceiptId).FilePath)
                : string.Empty);

    internal async Task AddExpenseAsync(int year)
    {
        var date = year == Today.Year ? Today : new DateOnly(year, 1, 1);
        var editor = new ExpenseEditorViewModel(_document, _dialogs, _receiptFiles, null, date, DefaultCurrency());
        await _dialogs.EditExpenseAsync(editor);
    }

    internal async Task EditExpenseAsync(Expense expense)
    {
        var editor = new ExpenseEditorViewModel(_document, _dialogs, _receiptFiles, expense, expense.Date, expense.Currency);
        await _dialogs.EditExpenseAsync(editor);
    }

    internal async Task DeleteExpenseAsync(Expense expense)
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Delete expense",
            $"Delete \"{expense.Description}\" ({expense.Amount:N2} {expense.Currency}) from {expense.Date:d}?",
            "Delete");
        if (confirmed && _document.Expenses.Contains(expense.Id))
            _document.Expenses.Delete(expense.Id);
    }

    [RelayCommand]
    private async Task AddYearAsync()
    {
        var text = await _dialogs.PromptTextAsync("Add year", "Year", string.Empty, "Add", ValidateNewYear);
        if (text is null)
            return;
        YearRequested?.Invoke(this, EnsureYear(int.Parse(text, CultureInfo.CurrentCulture)));
    }

    private string? ValidateNewYear(string text)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.CurrentCulture, out var year) || year < MinYear || year > MaxYear)
            return $"Enter a year from {MinYear} to {MaxYear}.";
        if (Years.Any(y => y.Year == year))
            return "That year already has a tab.";
        return null;
    }

    private YearViewModel EnsureYear(int year)
    {
        var existing = Years.FirstOrDefault(y => y.Year == year);
        if (existing is not null)
            return existing;
        var created = new YearViewModel(_document, this, year);
        var index = 0;
        while (index < Years.Count && Years[index].Year < year)
            index++;
        Years.Insert(index, created);
        return created;
    }

    // The most recent expense's currency, else the currency of the user's region.
    private string DefaultCurrency()
    {
        var latest = _document.Expenses.Items.OrderBy(e => e.Date).LastOrDefault();
        if (latest is not null)
            return latest.Currency;
        try
        {
            var code = RegionInfo.CurrentRegion.ISOCurrencySymbol;
            return code.Length == ExpenseList.CurrencyCodeLength ? code : "USD";
        }
        catch (ArgumentException)
        {
            return "USD";
        }
    }

    private void Document_Changed(object? sender, EventArgs e)
    {
        foreach (var year in ExpenseSummary.Years(_document.Expenses.Items))
            EnsureYear(year);
        foreach (var year in Years)
            year.Refresh();
        Categories.Refresh();
        Receipts.Refresh();
    }
}
