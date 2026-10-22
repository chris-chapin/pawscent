using Microsoft.Extensions.Time.Testing;
using Pawscent.Core.Expenses;
using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;
using Pawscent.Core.Reports;
using Pawscent.Core.Storage;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Xunit;

namespace Pawscent.Core.Tests;

public sealed class ExpenseReportGeneratorTests : IDisposable
{
    // 16x8 pixels, stored wider than tall.
    private const string LandscapePng =
        "iVBORw0KGgoAAAANSUhEUgAAABAAAAAICAIAAAB/FOjAAAAAE0lEQVR42mP8z0AaYGIY1UADDQCTUAEPbN5GZwAAAABJRU5ErkJggg==";

    // 16x8 pixels, stored wider than tall, with EXIF orientation 6: shown turned a quarter clockwise, so taller than wide.
    private const string TurnedJpeg =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/4QAiRXhpZgAATU0AKgAAAAgAAQESAAMAAAABAAYAAAAAAAD/2wBDABALDA4MChAODQ4SERATGCgaGBYWGDEjJR0oOjM9PDkzODdASFxOQERXRTc4UG1RV19iZ2hnPk1xeXBkeFxlZ2P/2wBDARESEhgVGC8aGi9jQjhCY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjY2P/wAARCAAIABADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDFoooryz7w/9k=";

    private static readonly (double Width, double Height) Letter = (612, 792);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pawscent-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _outputFolder;
    private readonly string _outputPath;
    private readonly AppPaths _paths;
    private readonly ProfileDocument _document;
    private readonly Category _vet;
    private readonly FakeConverter _converter = new();
    private readonly ExpenseReportGenerator _generator;

    public ExpenseReportGeneratorTests()
    {
        _paths = new AppPaths(Path.Combine(_root, "app"));
        // Where the user chose to save, outside the app folder.
        _outputFolder = Path.Combine(_root, "Documents");
        Directory.CreateDirectory(_outputFolder);
        _outputPath = Path.Combine(_outputFolder, "report.pdf");
        var service = new ProfileService(_paths);
        service.Create("Home");
        _document = service.Open("Home");
        _vet = _document.Categories.Create("Vet");
        _generator = new ExpenseReportGenerator(
            _paths, _converter, new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 9, 30, 0, TimeSpan.Zero)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Generate_PutsSummaryFirstThenEachReceiptOnceInOrderOfFirstUse()
    {
        var single = Receipt(WritePdf("single.pdf", 301));
        var twoPages = Receipt(WritePdf("two.pdf", 302, 303));
        Add(1, twoPages);
        Add(2, single);
        Add(3, twoPages);

        var result = Generate();

        var pdf = ReportPdf.Open(result.FilePath);
        Assert.Equal([Letter, (302, 500), (303, 500), (301, 500)], pdf.PageSizes);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Generate_LinksEverySummaryLineToItsReceiptsFirstPage()
    {
        var single = Receipt(WritePdf("single.pdf", 301));
        var twoPages = Receipt(WritePdf("two.pdf", 302, 303));
        Add(1, twoPages);
        Add(2, single);

        var pdf = ReportPdf.Open(Generate().FilePath);

        Assert.Equal(2, pdf.NamedDestinations.Count);
        Assert.Equal([2, 4], pdf.LinkTargets[0].Distinct().Order());
        Assert.All(pdf.LinkTargets.Skip(1), links => Assert.Empty(links));
    }

    [Fact]
    public void Generate_LongSummary_ShiftsReceiptPagesByItsLength()
    {
        var first = Receipt(WritePdf("first.pdf", 301));
        var second = Receipt(WritePdf("second.pdf", 302));
        for (var i = 0; i < 150; i++)
            Add(1, first);
        Add(2, second);

        var pdf = ReportPdf.Open(Generate().FilePath);

        var summaryPages = pdf.PageSizes.TakeWhile(s => s == Letter).Count();
        Assert.True(summaryPages >= 3, $"Expected a summary of several pages, got {summaryPages}.");
        Assert.Equal(summaryPages + 2, pdf.PageCount);
        Assert.Equal((301, 500), pdf.PageSizes[summaryPages]);
        Assert.Equal(
            [summaryPages + 1, summaryPages + 2],
            pdf.LinkTargets.Take(summaryPages).SelectMany(l => l).Distinct().Order());
    }

    [Fact]
    public void Generate_AddsBookmarksForSummaryAndEachExpense()
    {
        var single = Receipt(WritePdf("single.pdf", 301));
        var twoPages = Receipt(WritePdf("two.pdf", 302, 303));
        Add(1, twoPages, "Checkup");
        Add(2, single, "Shots");
        Add(3, twoPages, "Follow-up");

        var pdf = ReportPdf.Open(Generate().FilePath);

        Assert.Equal(
            [
                ("Summary", 0, 1),
                ("Receipts", 0, 2),
                ($"{new DateOnly(2026, 3, 1):d} Checkup", 1, 2),
                ($"{new DateOnly(2026, 3, 2):d} Shots", 1, 4),
                ($"{new DateOnly(2026, 3, 3):d} Follow-up", 1, 2),
            ],
            pdf.Bookmarks);
    }

    [Fact]
    public void Generate_ReceiptThatCannotBeRead_GetsNoticePageAndIssue()
    {
        var missing = Receipt(Path.Combine(ReceiptFiles.FolderFor(_document.Profile), "gone.pdf"));
        var damaged = Receipt(WriteFile("damaged.pdf", "%PDF-1.4 not really"));
        var text = Receipt(WriteFile("notes.txt", "hello"));
        var fine = Receipt(WritePdf("fine.pdf", 301));
        Add(1, missing);
        Add(2, damaged);
        Add(3, text);
        Add(4, fine);

        var result = Generate();

        Assert.Equal(["gone.pdf", "damaged.pdf", "notes.txt"], result.Issues.Select(i => i.FileName));
        Assert.Equal("The receipt file was not found.", result.Issues[0].Reason);
        var pdf = ReportPdf.Open(result.FilePath);
        Assert.Equal([Letter, Letter, Letter, Letter, (301, 500)], pdf.PageSizes);
        Assert.Equal([2, 3, 4, 5], pdf.LinkTargets[0].Distinct().Order());
    }

    [Fact]
    public void Generate_OfficeFile_IsConvertedToPdfFirst()
    {
        var sheet = Receipt(WriteFile("bill.xlsx", "spreadsheet"));
        Add(1, sheet);
        _converter.PageWidths = [311, 312];

        var result = Generate();

        Assert.Equal([sheet.FilePath], _converter.Converted);
        Assert.Equal([Letter, (311, 500), (312, 500)], ReportPdf.Open(result.FilePath).PageSizes);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_paths.TempRoot));
    }

