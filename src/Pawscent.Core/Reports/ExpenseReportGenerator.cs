using System.Globalization;
using MigraDoc.Rendering;
using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;
using Pawscent.Core.Storage;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace Pawscent.Core.Reports;

public enum ReportPaper
{
    A4,
    Letter,
}

public static class ReportPapers
{
    // Regions that print on US Letter; the rest of the world uses A4.
    private static readonly HashSet<string> LetterRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        "US", "CA", "MX", "PH", "CL", "CO", "VE", "CR", "GT", "PA", "DO", "SV", "NI", "PR",
    };

    /// <summary>The usual paper in a region, given as a two-letter ISO code such as "US".</summary>
    public static ReportPaper ForRegion(string twoLetterRegionCode) =>
        LetterRegions.Contains(twoLetterRegionCode) ? ReportPaper.Letter : ReportPaper.A4;
}

/// <summary>A receipt the report shows a notice page for instead of the receipt itself.</summary>
public sealed record ReportIssue(string FileName, string Reason);

public sealed record ReportResult(string FilePath, IReadOnlyList<ReportIssue> Issues);

/// <summary>
/// Builds a PDF report of a year tab's expenses: a summary that serves as the table of contents, followed by every
/// receipt in the order first listed. Summary lines and PDF bookmarks link to the receipts' pages.
/// </summary>
public sealed class ExpenseReportGenerator(AppPaths paths, IOfficeConverter officeConverter, TimeProvider timeProvider)
{
    public const string FolderName = "Reports";

    // How many file names to try when earlier reports are open in a PDF viewer and can't be replaced.
    private const int MaxFileNameAttempts = 20;

    public static string FolderFor(Profile profile) => Path.Combine(profile.FolderPath, FolderName);

    /// <summary>
    /// Writes the report to the profile's Reports folder, replacing an earlier report of the same year, and returns
    /// its path. A receipt that can't be read gets a notice page instead and is listed in the result.
    /// </summary>
    /// <exception cref="IOException">The report could not be saved.</exception>
    /// <exception cref="UnauthorizedAccessException">The report could not be saved.</exception>
    /// <exception cref="InvalidOperationException">No font for the report was found.</exception>
    public ReportResult Generate(ExpenseReport report, ReportPaper paper, CancellationToken cancellationToken = default)
    {
        ReportFonts.EnsureRegistered();
        var workFolder = Path.Combine(paths.TempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            using var output = new PdfDocument();
            var issues = new List<ReportIssue>();

            // 1. Receipts first, recording where each starts. 2. The summary, moved to the front, which shifts
            // every receipt by the summary's page count. 3. Links and bookmarks to the final page numbers.
            var receiptStarts = AppendReceipts(output, report.Receipts, paper, workFolder, issues, cancellationToken);
            var summaryPageCount = InsertSummary(output, report, paper, receiptStarts);
            var firstPages = receiptStarts.ToDictionary(p => p.Key, p => p.Value + summaryPageCount);
            AddNavigation(output, report, summaryPageCount, firstPages);

            output.Info.Title = $"{report.Profile.Name} – {report.Year} expenses";
            output.Info.Creator = "Pawscent";
            return new ReportResult(Save(output, report), issues);
        }
        finally
        {
            TryDeleteFolder(workFolder);
        }
    }

