namespace Pawscent.Core.Expenses;

/// <summary>
/// The expenses of a profile. Every expense must refer to a category in <see cref="Categories"/> and a
/// receipt in <see cref="Receipts"/>.
/// </summary>
public sealed class ExpenseList
{
    public const int CurrencyCodeLength = 3;

    private readonly List<Expense> _items = [];

    public ExpenseList(CategoryList categories, ReceiptList receipts)
        : this(categories, receipts, [])
    {
    }

    public ExpenseList(CategoryList categories, ReceiptList receipts, IEnumerable<Expense> items)
    {
        Categories = categories;
        Receipts = receipts;
        foreach (var item in items)
        {
            var error = Validate(item.Currency, item.Description, item.CategoryId, item.ReceiptId);
            if (error is not null)
                throw new ArgumentException(error, nameof(items));
            if (Contains(item.Id))
                throw new ArgumentException($"Duplicate expense id {item.Id}.", nameof(items));
            _items.Add(item);
        }
        categories.SetReferenceCheck(IsCategoryReferenced);
        receipts.SetReferenceCheck(IsReceiptReferenced);
    }

    public CategoryList Categories { get; }

    public ReceiptList Receipts { get; }

    public IReadOnlyList<Expense> Items => _items;

    public bool Contains(Guid id) => _items.Exists(e => e.Id == id);

    public Expense Get(Guid id) =>
        _items.Find(e => e.Id == id) ?? throw new KeyNotFoundException($"Expense {id} was not found.");

    public bool IsCategoryReferenced(Guid categoryId) => _items.Exists(e => e.CategoryId == categoryId);

    public IReadOnlyList<Expense> ForCategory(Guid categoryId) => _items.FindAll(e => e.CategoryId == categoryId);

    public bool IsReceiptReferenced(Guid receiptId) => _items.Exists(e => e.ReceiptId == receiptId);

    public IReadOnlyList<Expense> ForReceipt(Guid receiptId) => _items.FindAll(e => e.ReceiptId == receiptId);

    /// <summary>Returns null if the values are valid, otherwise a user-facing reason.</summary>
    public string? Validate(string currency, string description, Guid categoryId, Guid receiptId) =>
        ValidateFields(currency, description, categoryId)
        ?? (Receipts.Contains(receiptId) ? null : "Choose a receipt for this expense.");

    public Expense Create(DateOnly date, decimal amount, string currency, string description, Guid categoryId, Guid receiptId)
    {
        ThrowIfInvalid(currency, description, categoryId, receiptId);
        var expense = new Expense(
            Guid.NewGuid(), date, amount, currency.ToUpperInvariant(), description.Trim(), categoryId, receiptId);
        _items.Add(expense);
        return expense;
    }

    /// <summary>Adds a new receipt for <paramref name="receiptFilePath"/> and an expense that refers to it.</summary>
    public Expense CreateWithNewReceipt(
        DateOnly date, decimal amount, string currency, string description, Guid categoryId, string receiptFilePath)
    {
        // Validate everything before changing either list so a bad expense doesn't leave an orphan receipt.
        var error = ValidateFields(currency, description, categoryId) ?? Receipts.Validate(receiptFilePath);
        if (error is not null)
            throw new ArgumentException(error);
        var receipt = Receipts.Create(receiptFilePath);
        return Create(date, amount, currency, description, categoryId, receipt.Id);
    }

    public Expense Update(
        Guid id, DateOnly date, decimal amount, string currency, string description, Guid categoryId, Guid receiptId)
    {
        var index = IndexOf(id);
        ThrowIfInvalid(currency, description, categoryId, receiptId);
        var expense = new Expense(id, date, amount, currency.ToUpperInvariant(), description.Trim(), categoryId, receiptId);
        _items[index] = expense;
        return expense;
    }

    public void Delete(Guid id) => _items.RemoveAt(IndexOf(id));

    private string? ValidateFields(string currency, string description, Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return "Currency is required.";
        if (currency.Length != CurrencyCodeLength || !currency.All(char.IsAsciiLetter))
            return "Currency must be a 3-letter code such as USD.";
        if (string.IsNullOrWhiteSpace(description))
            return "Description is required.";
        if (!Categories.Contains(categoryId))
            return "Choose a category for this expense.";
        return null;
    }

    private int IndexOf(Guid id)
    {
        var index = _items.FindIndex(e => e.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Expense {id} was not found.");
    }

    private void ThrowIfInvalid(string currency, string description, Guid categoryId, Guid receiptId)
    {
        var error = Validate(currency, description, categoryId, receiptId);
        if (error is not null)
            throw new ArgumentException(error);
    }
}
