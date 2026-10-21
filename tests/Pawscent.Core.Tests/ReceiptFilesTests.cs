using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;
using Pawscent.Core.Storage;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ReceiptFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pawscent-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside;
    private readonly ReceiptFiles _files;
    private readonly ProfileDocument _document;

    public ReceiptFilesTests()
    {
        var paths = new AppPaths(Path.Combine(_root, "app"));
        _outside = Path.Combine(_root, "downloads");
        Directory.CreateDirectory(_outside);
        _files = new ReceiptFiles(paths);
        var service = new ProfileService(paths);
        service.Create("Home");
        _document = service.Open("Home");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Import_FileOutsideAppFolder_IsCopiedIntoProfileReceiptsFolder()
    {
        var source = WriteFile(_outside, "vet.pdf", "bill");

        var receipt = _files.Import(_document, source);

        var expected = Path.Combine(ReceiptFiles.FolderFor(_document.Profile), "vet.pdf");
        Assert.Equal(expected, receipt.FilePath);
        Assert.Equal("bill", File.ReadAllText(expected));
        Assert.True(File.Exists(source));
        Assert.Equal([receipt], _document.Receipts.Items);
    }

    [Fact]
    public void Import_SameNameTwice_DoesNotOverwriteEarlierCopy()
    {
        var first = _files.Import(_document, WriteFile(_outside, "vet.pdf", "one"));
        var otherFolder = Path.Combine(_outside, "other");
        Directory.CreateDirectory(otherFolder);

        var second = _files.Import(_document, WriteFile(otherFolder, "vet.pdf", "two"));

        Assert.Equal("vet (2).pdf", Path.GetFileName(second.FilePath));
        Assert.Equal("one", File.ReadAllText(first.FilePath));
        Assert.Equal("two", File.ReadAllText(second.FilePath));
    }

    [Fact]
    public void Import_FileInsideAppFolder_IsUsedInPlace()
    {
        var source = WriteFile(_document.Profile.FolderPath, "scan.png", "img");

        var receipt = _files.Import(_document, source);

        Assert.Equal(source, receipt.FilePath);
        Assert.False(Directory.Exists(ReceiptFiles.FolderFor(_document.Profile)));
    }

    [Fact]
    public void Import_FileAlreadyInList_ReturnsExistingReceipt()
    {
        var source = WriteFile(_document.Profile.FolderPath, "scan.png", "img");
        var first = _files.Import(_document, source);

        Assert.Same(first, _files.Import(_document, source));
        Assert.Single(_document.Receipts.Items);
    }

    [Fact]
    public void Import_MissingFile_Throws() =>
        Assert.Throws<FileNotFoundException>(() => _files.Import(_document, Path.Combine(_outside, "missing.pdf")));

    [Fact]
    public void IsInsideAppFolder_DoesNotMatchSiblingWithSamePrefix()
    {
        var sibling = Path.Combine(_root, "appendix", "a.pdf");

        Assert.False(_files.IsInsideAppFolder(sibling));
        Assert.True(_files.IsInsideAppFolder(Path.Combine(_root, "app", "a.pdf")));
    }

    [Fact]
    public void DeleteStoredCopy_DeletesOnlyCopiesInReceiptsFolder()
    {
        var copied = _files.Import(_document, WriteFile(_outside, "vet.pdf", "bill"));
        var inPlace = _files.Import(_document, WriteFile(_document.Profile.FolderPath, "scan.png", "img"));

        Assert.True(_files.DeleteStoredCopy(_document.Profile, copied));
        Assert.False(File.Exists(copied.FilePath));
        Assert.False(_files.DeleteStoredCopy(_document.Profile, inPlace));
        Assert.True(File.Exists(inPlace.FilePath));
    }

    private static string WriteFile(string folder, string name, string contents)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, contents);
        return path;
    }
}
