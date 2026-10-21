using Pawscent.Core.Expenses;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class CategoryListTests
{
    private readonly CategoryList _categories = new();

    [Fact]
    public void Create_AddsCategoryWithNewId()
    {
        var category = _categories.Create("Vet");

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Vet", category.Name);
        Assert.Equal([category], _categories.Items);
        Assert.Same(category, _categories.Get(category.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Food ")]
    [InlineData("bad\tname")]
    public void Create_RejectsInvalidNames(string name) =>
        Assert.Throws<ArgumentException>(() => _categories.Create(name));

    [Fact]
    public void Create_RejectsTooLongName() =>
        Assert.Throws<ArgumentException>(() => _categories.Create(new string('a', CategoryList.MaxNameLength + 1)));

    [Fact]
    public void Create_RejectsDuplicateNameIgnoringCase()
    {
        _categories.Create("Food");

        Assert.Throws<ArgumentException>(() => _categories.Create("FOOD"));
        Assert.Single(_categories.Items);
    }

    [Fact]
    public void FindByName_IgnoresCase()
    {
        var category = _categories.Create("Grooming");

        Assert.Equal(category, _categories.FindByName("grooming"));
        Assert.Null(_categories.FindByName("Toys"));
    }

    [Fact]
    public void Update_RenamesAndKeepsIdAndOrder()
    {
        var first = _categories.Create("Food");
        var second = _categories.Create("Toys");

        var updated = _categories.Update(first.Id, "Treats");

        Assert.Equal(new Category(first.Id, "Treats"), updated);
        Assert.Equal([updated, second], _categories.Items);
    }

    [Fact]
    public void Update_AllowsCaseChangeOnSameCategory() =>
        Assert.Equal("FOOD", _categories.Update(_categories.Create("Food").Id, "FOOD").Name);

    [Fact]
    public void Update_RejectsNameOfAnotherCategory()
    {
        _categories.Create("Food");
        var second = _categories.Create("Toys");

        Assert.Throws<ArgumentException>(() => _categories.Update(second.Id, "food"));
        Assert.Equal("Toys", _categories.Get(second.Id).Name);
    }

    [Fact]
    public void Update_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _categories.Update(Guid.NewGuid(), "Food"));

    [Fact]
    public void Delete_RemovesCategory()
    {
        var category = _categories.Create("Food");

        _categories.Delete(category.Id);

        Assert.Empty(_categories.Items);
        Assert.False(_categories.Contains(category.Id));
    }

    [Fact]
    public void Delete_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _categories.Delete(Guid.NewGuid()));

    [Fact]
    public void Get_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _categories.Get(Guid.NewGuid()));

    [Fact]
    public void Constructor_LoadsExistingItems()
    {
        var items = new[] { new Category(Guid.NewGuid(), "Food"), new Category(Guid.NewGuid(), "Vet") };

        var categories = new CategoryList(items);

        Assert.Equal(items, categories.Items);
    }

    [Fact]
    public void Constructor_RejectsDuplicateIdsAndNames()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new CategoryList([new Category(id, "Food"), new Category(id, "Vet")]));
        Assert.Throws<ArgumentException>(() =>
            new CategoryList([new Category(Guid.NewGuid(), "Food"), new Category(Guid.NewGuid(), "food")]));
    }
}
