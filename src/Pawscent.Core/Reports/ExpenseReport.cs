using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;

namespace Pawscent.Core.Reports;

/// <summary>One expense of a report, with the category name and receipt the summary prints for it.</summary>
public sealed record ReportLine(Expense Expense, string CategoryName, Receipt Receipt);

/// <summary>
/// What a year tab shows, copied so the report can be built in the background while the profile keeps changing: the
/// year's expenses that pass the category filter, oldest first, and their totals.
/// </summary>
public sealed record ExpenseReport(
    Profile Profile,
    int Year,
    IReadOnlyList<string>? CategoryFilter,
    IReadOnlyList<ReportLine> Lines,
    IReadOnlyList<CategoryTotal> CategoryTotals,
    IReadOnlyList<CurrencyTotal> CurrencyTotals)
{
    /// <summary>The receipts of <see cref="Lines"/>, each once, in the order they are first used.</summary>
    public IReadOnlyList<Receipt> Receipts => Lines.Select(l => l.Receipt).DistinctBy(r => r.Id).ToList();

    /// <param name="includedCategories">The categories shown, or null to show all.</param>
    public static ExpenseReport ForYear(ProfileDocument document, int year, IReadOnlySet<Guid>? includedCategories = null)
    {
        var categories = document.Categories;
        var expenses = ExpenseSummary.ForYear(document.Expenses.Items, year, includedCategories is null ? null : includedCategories.Contains);
        var lines = expenses
            .Select(e => new ReportLine(
                e,
                categories.Contains(e.CategoryId) ? categories.Get(e.CategoryId).Name : string.Empty,
                document.Receipts.Get(e.ReceiptId)))
            .ToList();

        // Named only when something is filtered out, so an unfiltered report doesn't list every category.
        var filter = includedCategories is null || categories.Items.All(c => includedCategories.Contains(c.Id))
            ? null
            : categories.Items
                .Where(c => includedCategories.Contains(c.Id))
                .Select(c => c.Name)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        return new ExpenseReport(
            document.Profile,
            year,
            filter,
            lines,
            ExpenseSummary.ByCategory(expenses, categories),
            ExpenseSummary.ByCurrency(expenses));
    }
}
