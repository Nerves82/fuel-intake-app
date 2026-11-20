# Project notes

Decisions and rules for the Fuel Assistance Intake app. Update this file when a decision
changes, so every contributor (and every Claude session) works from the same facts.

Full plan: "Fuel Assistance Intake App — Development Plan" (Claude doc, Nov 2026).

## Decisions

| Area | Decision |
| --- | --- |
| Rollout | Phase 1 desktop app, then phase 2 web app with Microsoft 365 |
| UI | Blazor (Razor components) in `FuelIntake.UI`, shared by both hosts |
| Desktop host | WPF + BlazorWebView, .NET 10, self-contained win-x64 |
| Offline use | Not needed |
| Storage | Existing SharePoint client folders; one `intake.json` per client + generated PDFs |
| Sign-in (phase 2) | Microsoft Entra ID; Graph calls act as the signed-in user |
| PDFs | PDFsharp / MigraDoc (MIT) |
| Distribution (phase 1) | Portable folder in the Crisis Fuel SharePoint library, synced by OneDrive; a launcher copies it locally before running |
| Repo | Private GitHub repo; currently `Nerves82/fuel-intake-app`, to move to a NEKCA organization later |

## Laptop test results

- 2026-11-20, build 0.1.3, NEKCA laptop: runs without admin rights. SmartScreen shows
  "Unknown publisher" the first time (More info → Run anyway). Ran from Downloads and from
  the synced Crisis Fuel folder. WebView2 present (157.x). Write access OK to Documents,
  the client files folder, OneDrive and the synced Crisis Fuel team folder.
- Lesson: the desktop project must target a Windows 10 SDK TFM
  (`net10.0-windows10.0.19041.0`), or WebView2 fails at start-up with a missing
  `Microsoft.Windows.SDK.NET` assembly.

## Phase 1 data locations (desktop)

- Client folders: `Documents\Client Files - Fuel 26-27\Last, First\intake.json`
  (existing workbook-era folders show up too; opening one starts a new intake from the folder name).
