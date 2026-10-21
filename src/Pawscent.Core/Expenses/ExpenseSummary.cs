namespace Pawscent.Core.Expenses;

/// <summary>Total of the expenses in one category and currency. Amounts in different currencies are never added.</summary>
public sealed record CategoryTotal(Guid CategoryId, string CategoryName, string Currency, int Count, decimal Total);

/// <summary>Total of the expenses in one currency.</summary>
public sealed record CurrencyTotal(string Currency, int Count, decimal Total);

/// <summary>Read-only views over a profile's expenses for the per-year screens.</summary>
public static class ExpenseSummary
{
    /// <summary>The distinct years that have at least one expense, oldest first.</summary>
    public static IReadOnlyList<int> Years(IEnumerable<Expense> expenses) =>
        expenses.Select(e => e.Date.Year).Distinct().Order().ToList();

    /// <summary>
    /// The expenses dated in <paramref name="year"/> whose category passes <paramref name="includeCategory"/>, oldest
    /// first. Expenses on the same date keep the order they were entered.
    /// </summary>
    public static IReadOnlyList<Expense> ForYear(
        IEnumerable<Expense> expenses, int year, Func<Guid, bool>? includeCategory = null) =>
        expenses
            .Where(e => e.Date.Year == year && (includeCategory is null || includeCategory(e.CategoryId)))
            .OrderBy(e => e.Date)
            .ToList();

    /// <summary>One row per category and currency, ordered by category name and then currency.</summary>
    public static IReadOnlyList<CategoryTotal> ByCategory(IEnumerable<Expense> expenses, CategoryList categories) =>
        expenses
            .GroupBy(e => (e.CategoryId, e.Currency))
            .Select(g => new CategoryTotal(
                g.Key.CategoryId,
                categories.Contains(g.Key.CategoryId) ? categories.Get(g.Key.CategoryId).Name : string.Empty,
                g.Key.Currency,
                g.Count(),
                g.Sum(e => e.Amount)))
            .OrderBy(t => t.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Currency, StringComparer.Ordinal)
            .ToList();

    /// <summary>One row per currency, ordered by currency code.</summary>
    public static IReadOnlyList<CurrencyTotal> ByCurrency(IEnumerable<Expense> expenses) =>
        expenses
            .GroupBy(e => e.Currency)
            .Select(g => new CurrencyTotal(g.Key, g.Count(), g.Sum(e => e.Amount)))
            .OrderBy(t => t.Currency, StringComparer.Ordinal)
            .ToList();
}
