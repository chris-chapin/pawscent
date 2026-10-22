using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;
using Pawscent.Core.Reports;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ExpenseReportTests
{
    private readonly ProfileDocument _document = new(new Profile("Home", DateTimeOffset.UnixEpoch, Profile.CurrentSchemaVersion));
    private readonly Category _vet;
    private readonly Category _food;
    private readonly Receipt _vetBill;
    private readonly Receipt _foodBill;

    public ExpenseReportTests()
    {
        _vet = _document.Categories.Create("Vet");
        _food = _document.Categories.Create("Food");
        _vetBill = _document.Receipts.Create(@"C:\Receipts\vet.pdf");
        _foodBill = _document.Receipts.Create(@"C:\Receipts\food.png");
    }

    [Fact]
    public void ForYear_KeepsTheYearsExpensesInIncludedCategoriesOldestFirst()
    {
        var late = Add(new DateOnly(2026, 9, 1), 30m, "USD", _vet, _vetBill);
        Add(new DateOnly(2026, 2, 1), 5m, "USD", _food, _foodBill);
        var early = Add(new DateOnly(2026, 1, 1), 10m, "USD", _vet, _vetBill);
        Add(new DateOnly(2025, 1, 1), 99m, "USD", _vet, _vetBill);

        var report = ExpenseReport.ForYear(_document, 2026, new HashSet<Guid> { _vet.Id });

        Assert.Equal([early, late], report.Lines.Select(l => l.Expense));
        Assert.Equal("Vet", report.Lines[0].CategoryName);
        Assert.Equal(_vetBill, report.Lines[0].Receipt);
        Assert.Equal([new CategoryTotal(_vet.Id, "Vet", "USD", 2, 40m)], report.CategoryTotals);
        Assert.Equal([new CurrencyTotal("USD", 2, 40m)], report.CurrencyTotals);
        Assert.Equal(2026, report.Year);
        Assert.Same(_document.Profile, report.Profile);
    }

    [Fact]
    public void ForYear_NamesTheFilterOnlyWhenACategoryIsLeftOut()
    {
        var toys = _document.Categories.Create("toys");

        Assert.Null(ExpenseReport.ForYear(_document, 2026).CategoryFilter);
        Assert.Null(ExpenseReport.ForYear(_document, 2026, new HashSet<Guid> { _vet.Id, _food.Id, toys.Id }).CategoryFilter);
        Assert.Equal(
            ["toys", "Vet"],
            ExpenseReport.ForYear(_document, 2026, new HashSet<Guid> { _vet.Id, toys.Id }).CategoryFilter);
        Assert.Equal([], ExpenseReport.ForYear(_document, 2026, new HashSet<Guid>()).CategoryFilter);
    }

    [Fact]
    public void Receipts_ListsEachReceiptOnceInOrderOfFirstUse()
    {
        Add(new DateOnly(2026, 1, 1), 1m, "USD", _food, _foodBill);
        Add(new DateOnly(2026, 2, 1), 2m, "USD", _vet, _vetBill);
        Add(new DateOnly(2026, 3, 1), 3m, "EUR", _food, _foodBill);

        var report = ExpenseReport.ForYear(_document, 2026);

        Assert.Equal([_foodBill, _vetBill], report.Receipts);
    }

    private Expense Add(DateOnly date, decimal amount, string currency, Category category, Receipt receipt) =>
        _document.Expenses.Create(date, amount, currency, "Item", category.Id, receipt.Id);
}
