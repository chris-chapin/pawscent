# Pawscent

Windows 11 desktop app (WinUI 3, .NET 10).

## Principles

- **Local only.** Pawscent works without an Internet connection and never stores files in the cloud. It makes no
  network connections and references no networking or cloud packages; the Store manifest declares no `internetClient`
  capability. Its own data lives on this PC under `%LOCALAPPDATA%\Pawscent`; other local files are used when you pick
  them (receipts to add, the PDF report to save).
- **No installer required.** The default build is a self-contained folder: copy it anywhere and run `Pawscent.exe`.
  .NET and the Windows App SDK are bundled.
- **Store-ready.** `Package.appxmanifest` and a `Store` publish profile are kept so an MSIX can be produced later.

## Data layout

```
%LOCALAPPDATA%\Pawscent\
  Profiles\
    <ProfileName>\        one folder per profile (workspace)
      profile.json        header (name, creation time, schema version), then categories, receipts, expenses
      Receipts\          copies of the profile's receipt files
  Temp\                  scratch files while a report is built (deleted afterwards)
  LibreOffice\           LibreOffice's settings while it converts receipts for a report
```

`File > New > Profile` creates a profile folder; `File > Load > Profile` opens an existing one.

An open profile shows these tabs:

- **One tab per year** (every year with expenses, plus the current year; `+` adds an empty year). Add, edit and
  delete expenses; the add/edit dialog picks or creates the category and receipt. The expense table is sorted oldest
  to newest, and a second table totals the shown expenses per category and currency. Both tables can be filtered to
  selected categories. **Create report...** saves a PDF of the expenses shown (see [Reports](#reports)).
- **Categories**: add, rename and delete categories. A category used by an expense cannot be deleted.
- **Receipts**: receipt files listed by file name. Every file added (here or in the expense dialog) is copied into
  the profile's `Receipts` folder first, so the profile never depends on the original; a file already in that folder
  is used as is. Deleting a receipt also deletes that copy. A receipt used by an expense cannot be deleted.

Changes to categories, receipts and expenses are saved automatically 3 seconds after the last edit, and also
when switching profiles or closing the app. Saves write `profile.json.tmp` first and then rename it over
`profile.json`, so an interrupted save never leaves a half-written file.

## Reports

**Create report...** on a year tab asks where to save the PDF (suggesting `Documents\<ProfileName> <year>
expenses.pdf`), builds a PDF of the expenses that tab shows, with the same category filter, and opens it in the
default PDF viewer. Saving over an existing file replaces it; if that file is open in another app, Pawscent says so and
the old file is kept.

- **Summary first.** The first pages repeat the year tab: the expense table and the totals per category. It works as
  the table of contents: each line names the page its receipt starts on, and clicking the line jumps there.
- **Receipts follow,** each once, in the order the summary first lists them (several expenses can share a receipt).
  PDFs are added as they are; JPG, PNG and BMP images get a page each, turned to match the photo's EXIF orientation.
- **Spreadsheets** (XLSX, XLSM, XLS, ODS, CSV) are converted to PDF by Microsoft Excel if it is installed, otherwise
  by [LibreOffice](https://www.libreoffice.org). **Other formats** (DOC, DOCX, ODT, RTF, GIF, TIFF, WebP) need
  LibreOffice. If the first program fails on a file, the next one installed is tried. Neither program is part of
  Pawscent and both are optional: without them, or for a file that can't be read (missing, damaged,
  password-protected, unsupported type), the report gets a page saying the receipt is not included and naming what
  to install, and Pawscent lists those receipts after creating the report.
- **Bookmarks** (PDF outline): *Summary*, then *Receipts* with one entry per expense.
- Paper is US Letter where that is the usual size (US, Canada, Mexico and a few others), otherwise A4.

Reports use the Windows font Segoe UI (Arial if missing). Excel runs as a hidden instance of its own (through COM
automation) that opens the receipt read-only with macros and external links off, exports every sheet with the
workbook's print settings, and quits. LibreOffice runs headless and offline as a separate process, with its own settings in
`%LOCALAPPDATA%\Pawscent\LibreOffice`, apart from yours. PDFs are built with PDFsharp and MigraDoc **6.2.4**, the last
MIT-licensed release (6.5 and later are AGPL-3.0), so the version is pinned.

## Projects

| Path | Purpose |
| --- | --- |
| `src/Pawscent.App` | WinUI 3 UI (MVVM via CommunityToolkit.Mvvm) |
| `src/Pawscent.Core` | UI-free logic: paths, profile storage, expenses, summaries, receipt files, PDF reports |
| `tests/Pawscent.Core.Tests` | xUnit tests for Core |

## Build

Requires Windows 10/11 with the .NET 10 SDK (Visual Studio 2026 with the WinUI workload is optional). The tests that
convert an XLSX file with Excel or LibreOffice run only where that program is installed and are reported as skipped
elsewhere.

```powershell
dotnet test tests/Pawscent.Core.Tests

# Portable folder -> publish\win-x64\Pawscent.exe
dotnet publish src/Pawscent.App -p:PublishProfile=win-x64
dotnet publish src/Pawscent.App -p:PublishProfile=win-arm64

# Microsoft Store MSIX -> publish\store\
dotnet publish src/Pawscent.App -p:PublishProfile=Store -p:WindowsPackageType=MSIX
```

Before a Store submission, replace the placeholder images in `src/Pawscent.App/Assets` and update the
`Identity` in `Package.appxmanifest` with the values from Partner Center.
