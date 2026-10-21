using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;
using Pawscent.Core.Storage;

namespace Pawscent.Core.Receipts;

/// <summary>
/// Keeps receipt files inside the app folder. A file picked from elsewhere is copied into the profile's Receipts
/// folder, so the profile never depends on files the user might move or delete.
/// </summary>
public sealed class ReceiptFiles(AppPaths paths)
{
    public const string FolderName = "Receipts";

    public static string FolderFor(Profile profile) => Path.Combine(profile.FolderPath, FolderName);

    /// <summary>True if <paramref name="filePath"/> is somewhere under the app folder.</summary>
    public bool IsInsideAppFolder(string filePath) => IsUnder(filePath, paths.Root);

    /// <summary>
    /// Adds <paramref name="sourcePath"/> to the profile's receipts and returns the receipt. A file outside the app
    /// folder is copied into the profile's Receipts folder first; a file already in the list is returned as is.
    /// </summary>
    public Receipt Import(ProfileDocument document, string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The receipt file was not found.", fullPath);

        var existing = document.Receipts.FindByPath(fullPath);
        if (existing is not null)
            return existing;

        var storedPath = fullPath;
        if (!IsInsideAppFolder(fullPath))
        {
            var folder = FolderFor(document.Profile);
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
