using Pawscent.Core.Expenses;

namespace Pawscent.Core.Profiles;

/// <summary>
/// The JSON shape of profile.json: header fields, then categories, receipts and expenses. Files written before the
/// lists existed have no list properties; those load as empty.
/// </summary>
internal sealed record ProfileFile(
    string Name,
    DateTimeOffset CreatedUtc,
    int SchemaVersion,
    IReadOnlyList<Category?>? Categories,
    IReadOnlyList<Receipt?>? Receipts,
    IReadOnlyList<Expense?>? Expenses);
