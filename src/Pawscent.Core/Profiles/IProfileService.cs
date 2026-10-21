namespace Pawscent.Core.Profiles;

public interface IProfileService
{
    IReadOnlyList<Profile> List();

    Profile Create(string name);

    Profile Load(string name);

    /// <summary>Returns null if the name is valid, otherwise a user-facing reason.</summary>
    string? Validate(string name);
}
