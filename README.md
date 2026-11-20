# Fuel Assistance Intake

Staff app for NEKCA Crisis Fuel / Electric assistance intake. Staff enter each client's
information once; the app calculates FPL and produces the print-ready forms and packets.

It replaces the Excel intake workbook and its PowerShell scripts, in two phases:

1. **Desktop app** (Windows, WPF + Blazor). Saves to the existing OneDrive-synced
   SharePoint client folders. No install or admin rights needed.
2. **Web app** (Blazor). Same screens, connected directly to SharePoint, Microsoft Lists
   and Microsoft 365 sign-in.

GoEngage stays the official system of record. This app is the staff worksheet and form printer.

See [PROJECT_NOTES.md](PROJECT_NOTES.md) for decisions and rules.

## Solution layout

| Project | What it is |
| --- | --- |
| `src/FuelIntake.Core` | Data model, validation, FPL, GoEngage mappings, interfaces. No UI, no Windows code. |
| `src/FuelIntake.UI` | The screens, as Razor components shared by both hosts. |
| `src/FuelIntake.Desktop` | Phase 1 host: WPF window showing the UI through BlazorWebView (WebView2). |
| `tests/FuelIntake.Core.Tests` | Automated tests for Core. |
| `reference/workbook` | The Excel workbook generator and scripts this app replaces (for porting). |

## Getting the app

**Newest approved version** (everything merged into `main`):
[Download FuelIntake-win-x64.zip](https://github.com/Nerves82/fuel-intake-app/releases/latest/download/FuelIntake-win-x64.zip)
· [All releases](https://github.com/Nerves82/fuel-intake-app/releases)

Unzip it (you get a `FuelIntake-<version>` folder) and run `FuelIntake.exe`. Windows may say "unknown publisher" the first time:
choose **More info → Run anyway**.

Test builds of branches that aren't merged yet: Actions → **Build** → pick a run → **Artifacts**.

## Building yourself (optional)

Needs the .NET 10 SDK on Windows (Visual Studio 2026 Community, or the SDK + VS Code).

```
dotnet build FuelIntake.sln
dotnet test FuelIntake.sln
dotnet run --project src/FuelIntake.Desktop
```

## Privacy

Never put real client data in this repository, in test files, or on a personal machine.
Use the built-in test data only.
