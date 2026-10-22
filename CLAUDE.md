# Pawscent

Windows 11 desktop app. WinUI 3 + .NET 10.

## Requirements (do not violate)
- **Local only.** Read/write only under `%LOCALAPPDATA%\Pawscent` (via `AppPaths`), except files the user picks in a file dialog (receipt import, report Save As). No network access, no HTTP/cloud packages, no `internetClient` capability.
- **Standalone exe by default.** Unpackaged (`WindowsPackageType=None`), self-contained .NET + Windows App SDK; `dotnet publish src/Pawscent.App -p:PublishProfile=win-x64` → `publish\win-x64\Pawscent.exe`, runs with no installs.
- **Keep Store publishing possible.** Don't remove `Package.appxmanifest`, `Assets/`, or the `Store` publish profile.

## Boilerplate decisions
- `net10.0-windows10.0.19041.0`, Windows App SDK 1.8, x64 + ARM64.
- MVVM with CommunityToolkit.Mvvm; DI via Microsoft.Extensions.DependencyInjection in `App.xaml.cs`.
- UI-free logic goes in `src/Pawscent.Core` (plain `net10.0`, unit-tested in `tests/Pawscent.Core.Tests`); WinUI code in `src/Pawscent.App`. View models reach UI only through `IDialogService`.
- Avoid `[ObservableProperty]` partial properties (generator failed, CS9248); write observable properties by hand with `SetProperty(ref field, value)`.
- `TreatWarningsAsErrors` is on (Directory.Build.props).
- Profiles = workspace folders under `Profiles\<Name>\` with `profile.json`.
- PDF reports: PDFsharp + MigraDoc pinned to **6.2.4** (last MIT release; 6.5+ is AGPL, don't upgrade). XLSX and other
  office files go through an installed LibreOffice (`soffice --headless`, optional, profile under `AppPaths.LibreOfficeProfile`).
- WinUI can't build on Linux; CI (`.github/workflows/build.yml`, windows-latest) is the build check.
