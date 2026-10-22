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
        TempRoot = Path.Combine(Root, "Temp");
        LibreOfficeProfile = Path.Combine(Root, "LibreOffice");
    }

    public string Root { get; }

    public string ProfilesRoot { get; }

    /// <summary>Scratch files of a running task, such as converted receipts while a report is built.</summary>
    public string TempRoot { get; }

    /// <summary>
    /// LibreOffice's user profile when it converts receipts, kept here instead of the user's own LibreOffice settings.
    /// </summary>
    public string LibreOfficeProfile { get; }

    public void EnsureCreated() => Directory.CreateDirectory(ProfilesRoot);
}
