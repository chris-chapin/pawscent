using System.Text.Json;
using Pawscent.Core.Profiles;
using Pawscent.Core.Storage;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ProfileStorageTests : IDisposable
{
    private static readonly DateOnly Date = new(2026, 10, 21);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pawscent-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ProfileService _service;

    public ProfileStorageTests() => _service = new ProfileService(new AppPaths(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Open_NewProfile_HasHeaderAndEmptyLists()
    {
        var created = _service.Create("Home");

        var document = _service.Open("Home");

        Assert.Equal(created.Name, document.Profile.Name);
        Assert.Equal(created.CreatedUtc, document.Profile.CreatedUtc);
        Assert.Equal(created.FolderPath, document.Profile.FolderPath);
        Assert.Empty(document.Categories.Items);
        Assert.Empty(document.Receipts.Items);
        Assert.Empty(document.Expenses.Items);
    }

    [Fact]
    public void Save_ThenOpen_RoundTripsCategoriesReceiptsAndExpenses()
    {
        _service.Create("Home");
        var document = _service.Open("Home");
        var vet = document.Categories.Create("Vet");
        var food = document.Categories.Create("Food");
        var receipt = document.Receipts.Create(@"C:\Receipts\vet.pdf");
        var checkup = document.Expenses.Create(Date, 42.50m, "USD", "Checkup", vet.Id, receipt.Id);
        var kibble = document.Expenses.CreateWithNewReceipt(Date, 19.99m, "EUR", "Kibble", food.Id, @"C:\Receipts\food.jpg");

        _service.Save(document);
        var reopened = _service.Open("Home");

        Assert.Equal(document.Categories.Items, reopened.Categories.Items);
        Assert.Equal(document.Receipts.Items, reopened.Receipts.Items);
        Assert.Equal([checkup, kibble], reopened.Expenses.Items);
    }

    [Fact]
    public void Save_WritesListsInOrderAndLeavesNoTempFile()
    {
        var profile = _service.Create("Home");
        var document = _service.Open("Home");
        var vet = document.Categories.Create("Vet");
        var receipt = document.Receipts.Create(@"C:\Receipts\vet.pdf");
        document.Expenses.Create(Date, 1m, "USD", "Checkup", vet.Id, receipt.Id);

        _service.Save(document);

        var path = Path.Combine(profile.FolderPath, ProfileService.MetadataFileName);
        Assert.False(File.Exists(path + AtomicFile.TempSuffix));
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(
            ["Name", "CreatedUtc", "SchemaVersion", "Categories", "Receipts", "Expenses"],
            json.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Save_ReloadedDocumentEnforcesReferences()
    {
        _service.Create("Home");
        var document = _service.Open("Home");
        var vet = document.Categories.Create("Vet");
        var receipt = document.Receipts.Create(@"C:\Receipts\vet.pdf");
        document.Expenses.Create(Date, 1m, "USD", "Checkup", vet.Id, receipt.Id);
        _service.Save(document);

        var reopened = _service.Open("Home");

        Assert.Throws<InvalidOperationException>(() => reopened.Categories.Delete(vet.Id));
        Assert.Throws<InvalidOperationException>(() => reopened.Receipts.Delete(receipt.Id));
    }

    [Fact]
    public void Open_HeaderOnlyFile_LoadsWithEmptyLists()
    {
        var folder = Path.Combine(_root, "Profiles", "Old");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, ProfileService.MetadataFileName),
            """{ "Name": "Old", "CreatedUtc": "2026-01-01T00:00:00+00:00", "SchemaVersion": 1 }""");

        var document = _service.Open("Old");

        Assert.Equal("Old", document.Profile.Name);
        Assert.Empty(document.Categories.Items);
        Assert.Empty(document.Receipts.Items);
        Assert.Empty(document.Expenses.Items);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{ "Name": "Bad", "CreatedUtc": "2026-01-01T00:00:00+00:00", "SchemaVersion": 99 }""")]
    [InlineData("""{ "Name": "Bad", "CreatedUtc": "2026-01-01T00:00:00+00:00", "SchemaVersion": 1, "Categories": [null] }""")]
    [InlineData("""{ "Name": "Bad", "CreatedUtc": "2026-01-01T00:00:00+00:00", "SchemaVersion": 1, "Categories": [{ "Id": "00000000-0000-0000-0000-000000000001", "Name": "" }] }""")]
    [InlineData("""{ "Name": "Bad", "CreatedUtc": "2026-01-01T00:00:00+00:00", "SchemaVersion": 1, "Expenses": [{ "Id": "00000000-0000-0000-0000-000000000001", "Date": "2026-10-21", "Amount": 1, "Currency": "USD", "Description": "x", "CategoryId": "00000000-0000-0000-0000-000000000002", "ReceiptId": "00000000-0000-0000-0000-000000000003" }] }""")]
    public void Open_InvalidFile_ThrowsInvalidData(string json)
    {
        var folder = Path.Combine(_root, "Profiles", "Bad");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ProfileService.MetadataFileName), json);

        Assert.Throws<InvalidDataException>(() => _service.Open("Bad"));
    }

    [Fact]
    public void Open_Missing_Throws() =>
        Assert.Throws<DirectoryNotFoundException>(() => _service.Open("nope"));

    [Fact]
    public void List_StillReadsHeadersOfFullFiles()
    {
        _service.Create("Home");
        var document = _service.Open("Home");
        document.Categories.Create("Vet");
        _service.Save(document);

        Assert.Equal(["Home"], _service.List().Select(p => p.Name));
    }

    [Fact]
    public void AtomicFile_ReplacesExistingFile()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "file.json");
        File.WriteAllText(path, "old");

        AtomicFile.WriteAllText(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(File.Exists(path + AtomicFile.TempSuffix));
    }
}