    [Fact]
    public void Generate_ConversionFails_GetsNoticePageWithReason()
    {
        Add(1, Receipt(WriteFile("bill.xlsx", "spreadsheet")));
        _converter.Failure = "LibreOffice is needed.";

        var result = Generate();

        Assert.Equal([new ReportIssue("bill.xlsx", "LibreOffice is needed.")], result.Issues);
        Assert.Equal([Letter, Letter], ReportPdf.Open(result.FilePath).PageSizes);
    }

    [Fact]
    public void Generate_ImagePage_IsTurnedToMatchTheImageAsShown()
    {
        Add(1, Receipt(WriteBytes("wide.png", Convert.FromBase64String(LandscapePng))));
        Add(2, Receipt(WriteBytes("photo.jpg", Convert.FromBase64String(TurnedJpeg))));

        var result = Generate();

        Assert.Empty(result.Issues);
        Assert.Equal([Letter, (792, 612), Letter], ReportPdf.Open(result.FilePath).PageSizes);
    }

    [Fact]
    public void Generate_UsesChosenPaperForSummary()
    {
        Add(1, Receipt(WritePdf("single.pdf", 301)));

        var result = _generator.Generate(ExpenseReport.ForYear(_document, 2026), ReportPaper.A4, _outputPath);

        var (width, height) = ReportPdf.Open(result.FilePath).PageSizes[0];
        Assert.Equal(595.3, width, precision: 1);
        Assert.Equal(841.9, height, precision: 1);
    }

    [Fact]
    public void Generate_WritesChosenFileReplacingWhatWasThere()
    {
        Add(1, Receipt(WritePdf("single.pdf", 301)));
        File.WriteAllText(_outputPath, "an older file");

        var result = Generate();

        Assert.Equal(_outputPath, result.FilePath);
        Assert.Equal([Letter, (301, 500)], ReportPdf.Open(_outputPath).PageSizes);
        Assert.Equal([_outputPath], Directory.EnumerateFileSystemEntries(_outputFolder));
    }

    [Fact]
    public void Generate_FolderMissing_ThrowsIOExceptionNamingTheFile()
    {
        Add(1, Receipt(WritePdf("single.pdf", 301)));
        var path = Path.Combine(_outputFolder, "gone", "report.pdf");

        var ex = Assert.Throws<IOException>(() =>
            _generator.Generate(ExpenseReport.ForYear(_document, 2026), ReportPaper.Letter, path));

        Assert.StartsWith("\"report.pdf\" could not be saved.", ex.Message);
    }

    [Fact]
    public void Generate_Fails_RemovesEmptyFileButKeepsExistingReport()
    {
        Add(1, Receipt(WritePdf("single.pdf", 301)));
        var empty = Path.Combine(_outputFolder, "empty.pdf");
        File.WriteAllText(empty, string.Empty);
        File.WriteAllText(_outputPath, "an older report");
        var cancelled = new CancellationToken(canceled: true);
        var report = ExpenseReport.ForYear(_document, 2026);

        Assert.Throws<OperationCanceledException>(() => _generator.Generate(report, ReportPaper.Letter, empty, cancelled));
        Assert.Throws<OperationCanceledException>(() => _generator.Generate(report, ReportPaper.Letter, _outputPath, cancelled));

        Assert.False(File.Exists(empty));
        Assert.Equal("an older report", File.ReadAllText(_outputPath));
    }

