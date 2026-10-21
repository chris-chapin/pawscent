namespace Pawscent.Core.Expenses;

/// <summary>The expense categories of a profile. Names are unique (case-insensitive).</summary>
public sealed class CategoryList
{
    public const int MaxNameLength = 64;

    private readonly List<Category> _items = [];
    private Func<Guid, bool> _isReferenced = _ => false;

    public CategoryList()
    {
    }

    public CategoryList(IEnumerable<Category> items)
    {
        foreach (var item in items)
        {
            var error = Validate(item.Name, item.Id);
            if (error is not null)
                throw new ArgumentException(error, nameof(items));
            if (Contains(item.Id))
                throw new ArgumentException($"Duplicate category id {item.Id}.", nameof(items));
            _items.Add(item);
        }
    }

    public IReadOnlyList<Category> Items => _items;

    public bool Contains(Guid id) => _items.Exists(c => c.Id == id);

    public Category Get(Guid id) =>
        _items.Find(c => c.Id == id) ?? throw new KeyNotFoundException($"Category {id} was not found.");

    public Category? FindByName(string name) =>
        _items.Find(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns null if the name is valid, otherwise a user-facing reason.</summary>
    /// <param name="excludeId">The category being edited, so it does not conflict with itself.</param>
    public string? Validate(string name, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";
        if (name != name.Trim())
            return "Name cannot start or end with spaces.";
        if (name.Length > MaxNameLength)
            return $"Name must be {MaxNameLength} characters or fewer.";
        if (name.Any(char.IsControl))
            return "Name contains characters that are not allowed.";
        var existing = FindByName(name);
        if (existing is not null && existing.Id != excludeId)
            return "A category with that name already exists.";
        return null;
    }

    public Category Create(string name)
    {
        ThrowIfInvalid(name, excludeId: null);
        var category = new Category(Guid.NewGuid(), name);
        _items.Add(category);
        return category;
    }

    public Category Update(Guid id, string name)
    {
        var index = IndexOf(id);
        ThrowIfInvalid(name, id);
        var category = _items[index] with { Name = name };
        _items[index] = category;
        return category;
    }

    /// <summary>Removes a category. Throws if an expense still uses it.</summary>
    public void Delete(Guid id)
    {
        var index = IndexOf(id);
        if (_isReferenced(id))
            throw new InvalidOperationException("The category is used by one or more expenses.");
        _items.RemoveAt(index);
    }

    // Set by ExpenseList so categories in use cannot be deleted.
    internal void SetReferenceCheck(Func<Guid, bool> isReferenced) => _isReferenced = isReferenced;

    private int IndexOf(Guid id)
    {
        var index = _items.FindIndex(c => c.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"Category {id} was not found.");
    }

    private void ThrowIfInvalid(string name, Guid? excludeId)
    {
        var error = Validate(name, excludeId);
        if (error is not null)
            throw new ArgumentException(error, nameof(name));
    }
}
