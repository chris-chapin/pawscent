namespace Pawscent.Core.Expenses;

/// <summary>The receipt files of a profile. File paths are unique (case-insensitive, as on Windows).</summary>
public sealed class ReceiptList
{
    private readonly List<Receipt> _items = [];
    private Func<Guid, bool> _isReferenced = _ => false;

    public ReceiptList()
    {
    }

    public ReceiptList(IEnumerable<Receipt> items)
    {
        foreach (var item in items)
        {
            var error = Validate(item.FilePath, item.Id);
            if (error is not null)
                throw new ArgumentException(error, nameof(items));
            if (Contains(item.Id))
                throw new ArgumentException($"Duplicate receipt id {item.Id}.", nameof(items));
            _items.Add(item);
        }
    }

    public IReadOnlyList<Receipt> Items => _items;

    public bool Contains(Guid id) => _items.Exists(r => r.Id == id);

    public Receipt Get(Guid id) =>
        _items.Find(r => r.Id == id) ?? throw new KeyNotFoundException($"Receipt {id} was not found.");

    public Receipt? FindByPath(string filePath) =>
        _items.Find(r => string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns null if the path is valid, otherwise a user-facing reason.</summary>
    /// <param name="excludeId">The receipt being edited, so it does not conflict with itself.</param>
    public string? Validate(string filePath, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return "File path is required.";
        if (filePath != filePath.Trim())
            return "File path cannot start or end with spaces.";
        if (filePath.Any(char.IsControl))
            return "File path contains characters that are not allowed.";
        var existing = FindByPath(filePath);
        if (existing is not null && existing.Id != excludeId)
            return "That file is already in the receipt list.";
        return null;
    }

    public Receipt Create(string filePath)
    {
        ThrowIfInvalid(filePath, excludeId: null);
        var receipt = new Receipt(Guid.NewGuid(), filePath);
        _items.Add(receipt);
        return receipt;
    }

    public Receipt Update(Guid id, string filePath)
    {
        var index = IndexOf(id);
        ThrowIfInvalid(filePath, id);
        var receipt = _items[index] with { FilePath = filePath };
        _items[index] = receipt;
        return receipt;
    }

    /// <summary>Removes a receipt. Throws if an expense still refers to it.</summary>
    public void Delete(Guid id)
    {
        var index = IndexOf(id);
        if (_isReferenced(id))
            throw new InvalidOperationException("The receipt is used by one or more expenses.");
        _items.RemoveAt(index);
    }

    // Set by ExpenseList so receipts in use cannot be deleted.
    internal void SetReferenceCheck(Func<Guid, bool> isReferenced) => _isReferenced = isReferenced;

    private int IndexOf(Guid id)
    {
        var index = _items.FindIndex(r => r.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Receipt {id} was not found.");
    }

    private void ThrowIfInvalid(string filePath, Guid? excludeId)
    {
        var error = Validate(filePath, excludeId);
        if (error is not null)
            throw new ArgumentException(error, nameof(filePath));
    }
}
