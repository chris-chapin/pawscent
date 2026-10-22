using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Pawscent.Core.Reports;

/// <summary>Turns receipt files into report pages: PDFs as they are, images one per page, other formats via LibreOffice.</summary>
internal static class ReceiptPages
{
    private const double Margin = 36;

    // Formats PDFsharp can draw itself.
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp",
    };

    // Formats converted to PDF by LibreOffice first.
    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xlsm", ".xls", ".ods", ".csv", ".docx", ".doc", ".odt", ".rtf", ".gif", ".tif", ".tiff", ".webp",
    };

    /// <summary>Appends the receipt's pages to <paramref name="output"/>.</summary>
    /// <remarks>On an exception some of the receipt's pages may already have been added; the caller removes them.</remarks>
    /// <exception cref="ReceiptConversionException">The file type is not supported or could not be converted.</exception>
    public static void Append(
        PdfDocument output,
        string filePath,
        ReportPaper paper,
        IOfficeConverter officeConverter,
        string workFolder,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The receipt file was not found.", filePath);

        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            AppendPdf(output, filePath);
        else if (ImageExtensions.Contains(extension))
            AppendImage(output, filePath, paper);
        else if (OfficeExtensions.Contains(extension))
            AppendPdf(output, officeConverter.ConvertToPdf(filePath, workFolder, cancellationToken));
        else
            throw new ReceiptConversionException("This type of file can't be added to a report. Use PDF, JPG, PNG or XLSX receipts.");
    }

    /// <summary>Appends a page saying the receipt is missing from the report, and why.</summary>
    public static void AppendNotice(PdfDocument output, string fileName, string reason, ReportPaper paper)
    {
        var page = AddPage(output, paper, landscape: false);
        using var gfx = XGraphics.FromPdfPage(page);
        var width = page.Width.Point - 2 * Margin;
        var title = new XFont(ReportFonts.FamilyName, 16, XFontStyleEx.Bold);
        var body = new XFont(ReportFonts.FamilyName, 11, XFontStyleEx.Regular);
        gfx.DrawString("Receipt not included", title, XBrushes.Black, new XRect(Margin, Margin, width, 24), XStringFormats.TopLeft);
        new XTextFormatter(gfx).DrawString(
            $"{fileName}\n\n{reason}",
            body,
            XBrushes.Black,
            new XRect(Margin, Margin + 36, width, page.Height.Point - 2 * Margin - 36),
            XStringFormats.TopLeft);
    }

    /// <summary>Width and height in points of an upright page.</summary>
    public static XSize PortraitSize(ReportPaper paper) =>
        paper == ReportPaper.Letter ? new XSize(612, 792) : new XSize(595.276, 841.89);

    private static void AppendPdf(PdfDocument output, string path)
    {
        using var source = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        if (source.PageCount == 0)
            throw new InvalidDataException("The PDF has no pages.");
        foreach (var page in source.Pages)
            output.AddPage(page);
    }

    // One page per image, turned to match the image, which is shrunk to fit but never enlarged.
    private static void AppendImage(PdfDocument output, string path, ReportPaper paper)
    {
        using var image = XImage.FromFile(path);
        var extension = Path.GetExtension(path);
        var orientation = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                          || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? JpegOrientation.Read(path)
            : JpegOrientation.Upright;
        // EXIF orientations 5 to 8 turn the image a quarter, swapping its width and height.
        var turned = orientation >= 5;
        var width = turned ? image.PointHeight : image.PointWidth;
        var height = turned ? image.PointWidth : image.PointHeight;
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("The image is empty.");

        var page = AddPage(output, paper, landscape: width > height);
        var area = new XRect(Margin, Margin, page.Width.Point - 2 * Margin, page.Height.Point - 2 * Margin);
        var scale = Math.Min(1, Math.Min(area.Width / width, area.Height / height));
        var box = new XRect(area.X + (area.Width - width * scale) / 2, area.Y, width * scale, height * scale);
        using var gfx = XGraphics.FromPdfPage(page);
        DrawUpright(gfx, image, box, orientation);
    }

    // Draws the image into box as EXIF orientation says it should be seen. Transforms apply last-call-first.
    private static void DrawUpright(XGraphics gfx, XImage image, XRect box, int orientation)
    {
        var state = gfx.Save();
        gfx.TranslateTransform(box.X + box.Width / 2, box.Y + box.Height / 2);
        switch (orientation)
        {
            case 2:
                gfx.ScaleTransform(-1, 1);
                break;
            case 3:
                gfx.RotateTransform(180);
                break;
            case 4:
                gfx.ScaleTransform(1, -1);
                break;
            case 5:
                gfx.ScaleTransform(-1, 1);
                gfx.RotateTransform(90);
                break;
            case 6:
                gfx.RotateTransform(90);
                break;
            case 7:
                gfx.ScaleTransform(1, -1);
                gfx.RotateTransform(90);
                break;
            case 8:
                gfx.RotateTransform(-90);
                break;
        }
        var (width, height) = orientation >= 5 ? (box.Height, box.Width) : (box.Width, box.Height);
        gfx.DrawImage(image, -width / 2, -height / 2, width, height);
        gfx.Restore(state);
    }

    private static PdfPage AddPage(PdfDocument output, ReportPaper paper, bool landscape)
    {
        var size = PortraitSize(paper);
        var page = output.AddPage();
        page.Width = XUnit.FromPoint(landscape ? size.Height : size.Width);
        page.Height = XUnit.FromPoint(landscape ? size.Width : size.Height);
        return page;
    }
}
