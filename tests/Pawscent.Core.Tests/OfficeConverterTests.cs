using Pawscent.Core.Reports;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class OfficeConverterTests
{
    private readonly FakeApp _excel = new("Microsoft Excel", ".xlsx");
    private readonly FakeApp _libreOffice = new("LibreOffice", ".xlsx", ".docx");

    [Fact]
    public void ConvertToPdf_UsesFirstInstalledAppForTheFormat()
    {
        var pdf = Converter().ConvertToPdf("bill.xlsx", "work", CancellationToken.None);

        Assert.Equal(Path.Combine("work", "0", "Microsoft Excel.pdf"), pdf);
        Assert.Empty(_libreOffice.Converted);
    }

    [Fact]
    public void ConvertToPdf_FirstAppMissing_UsesNextOne()
    {
        _excel.IsInstalled = false;

        var pdf = Converter().ConvertToPdf("bill.xlsx", "work", CancellationToken.None);

        Assert.Equal(Path.Combine("work", "1", "LibreOffice.pdf"), pdf);
        Assert.Empty(_excel.Converted);
    }

    [Fact]
    public void ConvertToPdf_FormatOnlyOneAppConverts_UsesThatApp()
    {
        Converter().ConvertToPdf("letter.docx", "work", CancellationToken.None);

        Assert.Empty(_excel.Converted);
        Assert.Equal(["letter.docx"], _libreOffice.Converted);
    }

    [Fact]
    public void ConvertToPdf_FirstAppFails_UsesNextOne()
    {
        _excel.Failure = "Excel failed.";

        var pdf = Converter().ConvertToPdf("bill.xlsx", "work", CancellationToken.None);

        Assert.Equal(Path.Combine("work", "1", "LibreOffice.pdf"), pdf);
    }

    [Fact]
    public void ConvertToPdf_AllAppsFail_ReportsFirstFailure()
    {
        _excel.Failure = "Excel failed.";
        _libreOffice.Failure = "LibreOffice failed.";

        var ex = Assert.Throws<ReceiptConversionException>(
            () => Converter().ConvertToPdf("bill.xlsx", "work", CancellationToken.None));

        Assert.Equal("Excel failed.", ex.Message);
    }

    [Fact]
    public void ConvertToPdf_NoAppInstalled_NamesEveryAppThatConvertsTheFormat()
    {
        _excel.IsInstalled = false;
        _libreOffice.IsInstalled = false;

        var sheet = Assert.Throws<ReceiptConversionException>(
            () => Converter().ConvertToPdf("bill.xlsx", "work", CancellationToken.None));
        var document = Assert.Throws<ReceiptConversionException>(
            () => Converter().ConvertToPdf("letter.docx", "work", CancellationToken.None));

        Assert.Equal(
            "Microsoft Excel or LibreOffice is needed to add this file to a report. "
            + "Install one of them and create the report again.",
            sheet.Message);
        Assert.Equal(
            "LibreOffice is needed to add this file to a report. Install LibreOffice and create the report again.",
            document.Message);
    }

    [Fact]
    public void ConvertToPdf_Cancelled_DoesNotTryNextApp()
    {
        _excel.Cancels = true;

        Assert.Throws<OperationCanceledException>(
            () => Converter().ConvertToPdf("bill.xlsx", "work", CancellationToken.None));

        Assert.Empty(_libreOffice.Converted);
    }

    private OfficeConverter Converter() => new([_excel, _libreOffice]);

    private sealed class FakeApp(string name, params string[] extensions) : IOfficeApp
    {
        public string Name => name;

        public bool IsInstalled { get; set; } = true;

        public string? Failure { get; set; }

        public bool Cancels { get; set; }

        public List<string> Converted { get; } = [];

        public bool Converts(string extension) => extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

        public string ConvertToPdf(string sourcePath, string workFolder, CancellationToken cancellationToken)
        {
            Converted.Add(sourcePath);
            if (Cancels)
                throw new OperationCanceledException();
            if (Failure is not null)
                throw new ReceiptConversionException(Failure);
            return Path.Combine(workFolder, name + ".pdf");
        }
    }
}
