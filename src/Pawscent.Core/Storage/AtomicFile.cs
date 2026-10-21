using System.Text;

namespace Pawscent.Core.Storage;

public static class AtomicFile
{
    public const string TempSuffix = ".tmp";

    /// <summary>
    /// Writes <paramref name="contents"/> to a temporary file next to <paramref name="path"/>, flushes it to disk, then
    /// renames it over <paramref name="path"/>. A crash mid-write leaves the previous file intact.
    /// </summary>
    public static void WriteAllText(string path, string contents)
    {
        var tempPath = path + TempSuffix;
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(contents);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
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
