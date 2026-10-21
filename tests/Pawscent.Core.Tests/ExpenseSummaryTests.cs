using Pawscent.Core.Expenses;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ExpenseSummaryTests
{
    private readonly CategoryList _categories = new();
    private readonly ReceiptList _receipts = new();
    private readonly ExpenseList _expenses;
    private readonly Category _vet;
    private readonly Category _food;
    private readonly Receipt _receipt;

    public ExpenseSummaryTests()
    {
        _expenses = new ExpenseList(_categories, _receipts);
        _vet = _categories.Create("Vet");
        _food = _categories.Create("food");
        _receipt = _receipts.Create(@"C:\Receipts\vet.pdf");
    }

    [Fact]
    public void Years_AreDistinctAndOldestFirst()
    {
        Add(new DateOnly(2026, 3, 1), 1m, "USD", _vet);
        Add(new DateOnly(2024, 5, 1), 1m, "USD", _vet);
        Add(new DateOnly(2026, 1, 1), 1m, "USD", _vet);

        Assert.Equal([2024, 2026], ExpenseSummary.Years(_expenses.Items));
    }

    [Fact]
    public void ForYear_SortsOldestFirstAndKeepsEntryOrderOnSameDate()
    {
        var late = Add(new DateOnly(2026, 12, 31), 1m, "USD", _vet);
        var sameDayFirst = Add(new DateOnly(2026, 6, 1), 2m, "USD", _vet);
        Add(new DateOnly(2025, 1, 1), 3m, "USD", _vet);
        var early = Add(new DateOnly(2026, 1, 1), 4m, "USD", _vet);
        var sameDaySecond = Add(new DateOnly(2026, 6, 1), 5m, "USD", _vet);

        Assert.Equal([early, sameDayFirst, sameDaySecond, late], ExpenseSummary.ForYear(_expenses.Items, 2026));
    }

    [Fact]
    public void ForYear_AppliesCategoryFilter()
    {
        var vet = Add(new DateOnly(2026, 2, 1), 1m, "USD", _vet);
        Add(new DateOnly(2026, 1, 1), 2m, "USD", _food);

        Assert.Equal([vet], ExpenseSummary.ForYear(_expenses.Items, 2026, id => id == _vet.Id));
    }

    [Fact]
    public void ByCategory_TotalsPerCategoryAndCurrencySortedByName()
    {
        Add(new DateOnly(2026, 1, 1), 10m, "USD", _vet);
        Add(new DateOnly(2026, 1, 2), 5.25m, "USD", _vet);
        Add(new DateOnly(2026, 1, 3), 7m, "EUR", _vet);
        Add(new DateOnly(2026, 1, 4), 3m, "USD", _food);

        var totals = ExpenseSummary.ByCategory(_expenses.Items, _categories);

        Assert.Equal(
            [
                new CategoryTotal(_food.Id, "food", "USD", 1, 3m),
                new CategoryTotal(_vet.Id, "Vet", "EUR", 1, 7m),
                new CategoryTotal(_vet.Id, "Vet", "USD", 2, 15.25m),
            ],
            totals);
    }

    [Fact]
    public void ByCurrency_TotalsEachCurrencySeparately()
    {
        Add(new DateOnly(2026, 1, 1), 10m, "USD", _vet);
        Add(new DateOnly(2026, 1, 2), 7m, "EUR", _vet);
        Add(new DateOnly(2026, 1, 3), 3m, "USD", _food);

        Assert.Equal(
            [new CurrencyTotal("EUR", 1, 7m), new CurrencyTotal("USD", 2, 13m)],
            ExpenseSummary.ByCurrency(_expenses.Items));
    }

    private Expense Add(DateOnly date, decimal amount, string currency, Category category) =>
        _expenses.Create(date, amount, currency, "Item", category.Id, _receipt.Id);
}
