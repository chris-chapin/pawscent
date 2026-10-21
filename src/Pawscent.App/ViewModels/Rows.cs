using Pawscent.Core.Expenses;

namespace Pawscent.App.ViewModels;

/// <summary>One line of a year's expense table.</summary>
public sealed class ExpenseRow(Expense expense, string categoryName, string receiptName)
{
    public Expense Expense => expense;

    public string DateText => expense.Date.ToString("d");

    public string Description => expense.Description;

    public string CategoryName => categoryName;

    public string ReceiptName => receiptName;

    public string AmountText => expense.Amount.ToString("N2");

    public string Currency => expense.Currency;
}

/// <summary>One line of a year's per-category summary table.</summary>
public sealed class CategoryTotalRow(CategoryTotal total)
{
    public string CategoryName => total.CategoryName;

    public string Currency => total.Currency;

    public string CountText => total.Count.ToString();

    public string TotalText => total.Total.ToString("N2");
}

/// <summary>One line of the categories table.</summary>
public sealed class CategoryRow(Category category, int expenseCount)
{
    public Category Category => category;

    public string Name => category.Name;

    public int ExpenseCount => expenseCount;

    public string ExpenseCountText => expenseCount.ToString();
}

/// <summary>A receipt shown by file name only.</summary>
public sealed class ReceiptRow(Receipt receipt, int expenseCount)
{
    public Receipt Receipt => receipt;

    public string FileName => Path.GetFileName(receipt.FilePath);

    public int ExpenseCount => expenseCount;

    public string ExpenseCountText => expenseCount.ToString();
}
