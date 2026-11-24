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

- Client files folder: chosen under **Staff, vendors & settings → Data folder**; default
  `Documents\Client Files - Fuel 26-27`. The choice is saved per Windows user in
  `%LOCALAPPDATA%\FuelIntake\data-folder.json`, outside the versioned app folder, so new versions
  keep it. To use SharePoint: sync the document library with OneDrive, then choose the synced folder.
  The app labels folders OneDrive keeps in sync (read from the user's OneDrive settings in HKCU).
- A chosen folder that's missing is never created (that would keep client files on the laptop only);
  the app shows the problem and lets staff pick again. The default folder is created when missing.
- Switching folders closes the open client first, can copy the lists and saved test data sets
  (never overwriting), and never moves client folders.
- Client folders: `<client files folder>\Last, First\intake.json`
  (existing workbook-era folders show up too; opening one starts a new intake from the folder name).
- Test clients: folders starting with `_TEST ` (made-up data only).
- Lists and settings: `<client files folder>\_Fuel Intake Settings\`
  `staff.json`, `vendors.json`, `settings.json` (created empty on first run).
  Open question: move these to the shared Crisis Fuel folder so the team shares one copy.
- New client folders get the consent forms copied from the Masters template folder.
- Logs: `%LOCALAPPDATA%\FuelIntake\logs`.

## Previewing screens without Windows

`dotnet run --project src/FuelIntake.DevHost` serves the screens with fake data at a local URL.
Shortcut: `/?sample=1&screen=Household`. In an offline sandbox use `-p:OfflinePreview=true`
(static rendering only: good for screenshots, not clicking).

## Test data
- Built-in practice cases live in `SampleIntakes.All` (Core). Each one uses SSNs starting 000 and
  555 phone numbers. Cases: single senior/oil, family of 4/disconnect, over income/denied/phone app,
  household of 10/propane with fees and invoice, multigenerational SSDI+SSI/pellets, shelter/kerosene/
  second crisis, and "Needs fixing" (deliberate errors for Review-tab practice; `HasErrors` exempts it
  from the no-errors test).
- **Save as test data set** is on the Review tab of a **test client only**. A real client can never be
  saved as a set, because its SSNs and income would become practice data. Core enforces this:
  `TestDataSetRules.FromRecord` throws for a non-test record, and sets read from disk that aren't
  marked as test data are skipped.
- Saving drops activity, notes, creator, staff name/overrides and case number. Starting a client
  from a set uses the new test client's name and today's date.
- Saving with an existing set's name replaces that set. Deleting a set leaves the test clients made from it.
- Saved sets are stored as one JSON file each in `_Fuel Intake Settings\test-data\`, next to the
  staff and vendor lists. They become shared whenever that folder does (see the open question above).
  Phase 2 will move them to a SharePoint list or library.

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
- The Review tab's Activity list groups each uninterrupted run by one person on one day into a
  single item (time range, summary, net field changes, notes added/deleted). The saved log keeps
  every entry; grouping is display only (`ActivityLog.Group`).

## Notes

- Staff notes live in `intake.json` → `notes` (id, time, author, text). The Notes panel opens from the
  header and stays open across tabs. Anyone can delete a note (with a confirm); adding and deleting
  are logged in the activity history with the note text, so a deleted note is still traceable.
- Hover over a field (or put the cursor in it) and a small **+ note** button shows by its label; it opens
  the notes panel with a note about that field (`about` = tab, field key, label). The note's link switches
  tab, scrolls to the field and flashes it. Right-click is left alone (the normal copy/paste menu); the
  earlier right-click "Add note" menu was removed because it got in the way. Field keys come from the cascaded `FieldScope` (tab › section/card › label), so
  renaming a section or label breaks old links (they then just open the tab). Household references
  are by member number: removing a member shifts later members' links.
- A field with notes about it shows a small 📝 marker by its label (with a count when more than one).
  Clicking it opens the notes panel and highlights those notes. Matching is by tab + field key
  (`ActivityLog.NotesAbout`), so a note still finds its field if the label wording changes.

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
- PDFs are made in a separate WebView2 browser with its own working folder
  (`%LOCALAPPDATA%\FuelIntake\WebView2-print`), started on the first save and reused. Sharing the main
  window's browser gave "The group or resource is not in the correct state" on the NEKCA laptop. If the
  print browser fails it's restarted once, then the main browser is tried. The forms are served to it
  from memory (no temp files). Failures are logged with their error code.

## Light / dark mode
- The round button at the right of the header cycles System (◐, follows Windows) → Light (☀) → Dark (☾).
  The choice is kept in WebView2's browser storage under %LOCALAPPDATA%, so new versions keep it.
- All app colours are CSS variables in `app.css`; `:root[data-theme="dark"]` overrides them.
  `fuelintake.js` sets `data-theme` before the first render. Printed forms (`forms.css`) and the
  print preview always stay black on white.

## Household last names
- Tab 2: new members start with the client's last name. A member whose last name differs (or is blank)
  gets a **Use "Smith"** link; the summary strip has **Use "Smith" for all N other members**, with
  **Undo** right after. (`HouseholdNames`)

## Correcting a client's name
- Fix the spelling on tab 1. Once it saves, a banner says the name doesn't match the folder and offers
  **Rename folder and files…**. The preview lists the folder (old → new) and every file whose name has
  the old name, each with a checkbox. "Not now" hides it until the client is closed.
- The old name is read from the folder name ("Last, First", "_TEST " kept for test clients), since tab 1
  already has the new one. Files match on the app's "J_Smith_…" start (initial follows the first name) and
  on the whole old first/last name standing alone (so "Al" never changes "all_forms"). A new name that's
  already taken gets " (2)"; nothing is overwritten. intake.json is never renamed. (`ClientRename`)
- The folder is renamed first; if Windows can't (a file is open, or a folder with the new name exists),
  nothing changes. Files that can't be renamed are skipped and listed. The rename is in the activity log
  (folder and each file, old → new); the app log records only counts, no names.
- The preview also lists household members (tab 2, member 2+) whose last name is the old spelling, ticked;
  their last names change with the rename and show in the activity as ordinary edits.

## Deleting a client
- Review tab → **Delete this client**: lists what's in the folder, then needs the client's last name
  typed to confirm. Real (non-test) clients get a reminder about record-keeping rules.
- The whole folder goes to the Windows Recycle Bin (SharePoint's recycle bin too, when synced), so it
  can be restored. Only folders directly inside the client files folder can be deleted, never `_Fuel
  Intake Settings`. The app log records that a folder was deleted, without the client's name.
- Saving never re-creates a deleted folder: someone else still editing that client gets a "not saved" message.

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

## Paper applications (scans → client)
- Decisions (Steve, Nov 23): recognition stays **on the laptop** (no cloud); staff may use the app's printed
  forms or the agency's existing forms; every field needs a staff tick before the client is made.
- Clients page → **New client from paper application** (between New client and Recently opened) →
  choose the scanned pages (PDF/JPG/PNG). A holding folder `_PENDING yyyyMMdd-HHmm xxxx` is made in the client
  files folder with copies of the scans and `import.json` (every field, its value, what the reader read, and
  who ticked it when). Holding folders show on the Clients page as **pending verification** (also in All
  clients) so any staff member can carry on later.
- Approval page (Screen.Verify): every field (client, benefits, each household member, income lines) with the
  scan beside it. Each needs **This is correct**, blank ones included ("left blank"); there is no check-all.
  Editing a value clears its tick. Values are checked by kind (date, money, SSN, phone, zip, list choice).
  **Create client** stays disabled until every field is ticked, nothing needs fixing and there's a name.
- Create: refuses if a client folder with that name already exists; builds intake.json from the confirmed
  values (`PaperImportRules.Build`), logs "Created from a paper application" with who checked how many fields,
  moves the scans into the new client folder, removes the holding folder, opens the Review tab.
  **Discard** sends the holding folder to the Recycle Bin.
- Scans are shown in the built-in viewer via a WebView2 virtual host (`clientfiles.fuelintake`) mapped to the
  client files folder (re-mapped when the folder changes).
- Reading (step 2): the first time an application is opened, the scans are read on the laptop with Windows'
  built-in text recognition (Windows.Data.Pdf renders PDF pages, Windows.Media.Ocr reads them; no install, nothing
  leaves the laptop). `ScanFieldReader` finds each field's printed label ("Last Name", "Date of Birth", "SSN", the
  income line names…) and takes the text to its right, else below, else above, then shapes it to the field
  (date, SSN, phone, zip, money, list choice). Values that don't look right are marked "Unsure reading".
  Only blank, unticked fields are filled, so **Read the scans again** never overwrites staff work.
  Pieces the reader splits on one row (table-style printouts like the GoEngage profile) are joined back first
  (`ScanFieldReader.Rows`); "Name: Smith, Jane" is split into last/first. Re-reading replaces earlier readings
  staff haven't changed or ticked. "What the reader saw" shows a copyable layout with letters → x and digits → 9
  (printed label words kept) so missed fields can be diagnosed without sharing client details.
  Works best on typed/printed forms and block capitals; cursive is hit and miss. Household members 2+,
  checkboxes and tables aren't read yet.
- Next: print blank forms with page codes and corner marks (known positions instead of label hunting),
  read ticked boxes by ink, map the agency's existing forms (need a blank copy of each).
- Known limit: two people editing the same pending application at the same moment: last save wins.

## Fuel Order email
- Review tab → **Email the Fuel Order to the vendor** → **Create email** (not shown for a disconnect notice).
  To = the vendor's emails from the vendor list (or the vendor email override on tab 5); subject and message
  from Agency settings → Fuel Order email, with built-in defaults (`FuelOrderEmail.DefaultSubject/Body`;
  stored blank while unchanged, so a better default reaches everyone). {Placeholders} are filled from the file;
  a "Label: {Token}" line is dropped when that detail is blank; unknown {words} are left as typed.
- Attachment: the newest `{Initial}_{Last}_fuel_order_{Date}.pdf` in the client folder; if none (or staff tick
  "Make a new Fuel Order PDF") the Fuel Order form is printed and saved under that name first.
- The app never sends mail. Desktop opens classic Outlook (COM, no admin) with To, subject, the message above
  the user's signature, and the PDF attached, but only when OUTLOOK.EXE is really installed (App Paths in the
  registry) **and** the user has an Outlook mail profile (HKCU Office\16.0\Outlook\Profiles), and it hasn't failed
  on this computer before (`%LOCALAPPDATA%\FuelIntake\classic-outlook-unavailable.txt`; delete it to retry).
  On NEKCA laptops Office 2016 lists Outlook without it being set up, and asking for it via COM started the
  "installing Outlook 2016" Windows Installer prompt.
  Otherwise mailto: opens the default mail app, or **Use Outlook on the web** opens an OWA compose deep link;
  neither can attach, so File Explorer opens with the PDF selected (`/select,"path"`) to drag in.
- Test clients: To is left blank so practice data is never sent to a real vendor.

## Compliance standards
- Agency-wide standards (start: **Warmth**, **Crisis Fuel**) in `_Fuel Intake Settings\compliance.json`, so every
  staff member using that folder gets the same rules. Edited under Staff, vendors & settings → **Compliance 🔒**,
  which needs a passcode (PBKDF2-SHA256 hash + salt in the same file; first person sets it; unlocked until the
  app closes). It keeps casual edits out; it isn't real security (the file is editable). Phase 2: a SharePoint
  list with permissions.
- Each standard: name, description, linked grant sources, and **required** fields (the only rule for now).
  Any field can be required: the list comes from the intake record itself (`FieldCatalog`, ~220 fields; household
  fields apply to every member; member 1's name/DOB are exempt because they're the client fields; a required
  checkbox must be ticked).
- Which standard applies: the one picked on the Review tab (`application.complianceStandard`), else the one
  linked to the Fuel Order / Electric Pledge / invoice grant source (or named the same as it).
- A missing required field is an **error** on Review: "Required for Crisis Fuel: fill in …".

## Grant / funding sources
- Kept in Agency settings (`settings.json` → `grantSources`), edited under Staff, vendors & settings →
  Grant sources: name (what prints), optional code, used for Fuel and/or Electric, optional limit per
  assist, active, notes. Phase 2: a Microsoft List.
- The Fuel Order, invoice and Electric Pledge "Grant/funding source" fields become dropdowns of the
  active sources for that use (a text box until the agency has a list). The chosen source's code, limit
  and notes show under the field. Picking on the Fuel Order also fills the invoice when it was blank
  or matched the order.
- Review tab: warns when a source is missing (once a list exists), inactive, set up for the other kind
  of assistance, or when the total is over its limit; notes a typed source that isn't on the list.
- Open question for Steve: what else a grant source should carry (e.g. a default per crisis type, a
  season/date range, or a code printed on the forms).

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

## New client with a compliance standard; badges in the clients list (2026-11-23)

- The **New client** box has an optional *Compliance standard* dropdown (listed only when standards exist). The
  new record starts with `application.complianceStandard` set to the standard's name as spelled in settings
  (`ComplianceBadge.ForNewClient`); blank leaves it to the grant source. It also applies to test-data clients.
  Reopening an existing folder only fills the standard in if the record has none. Change it later on Review.
- The clients list (Recently opened and All clients) shows a badge per client (`ComplianceBadge.For`):
  solid = picked on Review/at creation; dashed = from the grant source; amber = picked name no longer in settings.
- Badges need each client's intake.json: `IntakeSession.BadgeAsync` reads them in the background (4 at a time),
  caches each record until its LastModified changes, and works the badge out from the *current* settings.
  The page waits up to 300 ms so short lists show badges with the list; longer lists fill in as they're read.
  Note: with OneDrive Files On-Demand, this downloads each (small) intake.json once.

## State form 201SF: Vermont Fuel Assistance Eligibility Review (2026-11-23)

- Steve supplied the blank form (DCF ESD-Form-201SF, rev 01/2024). Reference copy: `reference/forms/ESD-Form-201SF.pdf`.
- Print tab: form **201SF Eligibility Review (state form)** and pack **Vermont Fuel Assistance Eligibility Review (201SF)**
  (file `{Initial}_{Last}_201SF_{Date}`). Also part of "All forms".
- How it's drawn: the state's pages (fields removed) as 220 dpi images behind the values, which are placed at the
  PDF fields' positions. That fits the existing HTML → PDF printing (preview, Save PDF, Fuel Order email unchanged).
  The saved PDF isn't fillable; staff and the client finish it by hand.
- `reference/forms/make_201sf.py` regenerates `src/FuelIntake.UI/Printing/Form201SF/page1-3.png` and
  `src/FuelIntake.Core/Forms/Form201SFLayout.g.cs` (field positions). Run it when DCF issues a new revision, then
  check `Form201SF.cs`: it picks boxes by PDF field name, and several names in this PDF don't match their labels
  (the signature block especially, see the comments).
- Filled from the intake (Core `Form201SF.Fill`), only where the intake clearly answers:
  - p1: name, SSN, mailing address (physical + town if no separate mailing; both lines when they differ), phone;
    four household rows (name, SSN, DOB); Hispanic yes/no, Male/Female, single races. Relationship, citizenship
    and ID# are blank. A 5th+ person goes on a continuation sheet after page 3.
  - p2: own/rent (+ the Mortgage/Rent bill when renting), heat included in rent, house/mobile home/apartment,
    main fuel, rent based on income = Yes only with a housing benefit (voucher or HUD-VASH → Section 8, public
    housing, permanent supportive → Subsidized), fuel supplier name/phone/account, electric provider/account.
  - p3: Q14 Yes when someone's income source is Self-employment; Q15 table from the worksheet's monthly
    amounts (SSA + SSDI → Social Security, SSI, VA, Unemployment, Workers' Comp, Child Support + Alimony,
    Pension → Retirement, Other + its description) with the names of members who have that source; helper
    name/phone = assigned staff, agency = NEKCA. "No" answers are never assumed. Job pay rows are left blank.

## Issues shown on the fields; built-in 201SF compliance standard (2026-11-23)

- Each `ValidationIssue` may carry a `Path` (`client.zip`, `household[2].ssn`, `incomeExpenses.income.SSA`: the
  same keys as the compliance `FieldCatalog`). Issues about totals or several fields have none.
- Fields find their own path from `@bind-Value` (`FieldBase.ValueExpression` → `BoundPath` → `RecordPaths`, which maps
  every object of the open record to its path). Fields not bound that way pass `Path` (the income/expense cells).
- A field with an issue gets a coloured edge (error red, warning amber, info blue) and the issue's message under it
  (a typing problem like a short zip still shows first). Checkboxes get an outline and the message.
- New issues in `IntakeValidator`: always give the path when the issue is about one field.
- Built-in compliance standard **Vermont Fuel Assistance (201SF)** (`ComplianceSettings.Vermont201SF`): requires the
  intake fields the 201SF fills (names, DOBs, SSNs of everyone, address, phone, housing type, fuel type, fuel vendor
  and account, electric provider and account, staff). It's in the defaults for new settings; existing
  compliance.json files get it from Settings › Compliance (passcode) › "+ Add built-in". Not linked to a grant: pick
  it on the Review tab or when creating the client.

## Compliance tag colours (2026-11-23)

- Each compliance standard's tag in the clients list has its own colour, by its place in the settings list
  (`ComplianceBadge.ColorOf` → CSS `std-0`…`std-7`: blue, teal, brown, indigo, olive, cyan, green, rose; light and
  dark values in app.css). None match the list's other tags (TEST purple, pending amber, no intake grey). A 9th
  standard would reuse the 1st colour.
- Filled = picked for the client. Dashed outline + "· via grant" = not picked; applies because of the grant source on
  the order. Red "· not in settings" = the picked standard was removed. A key above the list says so.
- Reordering standards in Settings changes their colours.

## Staff dropdown and staff phones (2026-11-24)

- Bug: the Staff, fuel vendor and electric provider fields (dropdowns that also accept a typed name) used the
  browser's `<datalist>`, which only lists options matching what's already in the box. With a name filled in (e.g.
  the signed-in staff member, stamped on new clients) only that name showed, so newly added staff looked missing.
  `SelectBox` with `AllowOther` now uses the app's own picker listing every option, plus a last entry
  "Type a name not on the list…" that switches to a text box (Enter or leaving the box keeps it, Esc goes back).
- Staff phones are formatted "(802) 555-0123": `IntakeCalculations.StaffPhone` (forms, email, 201SF), the Settings
  staff table (on load, on leaving the box, on save) and the default invoice phone. Anything not 10 digits stays as typed.

## Crisis Fuel rules: reference and compliance checks (2026-11-24)

- Source: Vermont DCF Crisis Fuel Assistance Rules 3100–3113, effective 06/22/2024 (official PDF:
  https://outside.vermont.gov/dept/DCF/Shared%20Documents/ESD/Rules/3100-Crisis-Fuel.pdf). Steve supplied a scan
  (`reference/rules/`), which has only the even-numbered pages; the odd pages' content was taken from the official PDF.
- `Core/Rules/CrisisFuelRules.cs`: each rule (or part) with its citation, a plain-language summary (NEKCA's notes,
  not the rule text; the official PDF governs), the printed page, which fields it applies to (`ForField`, by record
  path), and the 3104(k)/(l) application windows. Update it when DCF revises the rules.
- The "?" rule help shows only when the client's compliance standard (picked, or from its grant source) uses the
  Crisis Fuel rules, i.e. has any Crisis Fuel check switched on (`ComplianceStandard.UsesCrisisFuelRules`). The
  Rules panel itself is always available from the header.
- Screens: a "?" beside every field a rule applies to (hover = the rule in plain words; click = the Rules panel at
  that rule). The header's **Rules** button toggles the panel (it shares the right side with Notes). Review-tab issues
- Screens: a "?" beside every field a rule applies to (hover = the rule in plain words; click = the Rules panel, on the left, at
  that rule). The header's **Rules** button toggles the panel; it can be open alongside Notes (on the right). Review-tab issues
  from a rule check have a "?" too. The panel links to the official PDF (opens in the browser).
- Compliance standards now have **rule checks** (`ComplianceChecks`, written in code; standards just tick them) as
  well as required fields. Crisis Fuel's defaults: every `cf-` check, and required SSNs for all members, electric
  provider + account, fuel type, crisis type, application date (3104(e), (m)). The fuel supplier + account is a check
  (not needed when the main heat is electric). Existing compliance.json files get them with Settings › Compliance ›
  Crisis Fuel › "Apply the Crisis Fuel rules (3100–3113)" (keeps anything already set).
- Checks and how strict they are: over 200% FPL, a second benefit at or under 185%, over 125 gallons of a liquid fuel,
  missing supplier/account → errors. Outside the application season, no seasonal fuel status at or under 185%,
  tank over 25% or no disconnect notice, no circumstances described, landlord verification, no referral → warnings.
  Verification due date (+5 days), life-threatening (18 h / 30 days), after hours, special trip → info.
- Not checkable from the intake (judgment or history): residency, citizenship, one application per request, crisis
  benefits already used this season (only the "second crisis" box), identity verification.

## Discarding a paper application: copies only, never the originals (2026-11-24)

- Steve: the originals must never be deleted; deleting the app's copies is fine.
- Picking scans always **copies** them into the holding folder (`LocalPendingImports.CreateAsync`); the app never
  writes to, moves or deletes the originals. Finishing moves the copies into the client's folder.
- Discard deletes import.json first (so it's no longer pending, right away), then sends the holding folder (the
  copies) to the Recycle Bin. `RemoveHoldingFolder` refuses anything but a "_PENDING …" folder directly in the client
  files folder.
- The verify page closes its scan preview before discarding: an open scan blocked the Recycle Bin move, which failed
  partway and left the application listed. If a copy is still open elsewhere, the next listing finishes the removal.

## Citizenship (2026-11-24)

- New per-member field **Citizenship / immigration status** (Household tab): U.S. citizen, Asylee, Legal immigrant,
  Refugee, Other (the 201SF's boxes; `ChoiceLists.Citizenship`). Stored as `household[n].citizenship`.
- 201SF: ticks the matching box per row (`Form201SF.CitizenshipBox` sorts each row's five boxes by position, since
  their field numbers aren't in page order); the continuation sheet shows it too. The built-in 201SF standard
  requires it (saved settings: tick it under Household in Settings › Compliance).
- Crisis Fuel: "?" links to 3102(c)(2); new check `cf-citizenship` warns when every member recorded is "Other".

## Extra input tabs per compliance standard; VT Eligibility Review tab (2026-11-24)

- Steve: the 201SF asks things the intake doesn't normally capture; they go on their own tab, and which extra tabs a
  client gets is set **in the compliance standard** (Settings › Compliance › each standard › "Extra input tabs").
- `ComplianceStandard.ExtraTabs` (ids from `InputTabs.All`); the client's effective standard decides the tabs
  (`InputTabs.For`). Unknown ids are ignored. One tab so far: `vt-201sf`, **VT Eligibility Review**, ticked on the
  built-in 201SF standard. Saved settings from before this keep their old standard, so tick it there once.
- The tab follows the form: page 1 (day/message phone, each member's relationship, immigration ID # when not a U.S.
  citizen), **all of page 2, questions 1–11 in order**, page 3 (two jobs with up to 5 pay dates each, self-employment,
  interest/dividends, adoption subsidy, rental income). Stored in `IntakeRecord.StateReview`.
- Page 2 questions the intake already answers are shown there too, editing the same fields: 8 (fuel type), 10
  (supplier, account #) and 11 (electric provider, account #) are tab 5's fields. Question 6 (type of housing) has its
  own answer, because tab 1's housing type mixes Own/Rent with House/Apartment; blank falls back to tab 1 when that
  says House, Mobile Home/Trailer or Apartment.
- An answer on the tab beats what the intake implies (living situation, heat payment, rent based on income,
  self-employment). "No" is only printed when answered; it's never assumed.
- The tab's fields are in the field catalog, so a standard can require any of them.
- Steve (2026-11-24): no locked built-in Crisis Fuel standard. Applying the Crisis Fuel rules to any standard (its
  checks list) is enough.
