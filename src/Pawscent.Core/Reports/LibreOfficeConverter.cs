using System.ComponentModel;
using System.Diagnostics;
using Pawscent.Core.Storage;

namespace Pawscent.Core.Reports;

/// <summary>
/// Converts spreadsheets, documents and less common image formats to PDF by running an installed LibreOffice
/// headless (<c>soffice --headless --convert-to pdf</c>). LibreOffice is optional.
/// </summary>
public sealed class LibreOfficeConverter(AppPaths paths) : IOfficeApp
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    // LibreOffice runs one instance per user profile, so conversions take turns.
    private static readonly Lock RunLock = new();

    /// <summary>The path of soffice in the usual install folders or on PATH, or null if LibreOffice isn't installed.</summary>
    public static string? FindExecutable()
    {
        var fileName = OperatingSystem.IsWindows() ? "soffice.exe" : "soffice";
        var installFolders = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(Environment.GetFolderPath)
            .Where(f => f.Length > 0)
            .Select(f => Path.Combine(f, "LibreOffice", "program"));
        var pathFolders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => f.Trim('"'))
            .Where(Path.IsPathFullyQualified);
        return installFolders.Concat(pathFolders).Select(f => Path.Combine(f, fileName)).FirstOrDefault(File.Exists);
    }

    public string Name => "LibreOffice";

    public bool IsInstalled => FindExecutable() is not null;

    // Every format reports convert: LibreOffice opens them all.
    public bool Converts(string extension) => true;

    public string ConvertToPdf(string sourcePath, string workFolder, CancellationToken cancellationToken)
    {
        var soffice = FindExecutable() ?? throw new ReceiptConversionException("LibreOffice is not installed.");

        // LibreOffice opens a copy with a plain name, so its lock files never land next to the receipt.
        Directory.CreateDirectory(workFolder);
        var input = Path.Combine(workFolder, "receipt" + Path.GetExtension(sourcePath).ToLowerInvariant());
        File.Copy(sourcePath, input, overwrite: true);
        var output = Path.ChangeExtension(input, ".pdf");

        var startInfo = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[]
        {
            "-env:UserInstallation=" + new Uri(paths.LibreOfficeProfile).AbsoluteUri,
            "--headless", "--norestore", "--nologo", "--nodefault",
            "--convert-to", "pdf", "--outdir", workFolder, input,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        lock (RunLock)
            Run(startInfo, cancellationToken);

        // soffice exits with 0 even when it can't load the file, so the output file is the only sign of success.
        if (!File.Exists(output))
            throw new ReceiptConversionException("LibreOffice could not convert this file to PDF.");
        return output;
    }

    private static void Run(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Process process;
        try
        {
            process = Process.Start(startInfo) ?? throw new ReceiptConversionException("LibreOffice could not be started.");
        }
        catch (Win32Exception ex)
        {
            throw new ReceiptConversionException($"LibreOffice could not be started: {ex.Message}");
        }

        using (process)
        using (cancellationToken.Register(() => Kill(process)))
        {
            if (!process.WaitForExit(Timeout))
            {
                Kill(process);
                throw new ReceiptConversionException("LibreOffice took too long to convert this file.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already exited.
        }
    }
}
