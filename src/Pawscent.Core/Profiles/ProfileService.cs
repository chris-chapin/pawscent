using System.Text.Json;
using Pawscent.Core.Expenses;
using Pawscent.Core.Storage;

namespace Pawscent.Core.Profiles;

public sealed class ProfileService : IProfileService
{
    public const string MetadataFileName = "profile.json";
    public const int MaxNameLength = 64;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // Windows-invalid file name characters, fixed so behavior is identical on every OS.
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private readonly AppPaths _paths;

    public ProfileService(AppPaths paths)
    {
        _paths = paths;
        _paths.EnsureCreated();
    }

    public IReadOnlyList<Profile> List() =>
        Directory.EnumerateDirectories(_paths.ProfilesRoot)
            .Where(d => File.Exists(Path.Combine(d, MetadataFileName)))
            .Select(d => TryRead(d))
            .OfType<Profile>()
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public string? Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";
        if (name != name.Trim())
            return "Name cannot start or end with spaces.";
        if (name.Length > MaxNameLength)
            return $"Name must be {MaxNameLength} characters or fewer.";
        if (name.IndexOfAny(InvalidChars) >= 0 || name.Any(char.IsControl))
            return "Name contains characters that are not allowed: < > : \" / \\ | ? *";
        if (name.EndsWith('.') || name is "." or "..")
            return "Name cannot end with a period.";
        if (ReservedNames.Contains(name.Split('.')[0]))
            return "That name is reserved by Windows.";
        if (Directory.Exists(Path.Combine(_paths.ProfilesRoot, name)))
            return "A profile with that name already exists.";
        return null;
    }

    public Profile Create(string name)
    {
        var error = Validate(name);
        if (error is not null)
            throw new ArgumentException(error, nameof(name));

        var folder = ResolveFolder(name);
        Directory.CreateDirectory(folder);

        var profile = new Profile(name, DateTimeOffset.UtcNow, Profile.CurrentSchemaVersion) { FolderPath = folder };
        Save(new ProfileDocument(profile));
        return profile;
    }

    public Profile Load(string name)
    {
        var folder = ResolveFolder(name);
        return TryRead(folder) ?? throw new DirectoryNotFoundException($"Profile '{name}' was not found.");
    }

    public ProfileDocument Open(string name)
    {
        var folder = ResolveFolder(name);
        var path = Path.Combine(folder, MetadataFileName);
        if (!File.Exists(path))
            throw new DirectoryNotFoundException($"Profile '{name}' was not found.");

        ProfileFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The profile file is damaged: {ex.Message}", ex);
        }
        if (file is null)
            throw new InvalidDataException("The profile file is empty.");
        if (file.SchemaVersion > Profile.CurrentSchemaVersion)
            throw new InvalidDataException("This profile was saved by a newer version of Pawscent.");

        var profile = new Profile(file.Name, file.CreatedUtc, file.SchemaVersion) { FolderPath = folder };
        try
        {
            return new ProfileDocument(
                profile,
                new CategoryList(NonNullItems(file.Categories)),
                new ReceiptList(NonNullItems(file.Receipts)),
                NonNullItems(file.Expenses));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"The profile file contains invalid data: {ex.Message}", ex);
        }
    }

    public void Save(ProfileDocument document)
    {
        var profile = document.Profile;
        if (string.IsNullOrEmpty(profile.FolderPath))
            throw new ArgumentException("The profile has no folder.", nameof(document));

        var file = new ProfileFile(
            profile.Name,
            profile.CreatedUtc,
            Profile.CurrentSchemaVersion,
            document.Categories.Items,
            document.Receipts.Items,
            document.Expenses.Items);
        AtomicFile.WriteAllText(Path.Combine(profile.FolderPath, MetadataFileName), JsonSerializer.Serialize(file, JsonOptions));
    }

    // Guarantees the resulting path is a direct child of the profiles root.
    private string ResolveFolder(string name)
    {
        var folder = Path.GetFullPath(Path.Combine(_paths.ProfilesRoot, name));
        if (!string.Equals(Path.GetDirectoryName(folder), _paths.ProfilesRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Profile path must stay inside the profiles folder.", nameof(name));
        return folder;
    }

    // A missing list loads as empty; a null entry inside one means the file is damaged.
    private static List<T> NonNullItems<T>(IReadOnlyList<T?>? items)
        where T : class
    {
        var result = new List<T>(items?.Count ?? 0);
        foreach (var item in items ?? [])
            result.Add(item ?? throw new InvalidDataException("The profile file contains an empty entry."));
        return result;
    }

    private static Profile? TryRead(string folder)
    {
        try
        {
            var json = File.ReadAllText(Path.Combine(folder, MetadataFileName));
            var profile = JsonSerializer.Deserialize<Profile>(json);
            return profile is null ? null : profile with { FolderPath = folder };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
