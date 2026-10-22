using PdfSharp.Fonts;

namespace Pawscent.Core.Reports;

/// <summary>
/// Supplies PDFsharp with one sans-serif font from the operating system's font folder. Reports ask for
/// <see cref="FamilyName"/>; every family name resolves to the same font.
/// </summary>
internal sealed class ReportFonts : IFontResolver
{
    public const string FamilyName = "Pawscent Sans";

    private const string RegularFace = "regular";
    private const string BoldFace = "bold";

    // Regular and bold file of each candidate, best first. DejaVu and Liberation cover Linux, where the tests also run.
    private static readonly (string Folder, string Regular, string Bold)[] Candidates =
    [
        (Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf", "segoeuib.ttf"),
        (Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf", "arialbd.ttf"),
        ("/usr/share/fonts/truetype/dejavu", "DejaVuSans.ttf", "DejaVuSans-Bold.ttf"),
        ("/usr/share/fonts/truetype/liberation", "LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf"),
    ];

    private static readonly Lock RegisterLock = new();

    private readonly string _regularPath;
    private readonly string? _boldPath;

    private ReportFonts(string regularPath, string? boldPath)
    {
        _regularPath = regularPath;
        _boldPath = boldPath;
    }

    /// <summary>Makes PDFsharp use these fonts. PDFsharp's font settings are global, so this runs once per process.</summary>
    /// <exception cref="InvalidOperationException">No usable font file was found.</exception>
    public static void EnsureRegistered()
    {
        lock (RegisterLock)
        {
            if (GlobalFontSettings.FontResolver is ReportFonts)
                return;
            foreach (var (folder, regular, bold) in Candidates)
            {
                if (folder.Length == 0 || !File.Exists(Path.Combine(folder, regular)))
                    continue;
                var boldPath = Path.Combine(folder, bold);
                GlobalFontSettings.FontResolver = new ReportFonts(
                    Path.Combine(folder, regular), File.Exists(boldPath) ? boldPath : null);
                return;
            }
            throw new InvalidOperationException("No font for the report was found. Segoe UI or Arial must be installed.");
        }
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        isBold && _boldPath is not null
            ? new FontResolverInfo(BoldFace, mustSimulateBold: false, mustSimulateItalic: isItalic)
            : new FontResolverInfo(RegularFace, mustSimulateBold: isBold, mustSimulateItalic: isItalic);

    public byte[] GetFont(string faceName) => File.ReadAllBytes(faceName == BoldFace ? _boldPath! : _regularPath);
}
