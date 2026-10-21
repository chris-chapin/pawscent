namespace Pawscent.Core.Expenses;

/// <summary>
/// One expense line. <see cref="CategoryId"/> and <see cref="ReceiptId"/> are required and refer to a
/// <see cref="Category"/> and a <see cref="Receipt"/> in the same profile.
/// </summary>
public sealed record Expense(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string Description,
    Guid CategoryId,
    Guid ReceiptId);