    // Returns the 1-based page each receipt starts on, counting from the first receipt page.
    private Dictionary<Guid, int> AppendReceipts(
        PdfDocument output,
        IReadOnlyList<Receipt> receipts,
        ReportPaper paper,
        string workFolder,
        List<ReportIssue> issues,
        CancellationToken cancellationToken)
    {
        var starts = new Dictionary<Guid, int>();
        for (var i = 0; i < receipts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = receipts[i];
            var start = output.PageCount;
            try
            {
                var receiptWorkFolder = Path.Combine(workFolder, i.ToString(CultureInfo.InvariantCulture));
                ReceiptPages.Append(output, receipt.FilePath, paper, officeConverter, receiptWorkFolder, cancellationToken);
            }
            // PDFsharp reports damaged or unusual files with many exception types; each only costs this receipt.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                while (output.PageCount > start)
                    output.Pages.RemoveAt(output.PageCount - 1);
                var issue = new ReportIssue(Path.GetFileName(receipt.FilePath), Reason(ex));
                ReceiptPages.AppendNotice(output, issue.FileName, issue.Reason, paper);
                issues.Add(issue);
            }
            starts[receipt.Id] = start + 1;
        }
        return starts;
    }

    private static string Reason(Exception ex) => ex switch
    {
        FileNotFoundException or ReceiptConversionException => ex.Message,
        IOException or UnauthorizedAccessException => $"The file could not be read: {ex.Message}",
        _ => "The file could not be read. It may be damaged, protected by a password, or use a feature reports don't support.",
    };

    // Renders the summary after the receipts and moves it to the front. Returns its page count.
    private int InsertSummary(PdfDocument output, ExpenseReport report, ReportPaper paper, Dictionary<Guid, int> receiptStarts)
    {
        var created = timeProvider.GetLocalNow();
        var receiptPageCount = output.PageCount;

        // The summary prints page numbers that depend on its own length, so lay it out until its guess holds.
        var guess = 1;
        var summary = SummaryDocument.Build(report, paper, created, r => receiptStarts[r.Id] + guess);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var layout = new DocumentRenderer(summary);
            layout.PrepareDocument();
            var pageCount = layout.FormattedDocument!.PageCount;
            if (pageCount == guess)
                break;
            guess = pageCount;
            summary = SummaryDocument.Build(report, paper, created, r => receiptStarts[r.Id] + guess);
        }

        new PdfDocumentRenderer { Document = summary, PdfDocument = output }.RenderDocument();
        var summaryPageCount = output.PageCount - receiptPageCount;
        for (var i = 0; i < summaryPageCount; i++)
            output.Pages.MovePage(receiptPageCount + i, i);
        return summaryPageCount;
    }

    private static void AddNavigation(
        PdfDocument output, ExpenseReport report, int summaryPageCount, Dictionary<Guid, int> firstPages)
    {
        foreach (var receipt in report.Receipts)
        {
            output.AddNamedDestination(
                SummaryDocument.DestinationFor(receipt), firstPages[receipt.Id], PdfNamedDestinationParameters.CreateFit());
        }

        output.Outlines.Add("Summary", output.Pages[0], opened: true);
        if (report.Lines.Count > 0)
        {
            var receipts = output.Outlines.Add("Receipts", output.Pages[summaryPageCount], opened: true);
            foreach (var line in report.Lines)
            {
                var title = $"{line.Expense.Date:d} {line.Expense.Description}";
                receipts.Outlines.Add(title, output.Pages[firstPages[line.Receipt.Id] - 1]);
            }
        }
        output.PageMode = PdfPageMode.UseOutlines;
    }

    // Saves to a temporary file first so a failed save never leaves half a report.
    private static string Save(PdfDocument output, ExpenseReport report)
    {
        var folder = FolderFor(report.Profile);
        Directory.CreateDirectory(folder);
        var tempPath = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            output.Save(tempPath);
            var baseName = $"{report.Profile.Name} {report.Year} expenses";
            for (var n = 1; ; n++)
            {
                var path = Path.Combine(folder, n == 1 ? baseName + ".pdf" : $"{baseName} ({n}).pdf");
                try
                {
                    File.Move(tempPath, path, overwrite: true);
                    return path;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && n < MaxFileNameAttempts)
                {
                    // Probably open in a PDF viewer; try the next name.
                }
            }
        }
        finally
        {
            if (File.Exists(tempPath))
                TryDelete(() => File.Delete(tempPath));
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
            TryDelete(() => Directory.Delete(folder, recursive: true));
    }

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
