using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;
using Pawscent.Core.Storage;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ReceiptFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pawscent-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside;
    private readonly AppPaths _paths;
    private readonly ReceiptFiles _files;
    private readonly ProfileDocument _document;

    public ReceiptFilesTests()
    {
        _paths = new AppPaths(Path.Combine(_root, "app"));
        _outside = Path.Combine(_root, "downloads");
        Directory.CreateDirectory(_outside);
        _files = new ReceiptFiles();
        var service = new ProfileService(_paths);
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
    public void Import_FileElsewhereInAppFolder_IsCopiedToo()
    {
        var inProfileFolder = WriteFile(_document.Profile.FolderPath, "scan.png", "img");
        var otherReceipts = Path.Combine(_paths.ProfilesRoot, "Other", ReceiptFiles.FolderName);
        Directory.CreateDirectory(otherReceipts);
        var inOtherProfile = WriteFile(otherReceipts, "vet.pdf", "bill");

        var first = _files.Import(_document, inProfileFolder);
        var second = _files.Import(_document, inOtherProfile);

        var folder = ReceiptFiles.FolderFor(_document.Profile);
        Assert.Equal(Path.Combine(folder, "scan.png"), first.FilePath);
        Assert.Equal(Path.Combine(folder, "vet.pdf"), second.FilePath);
        Assert.True(File.Exists(inProfileFolder));
        Assert.True(File.Exists(inOtherProfile));
    }

    [Fact]
    public void Import_FileInSiblingFolderWithSamePrefix_IsCopied()
    {
        var sibling = Path.Combine(_document.Profile.FolderPath, ReceiptFiles.FolderName + "2");
        Directory.CreateDirectory(sibling);

        var receipt = _files.Import(_document, WriteFile(sibling, "vet.pdf", "bill"));

        Assert.Equal(Path.Combine(ReceiptFiles.FolderFor(_document.Profile), "vet.pdf"), receipt.FilePath);
    }

    [Fact]
    public void Import_FileAlreadyInReceiptsFolder_IsAddedWithoutCopying()
    {
        var folder = ReceiptFiles.FolderFor(_document.Profile);
        Directory.CreateDirectory(folder);
        var source = WriteFile(folder, "scan.png", "img");

        var receipt = _files.Import(_document, source);

        Assert.Equal(source, receipt.FilePath);
        Assert.Equal([source], Directory.EnumerateFiles(folder));
    }

    [Fact]
    public void Import_FileAlreadyInList_ReturnsExistingReceipt()
    {
        var first = _files.Import(_document, WriteFile(_outside, "vet.pdf", "bill"));

        Assert.Same(first, _files.Import(_document, first.FilePath));
        Assert.Single(_document.Receipts.Items);
    }

    [Fact]
    public void Import_MissingFile_Throws() =>
        Assert.Throws<FileNotFoundException>(() => _files.Import(_document, Path.Combine(_outside, "missing.pdf")));

    [Fact]
    public void DeleteStoredCopy_DeletesOnlyCopiesInReceiptsFolder()
    {
        var copied = _files.Import(_document, WriteFile(_outside, "vet.pdf", "bill"));
        // A receipt pointing outside the Receipts folder, as older versions allowed.
        var inPlace = _document.Receipts.Create(WriteFile(_document.Profile.FolderPath, "scan.png", "img"));

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
