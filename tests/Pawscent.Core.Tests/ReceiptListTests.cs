using Pawscent.Core.Expenses;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ReceiptListTests
{
    private readonly ReceiptList _receipts = new();

    [Fact]
    public void Create_AddsReceiptWithNewId()
    {
        var receipt = _receipts.Create(@"C:\Receipts\vet.pdf");

        Assert.NotEqual(Guid.Empty, receipt.Id);
        Assert.Equal(@"C:\Receipts\vet.pdf", receipt.FilePath);
        Assert.Equal([receipt], _receipts.Items);
        Assert.Same(receipt, _receipts.Get(receipt.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" padded.pdf ")]
    [InlineData("bad\tname.pdf")]
    public void Create_RejectsInvalidPaths(string path) =>
        Assert.Throws<ArgumentException>(() => _receipts.Create(path));

    [Fact]
    public void Create_RejectsDuplicatePathIgnoringCase()
    {
        _receipts.Create(@"C:\Receipts\vet.pdf");

        Assert.Throws<ArgumentException>(() => _receipts.Create(@"c:\receipts\VET.pdf"));
        Assert.Single(_receipts.Items);
    }

    [Fact]
    public void FindByPath_IgnoresCase()
    {
        var receipt = _receipts.Create(@"C:\Receipts\vet.pdf");

        Assert.Equal(receipt, _receipts.FindByPath(@"C:\RECEIPTS\vet.PDF"));
        Assert.Null(_receipts.FindByPath(@"C:\Receipts\other.pdf"));
    }

    [Fact]
    public void Update_ChangesPathAndKeepsIdAndOrder()
    {
        var first = _receipts.Create("a.pdf");
        var second = _receipts.Create("b.pdf");

        var updated = _receipts.Update(first.Id, "c.pdf");

        Assert.Equal(first.Id, updated.Id);
        Assert.Equal("c.pdf", updated.FilePath);
        Assert.Equal([updated, second], _receipts.Items);
    }

    [Fact]
    public void Update_AllowsSamePathOnSameReceipt()
    {
        var receipt = _receipts.Create("a.pdf");

        var updated = _receipts.Update(receipt.Id, "A.pdf");

        Assert.Equal("A.pdf", updated.FilePath);
    }

    [Fact]
    public void Update_RejectsPathOfAnotherReceipt()
    {
        _receipts.Create("a.pdf");
        var second = _receipts.Create("b.pdf");

        Assert.Throws<ArgumentException>(() => _receipts.Update(second.Id, "a.pdf"));
        Assert.Equal("b.pdf", _receipts.Get(second.Id).FilePath);
    }

    [Fact]
    public void Update_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _receipts.Update(Guid.NewGuid(), "a.pdf"));

    [Fact]
    public void Delete_RemovesReceipt()
    {
        var receipt = _receipts.Create("a.pdf");

        _receipts.Delete(receipt.Id);

        Assert.Empty(_receipts.Items);
        Assert.False(_receipts.Contains(receipt.Id));
    }

    [Fact]
    public void Delete_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _receipts.Delete(Guid.NewGuid()));

    [Fact]
    public void Get_Missing_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => _receipts.Get(Guid.NewGuid()));

    [Fact]
    public void Constructor_LoadsExistingItems()
    {
        var items = new[] { new Receipt(Guid.NewGuid(), "a.pdf"), new Receipt(Guid.NewGuid(), "b.pdf") };

        var receipts = new ReceiptList(items);

        Assert.Equal(items, receipts.Items);
    }

    [Fact]
    public void Constructor_RejectsDuplicateIdsAndPaths()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new ReceiptList([new Receipt(id, "a.pdf"), new Receipt(id, "b.pdf")]));
        Assert.Throws<ArgumentException>(() =>
            new ReceiptList([new Receipt(Guid.NewGuid(), "a.pdf"), new Receipt(Guid.NewGuid(), "A.PDF")]));
    }
}
