using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace Pawscent.Core.Tests;

/// <summary>Reads back the parts of a generated report the tests check: page sizes, links and bookmarks.</summary>
internal sealed class ReportPdf
{
    private ReportPdf(PdfDocument document)
    {
        PageSizes = document.Pages.Cast<PdfPage>().Select(p => (p.Width.Point, p.Height.Point)).ToList();
        var pageNumbers = document.Pages.Cast<PdfPage>()
            .Select((p, i) => (p.Reference!, Number: i + 1))
            .ToDictionary(p => p.Item1, p => p.Number);
        int PageOf(PdfArray destination) => pageNumbers[destination.Elements.GetReference(0)!];

        var catalog = document.Internals.Catalog;
        var destinations = new Dictionary<string, int>();
        var tree = catalog.Elements.GetDictionary("/Names")?.Elements.GetDictionary("/Dests");
        if (tree is not null)
            CollectNames(tree, (name, value) => destinations[name] = PageOf((PdfArray)value));
        NamedDestinations = destinations;

        LinkTargets = document.Pages.Cast<PdfPage>()
            .Select(page => (IReadOnlyList<int>)page.Annotations.Cast<PdfDictionary>()
                .Select(a => destinations[a.Elements.GetDictionary("/A")!.Elements.GetString("/D")])
                .ToList())
            .ToList();

        var bookmarks = new List<(string, int, int)>();
        CollectOutlines(catalog.Elements.GetDictionary("/Outlines"), 0, bookmarks, PageOf);
        Bookmarks = bookmarks;
    }

    public IReadOnlyList<(double Width, double Height)> PageSizes { get; }

    public int PageCount => PageSizes.Count;

    public IReadOnlyDictionary<string, int> NamedDestinations { get; }

    /// <summary>Per page, the page number each of its links goes to.</summary>
    public IReadOnlyList<IReadOnlyList<int>> LinkTargets { get; }

    /// <summary>Bookmarks in order, with their nesting depth (0 = top level) and target page number.</summary>
    public IReadOnlyList<(string Title, int Depth, int Page)> Bookmarks { get; }

    public static ReportPdf Open(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return new ReportPdf(document);
    }

    private static void CollectNames(PdfDictionary node, Action<string, PdfItem> add)
    {
        var names = node.Elements.GetArray("/Names");
        for (var i = 0; names is not null && i + 1 < names.Elements.Count; i += 2)
            add(names.Elements.GetString(i), Resolve(names.Elements[i + 1]));
        var kids = node.Elements.GetArray("/Kids");
        for (var i = 0; kids is not null && i < kids.Elements.Count; i++)
            CollectNames((PdfDictionary)Resolve(kids.Elements[i]), add);
    }

    private static void CollectOutlines(
        PdfDictionary? parent, int depth, List<(string, int, int)> into, Func<PdfArray, int> pageOf)
    {
        for (var item = parent?.Elements.GetDictionary("/First"); item is not null; item = item.Elements.GetDictionary("/Next"))
        {
            into.Add((item.Elements.GetString("/Title"), depth, pageOf(item.Elements.GetArray("/Dest")!)));
            CollectOutlines(item, depth + 1, into, pageOf);
        }
    }

    private static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;
}
