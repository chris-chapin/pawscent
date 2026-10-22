using System.Globalization;
using System.Text;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Pawscent.Core.Expenses;
using PdfSharp.Drawing;

namespace Pawscent.Core.Reports;

/// <summary>
/// Lays out the report's first pages, its table of contents: the year tab's expense table, each line linked to its
/// receipt, followed by the totals per category.
/// </summary>
internal static class SummaryDocument
{
    private const double MarginPoints = 40;
    private const double FontSize = 9;
    private const double CellPadding = 4;
    private const string LinkStyle = "Link";

    private static readonly Color HeaderShading = Color.FromRgb(0xEE, 0xEE, 0xEE);
    private static readonly Color RuleColor = Color.FromRgb(0xBB, 0xBB, 0xBB);
    private static readonly Color MutedColor = Color.FromRgb(0x55, 0x55, 0x55);
    private static readonly Color LinkColor = Color.FromRgb(0x00, 0x5A, 0x9E);

    /// <summary>The named destination of a receipt's first page, which the summary's lines link to.</summary>
    public static string DestinationFor(Receipt receipt) => "receipt-" + receipt.Id.ToString("N");

    /// <param name="firstPageOf">The page number, in the finished report, where a receipt starts.</param>
    public static Document Build(ExpenseReport report, ReportPaper paper, DateTimeOffset created, Func<Receipt, int> firstPageOf)
    {
        var document = new Document();
        DefineStyles(document);
        var section = document.AddSection();
        SetUpPage(section.PageSetup, paper);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.Format.Font.Color = MutedColor;
        footer.AddText("Page ");
        footer.AddPageField();

        section.AddParagraph($"{report.Profile.Name} – {report.Year} expenses", "ReportTitle");
        var categories = report.CategoryFilter is null ? "All" : string.Join(", ", report.CategoryFilter);
        section.AddParagraph($"Categories: {categories}");
        section.AddParagraph(
            $"{report.Lines.Count} expense(s), {report.Receipts.Count} receipt(s). Created {created.LocalDateTime:g}.");
        if (report.Lines.Count > 0)
        {
            var hint = section.AddParagraph("Select a line to go to its receipt. Receipts follow this summary in the same order.");
            hint.Format.Font.Color = MutedColor;
        }

        using var measure = XGraphics.CreateMeasureContext(new XSize(2000, 2000), XGraphicsUnit.Point, XPageDirection.Downwards);
        var breaker = new WordBreaker(measure, new XFont(ReportFonts.FamilyName, FontSize));

        section.AddParagraph("Expenses", "SectionTitle");
        AddExpenseTable(section, report, firstPageOf, breaker);
        var total = section.AddParagraph(TotalText(report));
        total.Format.Font.Bold = true;
        total.Format.SpaceBefore = Unit.FromPoint(6);

        section.AddParagraph("By category", "SectionTitle");
        AddCategoryTable(section, report, breaker);
        return document;
    }

    private static void DefineStyles(Document document)
    {
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = ReportFonts.FamilyName;
        normal.Font.Size = FontSize;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);

        var title = document.Styles.AddStyle("ReportTitle", StyleNames.Normal);
        title.Font.Size = 18;
        title.Font.Bold = true;
        title.ParagraphFormat.SpaceAfter = Unit.FromPoint(8);

        var sectionTitle = document.Styles.AddStyle("SectionTitle", StyleNames.Normal);
        sectionTitle.Font.Size = 12;
        sectionTitle.Font.Bold = true;
        sectionTitle.ParagraphFormat.SpaceBefore = Unit.FromPoint(16);
        sectionTitle.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        sectionTitle.ParagraphFormat.KeepWithNext = true;

