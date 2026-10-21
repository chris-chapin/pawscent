using Pawscent.Core.Profiles;
using Pawscent.Core.Storage;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ProfileServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pawscent-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ProfileService _service;

    public ProfileServiceTests() => _service = new ProfileService(new AppPaths(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Create_MakesFolderWithMetadata()
    {
        var profile = _service.Create("Home");

        Assert.True(Directory.Exists(profile.FolderPath));
        Assert.True(File.Exists(Path.Combine(profile.FolderPath, ProfileService.MetadataFileName)));
        Assert.Equal(Path.Combine(_root, "Profiles", "Home"), profile.FolderPath);
    }

    [Fact]
    public void List_ReturnsCreatedProfilesSorted()
    {
        _service.Create("beta");
        _service.Create("Alpha");

        Assert.Equal(["Alpha", "beta"], _service.List().Select(p => p.Name));
    }

    [Fact]
    public void Load_RoundTrips()
    {
        var created = _service.Create("Work");
        var loaded = _service.Load("Work");

        Assert.Equal(created.Name, loaded.Name);
        Assert.Equal(created.FolderPath, loaded.FolderPath);
    }

    [Fact]
    public void Load_Missing_Throws() =>
        Assert.Throws<DirectoryNotFoundException>(() => _service.Load("nope"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("trailing.")]
    [InlineData(" padded ")]
    [InlineData("what?")]
    public void Create_RejectsInvalidNames(string name) =>
        Assert.Throws<ArgumentException>(() => _service.Create(name));

    [Fact]
    public void Create_RejectsDuplicate()
    {
        _service.Create("Dup");
        Assert.Throws<ArgumentException>(() => _service.Create("Dup"));
    }
}
