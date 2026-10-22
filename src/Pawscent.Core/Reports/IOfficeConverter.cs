namespace Pawscent.Core.Reports;

/// <summary>Converts receipts PDFsharp can't read, such as spreadsheets, to PDF.</summary>
public interface IOfficeConverter
{
    /// <summary>Converts <paramref name="sourcePath"/> to a PDF inside <paramref name="workFolder"/> and returns its path.</summary>
    /// <exception cref="ReceiptConversionException">The converter is missing or could not convert the file.</exception>
    string ConvertToPdf(string sourcePath, string workFolder, CancellationToken cancellationToken);
}

/// <summary>A receipt could not be converted to PDF. The message is user-facing.</summary>
public sealed class ReceiptConversionException(string message) : Exception(message);
