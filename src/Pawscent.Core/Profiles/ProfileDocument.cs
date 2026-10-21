using Pawscent.Core.Expenses;

namespace Pawscent.Core.Profiles;

/// <summary>Everything stored in a profile's profile.json: the header plus its categories, receipts and expenses.</summary>
public sealed class ProfileDocument
{
    public ProfileDocument(Profile profile)
        : this(profile, new CategoryList(), new ReceiptList(), [])
    {
    }

    public ProfileDocument(Profile profile, CategoryList categories, ReceiptList receipts, IEnumerable<Expense> expenses)
    {
        Profile = profile;
        Expenses = new ExpenseList(categories, receipts, expenses);
        categories.Changed += OnListChanged;
        receipts.Changed += OnListChanged;
        Expenses.Changed += OnListChanged;
    }

    public Profile Profile { get; }

    public CategoryList Categories => Expenses.Categories;

    public ReceiptList Receipts => Expenses.Receipts;

    public ExpenseList Expenses { get; }

    /// <summary>Raised after any category, receipt or expense is created, updated or deleted.</summary>
    public event EventHandler? Changed;

    private void OnListChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
