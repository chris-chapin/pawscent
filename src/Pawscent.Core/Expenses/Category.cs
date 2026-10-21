namespace Pawscent.Core.Expenses;

/// <summary>A user-defined expense category. Every expense belongs to exactly one.</summary>
public sealed record Category(Guid Id, string Name);
