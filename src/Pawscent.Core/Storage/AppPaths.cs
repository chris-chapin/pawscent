namespace Pawscent.Core.Storage;

/// <summary>
/// All application data lives under %LOCALAPPDATA%\Pawscent. Nothing is read or written elsewhere.
/// </summary>
public sealed class AppPaths
{
    public const string AppFolderName = "Pawscent";

    public AppPaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName))
    {
    }

    public AppPaths(string root)
    {
        Root = Path.GetFullPath(root);
        ProfilesRoot = Path.Combine(Root, "Profiles");
    }

    public string Root { get; }

    public string ProfilesRoot { get; }

    public void EnsureCreated() => Directory.CreateDirectory(ProfilesRoot);
}
