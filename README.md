# Pawscent

Windows 11 desktop app (WinUI 3, .NET 10).

## Principles

- **Local only.** The app reads and writes only under `%LOCALAPPDATA%\Pawscent`. It makes no network
  connections and references no networking packages; the Store manifest declares no `internetClient` capability.
- **No installer required.** The default build is a self-contained folder: copy it anywhere and run `Pawscent.exe`.
  .NET and the Windows App SDK are bundled.
- **Store-ready.** `Package.appxmanifest` and a `Store` publish profile are kept so an MSIX can be produced later.

## Data layout

```
%LOCALAPPDATA%\Pawscent\
  Profiles\
    <ProfileName>\        one folder per profile (workspace)
      profile.json        header (name, creation time, schema version), then categories, receipts, expenses
      Receipts\          copies of receipt files picked from outside %LOCALAPPDATA%\Pawscent
```

`File > New > Profile` creates a profile folder; `File > Load > Profile` opens an existing one.

An open profile shows these tabs:

- **One tab per year** (every year with expenses, plus the current year; `+` adds an empty year). Add, edit and
  delete expenses; the add/edit dialog picks or creates the category and receipt. The expense table is sorted oldest
  to newest, and a second table totals the shown expenses per category and currency. Both tables can be filtered to
  selected categories.
- **Categories**: add, rename and delete categories. A category used by an expense cannot be deleted.
- **Receipts**: receipt files listed by file name. A file added from outside the app folder is copied into the
  profile's `Receipts` folder first. Deleting a receipt also deletes that copy. A receipt used by an expense cannot be
  deleted.

Changes to categories, receipts and expenses are saved automatically 3 seconds after the last edit, and also
when switching profiles or closing the app. Saves write `profile.json.tmp` first and then rename it over
`profile.json`, so an interrupted save never leaves a half-written file.

## Projects

| Path | Purpose |
| --- | --- |
| `src/Pawscent.App` | WinUI 3 UI (MVVM via CommunityToolkit.Mvvm) |
| `src/Pawscent.Core` | UI-free logic: paths, profile storage, expenses, summaries, receipt files |
| `tests/Pawscent.Core.Tests` | xUnit tests for Core |

## Build

Requires Windows 10/11 with the .NET 10 SDK (Visual Studio 2026 with the WinUI workload is optional).

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
