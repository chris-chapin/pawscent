using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;

namespace Pawscent.Core.Receipts;

/// <summary>
/// Keeps each profile's receipt files in its own Receipts folder. Every file the user adds is copied there, so the
/// profile never depends on files that might be moved, changed or deleted elsewhere, including by another profile.
/// </summary>
public sealed class ReceiptFiles
{
    public const string FolderName = "Receipts";

    public static string FolderFor(Profile profile) => Path.Combine(profile.FolderPath, FolderName);

    /// <summary>
    /// Copies <paramref name="sourcePath"/> into the profile's Receipts folder and adds the copy to the profile's
    /// receipts. A file already in that folder is added without copying; one already in the list is returned as is.
    /// </summary>
    public Receipt Import(ProfileDocument document, string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The receipt file was not found.", fullPath);

        var existing = document.Receipts.FindByPath(fullPath);
        if (existing is not null)
            return existing;

        var folder = FolderFor(document.Profile);
        var storedPath = fullPath;
        if (!IsUnder(fullPath, folder))
        {
            Directory.CreateDirectory(folder);
            storedPath = UniquePath(folder, Path.GetFileName(fullPath));
            File.Copy(fullPath, storedPath, overwrite: false);
        }

        try
        {
            return document.Receipts.Create(storedPath);
        }
        catch (ArgumentException)
        {
            if (storedPath != fullPath)
                TryDelete(storedPath);
            throw;
        }
    }

    /// <summary>Deletes the receipt's file if it is a copy in the profile's Receipts folder. Returns true if deleted.</summary>
    public bool DeleteStoredCopy(Profile profile, Receipt receipt)
    {
        if (!IsUnder(receipt.FilePath, FolderFor(profile)) || !File.Exists(receipt.FilePath))
            return false;
        File.Delete(receipt.FilePath);
        return true;
    }

    private static bool IsUnder(string filePath, string folder)
    {
        var full = Path.GetFullPath(filePath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    // "bill.pdf", then "bill (2).pdf", "bill (3).pdf", ... so copies never overwrite each other.
    private static string UniquePath(string folder, string fileName)
    {
        var candidate = Path.Combine(folder, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var n = 2; File.Exists(candidate); n++)
            candidate = Path.Combine(folder, $"{stem} ({n}){extension}");
        return candidate;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
