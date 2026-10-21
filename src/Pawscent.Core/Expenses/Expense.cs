namespace Pawscent.Core.Expenses;

/// <summary>One expense line. <see cref="ReceiptId"/> refers to a <see cref="Receipt"/> in the same profile.</summary>
public sealed record Expense(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string Description,
    Guid ReceiptId);
