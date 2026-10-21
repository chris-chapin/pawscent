namespace Pawscent.Core.Expenses;

/// <summary>A file proving an expense was billed and/or paid. Several expenses may share one receipt.</summary>
public sealed record Receipt(Guid Id, string FilePath);
