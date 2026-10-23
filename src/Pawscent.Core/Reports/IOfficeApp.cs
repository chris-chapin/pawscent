namespace Pawscent.Core.Reports;

/// <summary>An office program, installed separately, that converts some receipt formats to PDF.</summary>
public interface IOfficeApp
{
    /// <summary>The program's name as users know it, for messages.</summary>
    string Name { get; }

    /// <summary>Whether the program is installed on this PC. Checked before every conversion.</summary>
    bool IsInstalled { get; }

    /// <summary>Whether the program converts files with this extension (such as ".xlsx").</summary>
    bool Converts(string extension);

    /// <summary>Converts <paramref name="sourcePath"/> to a PDF inside <paramref name="workFolder"/> and returns its path.</summary>
    /// <exception cref="ReceiptConversionException">The program is missing or could not convert the file.</exception>
    string ConvertToPdf(string sourcePath, string workFolder, CancellationToken cancellationToken);
}
