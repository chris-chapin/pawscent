namespace Pawscent.Core.Profiles;

public interface IProfileService
{
    IReadOnlyList<Profile> List();

    Profile Create(string name);

    Profile Load(string name);

    /// <summary>Reads the profile's header, categories, receipts and expenses.</summary>
    /// <exception cref="InvalidDataException">The profile file is damaged or from a newer version.</exception>
    ProfileDocument Open(string name);

    /// <summary>Writes the document to its profile.json via a temporary file, so a failed save keeps the old file.</summary>
    void Save(ProfileDocument document);

    /// <summary>Returns null if the name is valid, otherwise a user-facing reason.</summary>
    string? Validate(string name);
}