    [Fact]
    public void SuggestedName_IsProfileYearAndExpenses() =>
        Assert.Equal("Home 2026 expenses", ExpenseReportGenerator.SuggestedName(_document.Profile, 2026));

    [Theory]
    [InlineData("US", ReportPaper.Letter)]
    [InlineData("ca", ReportPaper.Letter)]
    [InlineData("DE", ReportPaper.A4)]
    [InlineData("GB", ReportPaper.A4)]
    public void ReportPapers_ForRegion_UsesLetterOnlyWhereItIsCommon(string region, ReportPaper expected) =>
        Assert.Equal(expected, ReportPapers.ForRegion(region));

    [LibreOfficeFact]
    public void LibreOffice_ConvertsXlsxToPdf()
    {
        var workFolder = Path.Combine(_paths.TempRoot, "work");

        var pdfPath = new LibreOfficeConverter(_paths).ConvertToPdf(WriteXlsx("bill.xlsx"), workFolder, CancellationToken.None);

        Assert.StartsWith(workFolder, pdfPath);
        using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount >= 1);
    }

    private ReportResult Generate() =>
        _generator.Generate(ExpenseReport.ForYear(_document, 2026), ReportPaper.Letter, _outputPath);

    private Expense Add(int day, Receipt receipt, string description = "Item") =>
        _document.Expenses.Create(new DateOnly(2026, 3, day), 10m, "USD", description, _vet.Id, receipt.Id);

    private Receipt Receipt(string path) => _document.Receipts.Create(path);

    private string WritePdf(string name, params double[] pageWidths)
    {
        var path = PathFor(name);
        using var pdf = new PdfDocument();
        foreach (var width in pageWidths)
        {
            var page = pdf.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(500);
        }
        pdf.Save(path);
        return path;
    }

    private string WriteFile(string name, string contents)
    {
        var path = PathFor(name);
        File.WriteAllText(path, contents);
        return path;
    }

    private string WriteBytes(string name, byte[] contents)
    {
        var path = PathFor(name);
        File.WriteAllBytes(path, contents);
        return path;
    }

    // The smallest workbook Excel and LibreOffice open: one sheet with a text and a number cell.
    private string WriteXlsx(string name)
    {
        var path = PathFor(name);
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
        void Add(string entry, string xml)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + xml);
        }
        const string package = "http://schemas.openxmlformats.org/package/2006";
        const string office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Add("[Content_Types].xml",
            $"<Types xmlns=\"{package}/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "</Types>");
        Add("_rels/.rels",
            $"<Relationships xmlns=\"{package}/relationships\">" +
            $"<Relationship Id=\"rId1\" Type=\"{office}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Add("xl/workbook.xml",
            $"<workbook xmlns=\"{sheet}\" xmlns:r=\"{office}\"><sheets><sheet name=\"Bill\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add("xl/_rels/workbook.xml.rels",
            $"<Relationships xmlns=\"{package}/relationships\">" +
            $"<Relationship Id=\"rId1\" Type=\"{office}/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
        Add("xl/worksheets/sheet1.xml",
            $"<worksheet xmlns=\"{sheet}\"><sheetData><row r=\"1\">" +
            "<c r=\"A1\" t=\"inlineStr\"><is><t>Kibble</t></is></c><c r=\"B1\"><v>42.5</v></c>" +
            "</row></sheetData></worksheet>");
        return path;
    }

    private string PathFor(string name)
    {
        var folder = ReceiptFiles.FolderFor(_document.Profile);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, name);
    }

    private sealed class FakeConverter : IOfficeConverter
    {
        public List<string> Converted { get; } = [];

        public double[] PageWidths { get; set; } = [300];

        public string? Failure { get; set; }

        public string ConvertToPdf(string sourcePath, string workFolder, CancellationToken cancellationToken)
        {
            Converted.Add(sourcePath);
            if (Failure is not null)
                throw new ReceiptConversionException(Failure);
            Directory.CreateDirectory(workFolder);
            var path = Path.Combine(workFolder, "converted.pdf");
            using var pdf = new PdfDocument();
            foreach (var width in PageWidths)
            {
                var page = pdf.AddPage();
                page.Width = XUnit.FromPoint(width);
                page.Height = XUnit.FromPoint(500);
            }
            pdf.Save(path);
            return path;
        }
    }
}

/// <summary>A test that runs only where LibreOffice is installed, and shows as skipped elsewhere.</summary>
public sealed class LibreOfficeFactAttribute : FactAttribute
{
    public LibreOfficeFactAttribute()
    {
        if (LibreOfficeConverter.FindExecutable() is null)
            Skip = "LibreOffice is not installed.";
    }
}
