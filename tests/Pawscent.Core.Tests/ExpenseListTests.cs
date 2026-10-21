using Pawscent.Core.Expenses;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ExpenseListTests
{
    private static readonly DateOnly Date = new(2026, 10, 21);

    private readonly CategoryList _categories = new();
    private readonly ReceiptList _receipts = new();
    private readonly ExpenseList _expenses;
    private readonly Category _category;
    private readonly Receipt _receipt;

    public ExpenseListTests()
    {
        _expenses = new ExpenseList(_categories, _receipts);
        _category = _categories.Create("Vet");
        _receipt = _receipts.Create(@"C:\Receipts\vet.pdf");
    }

    [Fact]
    public void Create_AddsExpenseReferencingExistingCategoryAndReceipt()
    {
        var expense = _expenses.Create(Date, 42.50m, "usd", " Checkup ", _category.Id, _receipt.Id);

        Assert.NotEqual(Guid.Empty, expense.Id);
        Assert.Equal(Date, expense.Date);
        Assert.Equal(42.50m, expense.Amount);
        Assert.Equal("USD", expense.Currency);
        Assert.Equal("Checkup", expense.Description);
        Assert.Equal(_category.Id, expense.CategoryId);
        Assert.Equal(_receipt.Id, expense.ReceiptId);
        Assert.Equal([expense], _expenses.Items);
        Assert.Same(expense, _expenses.Get(expense.Id));
    }

    [Fact]
    public void Create_MultipleExpensesCanShareReceiptAndCategory()
    {
        var food = _expenses.Create(Date, 20m, "USD", "Food", _category.Id, _receipt.Id);
        var toys = _expenses.Create(Date, 15m, "USD", "Toys", _category.Id, _receipt.Id);

        Assert.Equal([food, toys], _expenses.ForReceipt(_receipt.Id));
        Assert.Equal([food, toys], _expenses.ForCategory(_category.Id));
        Assert.Single(_receipts.Items);
    }

    [Fact]
    public void CreateWithNewReceipt_AddsBoth()
    {
        var expense = _expenses.CreateWithNewReceipt(Date, 9.99m, "EUR", "Grooming", _category.Id, @"C:\Receipts\groom.jpg");

        var receipt = _receipts.Get(expense.ReceiptId);
        Assert.Equal(@"C:\Receipts\groom.jpg", receipt.FilePath);
        Assert.Equal(2, _receipts.Items.Count);
        Assert.Single(_expenses.Items);
    }

    [Fact]
    public void CreateWithNewReceipt_InvalidExpense_DoesNotAddReceipt()
    {
        Assert.Throws<ArgumentException>(() =>
            _expenses.CreateWithNewReceipt(Date, 1m, "dollars", "Food", _category.Id, "new.pdf"));

        Assert.Null(_receipts.FindByPath("new.pdf"));
        Assert.Empty(_expenses.Items);
    }

    [Fact]
    public void CreateWithNewReceipt_UnknownCategory_DoesNotAddReceipt()
    {
        Assert.Throws<ArgumentException>(() =>
            _expenses.CreateWithNewReceipt(Date, 1m, "USD", "Food", Guid.NewGuid(), "new.pdf"));

        Assert.Null(_receipts.FindByPath("new.pdf"));
        Assert.Empty(_expenses.Items);
    }

    [Fact]
    public void CreateWithNewReceipt_ExistingPath_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _expenses.CreateWithNewReceipt(Date, 1m, "USD", "Food", _category.Id, _receipt.FilePath));

        Assert.Empty(_expenses.Items);
    }

    [Theory]
    [InlineData("", "Food")]
    [InlineData("US", "Food")]
    [InlineData("USDD", "Food")]
    [InlineData("U$D", "Food")]
    [InlineData("USD", "")]
    [InlineData("USD", "   ")]
    public void Create_RejectsInvalidFields(string currency, string description)
    {
        Assert.Throws<ArgumentException>(() => _expenses.Create(Date, 1m, currency, description, _category.Id, _receipt.Id));
        Assert.Empty(_expenses.Items);
    }

    [Fact]
    public void Create_RejectsUnknownCategory() =>
        Assert.Throws<ArgumentException>(() => _expenses.Create(Date, 1m, "USD", "Food", Guid.NewGuid(), _receipt.Id));

    [Fact]
    public void Create_RejectsUnknownReceipt() =>
        Assert.Throws<ArgumentException>(() => _expenses.Create(Date, 1m, "USD", "Food", _category.Id, Guid.NewGuid()));

    [Fact]
    public void Update_ReplacesFieldsAndKeepsIdAndOrder()
    {
        var first = _expenses.Create(Date, 1m, "USD", "Food", _category.Id, _receipt.Id);
        var second = _expenses.Create(Date, 2m, "USD", "Toys", _category.Id, _receipt.Id);
        var otherCategory = _categories.Create("Treats");
        var otherReceipt = _receipts.Create("other.pdf");
        var newDate = Date.AddDays(-3);

        var updated = _expenses.Update(first.Id, newDate, 3.25m, "cad", "Treats", otherCategory.Id, otherReceipt.Id);

        Assert.Equal(new Expense(first.Id, newDate, 3.25m, "CAD", "Treats", otherCategory.Id, otherReceipt.Id), updated);
        Assert.Equal([updated, second], _expenses.Items);
    }

    [Fact]
    public void Update_Invalid_LeavesExpenseUnchanged()
    {
        var expense = _expenses.Create(Date, 1m, "USD", "Food", _category.Id, _receipt.Id);

        Assert.Throws<ArgumentException>(() =>
            _expenses.Update(expense.Id, Date, 5m, "USD", "Food", _category.Id, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() =>
            _expenses.Update(expense.Id, Date, 5m, "USD", "Food", Guid.NewGuid(), _receipt.Id));
        Assert.Equal(expense, _expenses.Get(expense.Id));
    }

    [Fact]
    public void Update_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() =>
            _expenses.Update(Guid.NewGuid(), Date, 1m, "USD", "Food", _category.Id, _receipt.Id));

    [Fact]
    public void Delete_RemovesExpenseAndKeepsCategoryAndReceipt()
    {
        var expense = _expenses.Create(Date, 1m, "USD", "Food", _category.Id, _receipt.Id);

        _expenses.Delete(expense.Id);

        Assert.Empty(_expenses.Items);
        Assert.True(_categories.Contains(_category.Id));
        Assert.True(_receipts.Contains(_receipt.Id));
    }

    [Fact]
    public void Delete_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _expenses.Delete(Guid.NewGuid()));

    [Fact]
    public void DeleteReceipt_InUse_Throws()
    {
        var expense = _expenses.Create(Date, 1m, "USD", "Food", _category.Id, _receipt.Id);

        Assert.Throws<InvalidOperationException>(() => _receipts.Delete(_receipt.Id));
        Assert.True(_receipts.Contains(_receipt.Id));

        _expenses.Delete(expense.Id);
        _receipts.Delete(_receipt.Id);
        Assert.Empty(_receipts.Items);
    }

    [Fact]
    public void DeleteCategory_InUse_Throws()
    {
        var expense = _expenses.Create(Date, 1m, "USD", "Food", _category.Id, _receipt.Id);

        Assert.Throws<InvalidOperationException>(() => _categories.Delete(_category.Id));
        Assert.True(_categories.Contains(_category.Id));

        _expenses.Delete(expense.Id);
        _categories.Delete(_category.Id);
        Assert.Empty(_categories.Items);
    }

    [Fact]
    public void Constructor_LoadsExistingItems()
    {
        var items = new[] { new Expense(Guid.NewGuid(), Date, 1m, "USD", "Food", _category.Id, _receipt.Id) };

        var expenses = new ExpenseList(_categories, _receipts, items);

        Assert.Equal(items, expenses.Items);
        Assert.Throws<InvalidOperationException>(() => _categories.Delete(_category.Id));
        Assert.Throws<InvalidOperationException>(() => _receipts.Delete(_receipt.Id));
    }

    [Fact]
    public void Constructor_RejectsUnknownReferencesAndDuplicateIds()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            new ExpenseList(_categories, _receipts, [new Expense(id, Date, 1m, "USD", "Food", _category.Id, Guid.NewGuid())]));
        Assert.Throws<ArgumentException>(() =>
            new ExpenseList(_categories, _receipts, [new Expense(id, Date, 1m, "USD", "Food", Guid.NewGuid(), _receipt.Id)]));
        Assert.Throws<ArgumentException>(() => new ExpenseList(_categories, _receipts,
        [
            new Expense(id, Date, 1m, "USD", "Food", _category.Id, _receipt.Id),
            new Expense(id, Date, 2m, "USD", "Toys", _category.Id, _receipt.Id),
        ]));
    }
}