        var link = document.Styles.AddStyle(LinkStyle, StyleNames.Normal);
        link.Font.Color = LinkColor;
    }

    private static void SetUpPage(PageSetup setup, ReportPaper paper)
    {
        var size = ReceiptPages.PortraitSize(paper);
        setup.PageWidth = Unit.FromPoint(size.Width);
        setup.PageHeight = Unit.FromPoint(size.Height);
        setup.TopMargin = setup.BottomMargin = setup.LeftMargin = setup.RightMargin = Unit.FromPoint(MarginPoints);
        setup.FooterDistance = Unit.FromPoint(24);
    }

    private static double ContentWidth(Section section) =>
        section.PageSetup.PageWidth.Point - section.PageSetup.LeftMargin.Point - section.PageSetup.RightMargin.Point;

    private static void AddExpenseTable(Section section, ExpenseReport report, Func<Receipt, int> firstPageOf, WordBreaker breaker)
    {
        // Date, description, category, receipt, amount, page. The description takes what the others leave.
        double[] fixedWidths = [62, 78, 88, 84, 34];
        var table = CreateTable(section, ContentWidth(section) - fixedWidths.Sum(), fixedWidths, insertAt: 1);
        table.Columns[4].Format.Alignment = ParagraphAlignment.Right;
        table.Columns[5].Format.Alignment = ParagraphAlignment.Right;
        AddHeader(table, "Date", "Description", "Category", "Receipt", "Amount", "Page");

        foreach (var line in report.Lines)
        {
            var row = table.AddRow();
            var destination = DestinationFor(line.Receipt);
            var expense = line.Expense;
            string[] texts =
            [
                expense.Date.ToString("d"),
                expense.Description,
                line.CategoryName,
                Path.GetFileName(line.Receipt.FilePath),
                $"{expense.Amount:N2} {expense.Currency}",
                firstPageOf(line.Receipt).ToString(),
            ];
            // Every cell links to the receipt, so the whole line can be clicked; receipt and page look like links.
            for (var i = 0; i < texts.Length; i++)
            {
                var paragraph = row.Cells[i].AddParagraph();
                if (i is 3 or 5)
                    paragraph.Style = LinkStyle;
                var pieces = breaker.Split(texts[i], table.Columns[i].Width.Point - 2 * CellPadding);
                for (var p = 0; p < pieces.Count; p++)
                {
                    if (p > 0)
                        paragraph.AddLineBreak();
                    paragraph.AddHyperlink(destination, HyperlinkType.Bookmark).AddText(pieces[p]);
                }
            }
        }
    }

    private static void AddCategoryTable(Section section, ExpenseReport report, WordBreaker breaker)
    {
        double[] fixedWidths = [50, 100];
        var table = CreateTable(section, ContentWidth(section) - fixedWidths.Sum(), fixedWidths, insertAt: 0);
        table.Columns[1].Format.Alignment = ParagraphAlignment.Right;
        table.Columns[2].Format.Alignment = ParagraphAlignment.Right;
        AddHeader(table, "Category", "Count", "Total");
        foreach (var total in report.CategoryTotals)
        {
            var row = table.AddRow();
            var name = row.Cells[0].AddParagraph();
            var pieces = breaker.Split(total.CategoryName, table.Columns[0].Width.Point - 2 * CellPadding);
            for (var p = 0; p < pieces.Count; p++)
            {
                if (p > 0)
                    name.AddLineBreak();
                name.AddText(pieces[p]);
            }
            row.Cells[1].AddParagraph(total.Count.ToString());
            row.Cells[2].AddParagraph($"{total.Total:N2} {total.Currency}");
        }
    }

    // A table with one flexible column inserted among fixed-width ones.
    private static Table CreateTable(Section section, double flexibleWidth, double[] fixedWidths, int insertAt)
    {
        var table = section.AddTable();
        table.Borders.Bottom.Width = Unit.FromPoint(0.5);
        table.Borders.Bottom.Color = RuleColor;
        table.TopPadding = table.BottomPadding = Unit.FromPoint(3);
        table.LeftPadding = table.RightPadding = Unit.FromPoint(CellPadding);
        var widths = fixedWidths.ToList();
        widths.Insert(insertAt, flexibleWidth);
        foreach (var width in widths)
            table.AddColumn(Unit.FromPoint(width));
        return table;
    }

    private static void AddHeader(Table table, params string[] titles)
    {
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Shading.Color = HeaderShading;
        for (var i = 0; i < titles.Length; i++)
            header.Cells[i].AddParagraph(titles[i]);
    }

    private static string TotalText(ExpenseReport report) =>
        report.CurrencyTotals.Count == 0
            ? "No expenses shown."
            : "Total: " + string.Join(" + ", report.CurrencyTotals.Select(t => $"{t.Total:N2} {t.Currency}"));

    /// <summary>
    /// MigraDoc wraps text only at spaces, so a word wider than its column, such as a long file name, would run into
    /// the next column. This cuts such words into pieces that fit; the caller puts a line break between pieces.
    /// </summary>
    private sealed class WordBreaker(XGraphics measure, XFont font)
    {
        public IReadOnlyList<string> Split(string text, double width)
        {
            var pieces = new List<string>();
            var current = new StringBuilder();
            var words = text.Split(' ');
            for (var w = 0; w < words.Length; w++)
            {
                if (w > 0)
                    current.Append(' ');
                var word = words[w];
                if (Width(word) <= width)
                {
                    current.Append(word);
                    continue;
                }
                var start = 0;
                while (start < word.Length)
                {
                    var length = FittingLength(word, start, width);
                    current.Append(word, start, length);
                    start += length;
                    if (start < word.Length)
                    {
                        pieces.Add(current.ToString());
                        current.Clear();
                    }
                }
            }
            pieces.Add(current.ToString());
            return pieces;
        }

        // The longest run of whole characters from start that fits, but at least one character.
        private int FittingLength(string word, int start, double width)
        {
            var length = StringInfo.GetNextTextElementLength(word, start);
            while (start + length < word.Length)
            {
                var next = length + StringInfo.GetNextTextElementLength(word, start + length);
                if (Width(word.Substring(start, next)) > width)
                    break;
                length = next;
            }
            return length;
        }

        private double Width(string text) => measure.MeasureString(text, font).Width;
    }
}
