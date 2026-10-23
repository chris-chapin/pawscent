using System.Globalization;

namespace Pawscent.Core.Reports;

/// <summary>
/// Converts a receipt with the first installed office program that handles its format, trying the next one if it
/// fails. When none is installed, the error names the programs to install.
/// </summary>
/// <param name="apps">The programs in order of preference.</param>
public sealed class OfficeConverter(IEnumerable<IOfficeApp> apps) : IOfficeConverter
{
    private readonly IOfficeApp[] _apps = [.. apps];

    public string ConvertToPdf(string sourcePath, string workFolder, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(sourcePath);
        var candidates = _apps.Where(a => a.Converts(extension)).ToList();
        if (candidates.Count == 0)
            throw new ReceiptConversionException("This type of file can't be converted to PDF.");

        ReceiptConversionException? firstFailure = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates[i].IsInstalled)
                continue;
            // A folder per program, so a half-written PDF from one never passes for the next one's result.
            var appWorkFolder = Path.Combine(workFolder, i.ToString(CultureInfo.InvariantCulture));
            try
            {
                return candidates[i].ConvertToPdf(sourcePath, appWorkFolder, cancellationToken);
            }
            catch (ReceiptConversionException ex)
            {
                firstFailure ??= ex;
            }
        }
        throw firstFailure ?? new ReceiptConversionException(NeededMessage(candidates.Select(a => a.Name).ToList()));
    }

    private static string NeededMessage(List<string> names) => names.Count == 1
        ? $"{names[0]} is needed to add this file to a report. Install {names[0]} and create the report again."
        : $"{string.Join(" or ", names)} is needed to add this file to a report. Install one of them and create the report again.";
}