- Test clients: folders starting with `_TEST ` (made-up data only).
- Lists and settings: `Documents\Client Files - Fuel 26-27\_Fuel Intake Settings\`
  `staff.json`, `vendors.json`, `settings.json` (created empty on first run).
  Open question: move these to the shared Crisis Fuel folder so the team shares one copy.
- New client folders get the consent forms copied from the Masters template folder.
- Logs: `%LOCALAPPDATA%\FuelIntake\logs`.

## Previewing screens without Windows

`dotnet run --project src/FuelIntake.DevHost` serves the screens with fake data at a local URL.
Shortcut: `/?sample=1&screen=Household`. In an offline sandbox use `-p:OfflinePreview=true`
(static rendering only: good for screenshots, not clicking).

## Multiple staff on one intake

- Any staff member can open and edit any client. Each save logs who changed which fields
  (old → new, SSNs masked) in `intake.json` → `activity`; edits by the same person within
  15 minutes merge into one entry. The Review tab shows the history and flags edits by
  someone other than the creator.
- The assigned staff member's Staff-list details are copied into the file (`staff.staffSnapshot`),
  so forms print correctly on any computer; opening the file adds them to the local Staff list
  if missing.
- If intake.json changed on disk since it was opened (another computer saved), the app stops
  saving and tells the user to reopen, instead of overwriting.

## Notes

- Staff notes live in `intake.json` → `notes` (id, time, author, text). The Notes panel opens from the
  header and stays open across tabs. Anyone can delete a note (with a confirm); adding and deleting
  are logged in the activity history with the note text, so a deleted note is still traceable.

## Printing (phase 1c)

- Forms are Razor components in `FuelIntake.UI/Printing/Forms`, styled by `forms.css`
  (letter pages; `.landscape` pages use a named CSS page). Logo and CSS are embedded so the
  HTML is self-contained.
- `PacketRenderer` turns the chosen forms into one HTML document; `IPdfPrinter` prints it.
  Desktop: WebView2 `Page.printToPDF` with `preferCSSPageSize` (portrait + landscape in one PDF).
  Phase 2: a headless Chromium on the server can print the same HTML.
- Check layouts without Windows: DevHost `/print-html?sample=1&forms=FuelOrder,Access`, then print to
  PDF in Chromium (Playwright `page.pdf(prefer_css_page_size=True)`) — same engine as WebView2.
- Re-printing replaces the PDF of the same name; if it's open in a viewer, a "(2)" copy is saved.
- Consent forms are not drawn: they're copied from the Masters folder into new client folders.

## Architecture rules

- Screens never call Windows-only features directly. Anything host-specific goes behind
  a Core interface: `IClientStore`, `IListSource`, `ICurrentUser`, `IFileOutput`.
- Core has no UI and no Windows code, and is covered by tests.
- `intake.json` carries a format version number; phase 2 must open phase 1 files unchanged.

## Privacy rules

- Client data is saved only to the client's SharePoint folder. Never to the desktop,
  email, logs, crash reports, this repo, or a personal machine.
- The Cover Sheet and GoEngage Data printouts contain SSNs and income: staff only,
  never uploaded to GoEngage.
- Test data is clearly fake and loads only in test mode, never into a real client's file.

## Business rules carried over from the workbook

Ported to `FuelIntake.Core` in phase 1a (data model, formatting, FPL, mappings, staff/vendor
lookups, packets, validation); print layouts are still in `reference/workbook/print_tabs.py`.

Small deliberate changes from the workbook:
- ACCESS income text joins amount and source with " - " only when both exist
  (the workbook could print a leading " - " when only a source was given).
- Member ages use the application date, or today when there is none (same as the workbook).
- The Staff list no longer rejects badly formatted phones; validation flags them instead.

- FPL % = monthly household income × 12 ÷ HHS poverty guideline for the household size
  (48 contiguous states). Sizes over 8 add the per-person amount. Guidelines are updated
  each January; the year's table must be editable without a new build.
- Crisis Fuel income limit: under 200% FPL (editable setting).
- Dropdown values match GoEngage exactly; paper checkboxes are derived from them:
  - Housing: Apartment, House, Mobile Home and similar → "Other"; homeless types →
    "Homeless/Temporary Shelter".
  - Occupation: combined values tick two boxes; Disabled, Paying job, Volunteer and
    similar tick none; N/A is excluded.
  - Disabled stays Y/N.
- Phone: stored as 10 digits; input accepts dashes, spaces, parentheses and stray line
  breaks; prints as (802) 555-0123. Leading zeros must survive.
- SSN: stored as text, padded to 9 digits, dashes optional on input; prints as 012-34-5678.
- Zip: 5 digits, text (leading zeros).
- The client's date of birth fills household member 1.
- The ACCESS non-income line comes from SNAP.
- Staff info comes from a Staff list (name, initials, phone, email); per-client overrides
  allowed. "Send invoice to" uses the staff contact, else an agency default.
- Vendor info comes from the "Fuel Team Vendor List" (SharePoint List on the
  3025 Crisis Fuel site). Columns used: Vender Name, Location, Contact Emails,
  Contact Phone Numbers, Fuel Offered, Minimum Delivery, Emergency Trip Fee,
  Preassure Test/Leak Check Fee, Notes (spellings as in the List).
  - A copy of the List (CSV export, Nov 2026) ships in `src/FuelIntake.Core/Data/` and seeds
    vendors.json when it's missing or empty. Staff update it with Settings → Vendors → import CSV.
  - Rows with no Vender Name are skipped (with a warning). The pressure-test fee exports as a Number:
    0 means "not set". 10-digit phones print as (802) 555-0123.
  - Fuel vendor dropdown: entries not offering only Electric.
  - Electric provider dropdown: entries whose Fuel Offered includes Electric.
  - Fuel Order: emails on contact line 1, phones on line 2. Electric Pledge the same.
- Client folders: `Last, First` (+ date), under the clients root folder.
- Packet file name example: `f_lastname_invoice_packet_mm_dd_yy`.

## Packets (from the workbook's Print Packets script)

- Cover Sheet + GoEngage Data (staff only)
- Intake Form Package
- Application Forms Pack (Fuel Order or Electric Pledge chosen by crisis type)
- All forms
- Custom selection

## Open questions

- Informal OK from supervisor / IT for phase 1, and personal-machine development with test data.
- Who co-owns the NEKCA GitHub organization.
- Read vendor/staff lists from SharePoint already in phase 1, or wait for phase 2.
