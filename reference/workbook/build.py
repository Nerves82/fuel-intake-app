"""Build the Fuel Assistance intake workbook."""
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.worksheet.datavalidation import DataValidation
from openpyxl.comments import Comment
from openpyxl.utils import get_column_letter
from openpyxl.workbook.defined_name import DefinedName

FONT = "Arial"
NAVY = "1F3B57"
INPUT_FILL = PatternFill("solid", fgColor="FFF2CC")
CALC_FILL = PatternFill("solid", fgColor="E7EEF6")
HEAD_FILL = PatternFill("solid", fgColor=NAVY)
SUB_FILL = PatternFill("solid", fgColor="D9E1EA")
thin = Side(style="thin", color="A6A6A6")
BOX = Border(left=thin, right=thin, top=thin, bottom=thin)

def f(bold=False, size=10, color="000000", italic=False):
    return Font(name=FONT, bold=bold, size=size, color=color, italic=italic)

wb = Workbook()
wb.remove(wb.active)

# field registry: name -> dict(ref=..., kind=..., label=..., expr=...)
FIELDS = {}
INPUT_REF = {}
ORDER = []

def reg(name, ref, kind, label, expr=None, src=None):
    assert name not in FIELDS, name
    FIELDS[name] = dict(ref=ref, kind=kind, label=label, expr=expr, src=src)
    ORDER.append(name)

YES = "Yes"
DV_CACHE = {}
def dv(ws, formula, cell):
    key = (ws.title, formula)
    if key not in DV_CACHE:
        d = DataValidation(type="list", formula1=formula, allow_blank=True)
        d.error = "Pick a value from the list. To add choices, edit the Lists tab."
        d.showErrorMessage = True
        d.errorTitle = "Invalid entry"
        ws.add_data_validation(d)
        DV_CACHE[key] = d
    DV_CACHE[key].add(cell)

DATE_DV = {}
def date_dv(ws, cell):
    if ws.title not in DATE_DV:
        d = DataValidation(type="date", operator="between", formula1="DATE(1900,1,1)", formula2="DATE(2100,12,31)", allow_blank=True)
        d.showErrorMessage = True
        d.errorTitle = "Date needed"
        d.error = "Enter a date like 11/16/2026 (month/day/year). Ctrl+; enters today."
        d.showInputMessage = True
        d.promptTitle = "Date"
        d.prompt = "mm/dd/yyyy"
        ws.add_data_validation(d)
        DATE_DV[ws.title] = d
    DATE_DV[ws.title].add(cell)

MONEY_DV = {}
def money_dv(ws, cell):
    if ws.title not in MONEY_DV:
        d = DataValidation(type="decimal", operator="greaterThanOrEqual", formula1="0", allow_blank=True)
        d.showErrorMessage = True; d.errorTitle = "Amount needed"
        d.error = "Enter a dollar amount as a number, like 291 or 291.50 (no letters)."
        ws.add_data_validation(d); MONEY_DV[ws.title] = d
    MONEY_DV[ws.title].add(cell)

def PH_DIGITS(ref):
    s = f'{ref}&""'
    for ch in ('"-"', '" "', '"("', '")"', '"."', 'CHAR(10)', 'CHAR(13)', 'CHAR(160)'):
        s = f'SUBSTITUTE({s},{ch},"")'
    return s
def PH_FMT(ref):
    d = PH_DIGITS(ref)
    return f'"("&LEFT({d},3)&") "&MID({d},4,3)&"-"&RIGHT({d},4)'
def phone_dv(ws, cell):
    # any entry with exactly 10 digits is OK (dashes, spaces, ( ) . and stray line breaks are ignored)
    d = DataValidation(type="custom",
                       formula1=f'AND(LEN({cell}&"")<=16,SUMPRODUCT(--ISNUMBER(--MID({cell}&"",ROW($A$1:$A$16),1)))=10)', allow_blank=True)
    d.showErrorMessage = True; d.errorTitle = "Phone number"
    d.error = "That doesn't look like a 10-digit phone number. Example: 8025550123 or 802-555-0123. Forms print it as (802) 555-0123."
    d.showInputMessage = True; d.promptTitle = "Phone"; d.prompt = "10 digits (dashes OK)"
    ws.add_data_validation(d); d.add(cell)

def ssn_dv(ws, cell):
    dg = f'SUBSTITUTE({cell}&"","-","")'
    d = DataValidation(type="custom",
                       formula1=f'AND(LEN({dg})=9,SUMPRODUCT(--ISNUMBER(--MID({dg},ROW($A$1:$A$9),1)))=9)', allow_blank=True)
    d.showErrorMessage = True; d.errorTitle = "Social Security number"
    d.error = "Enter 9 digits, with or without dashes: 012345678 or 012-34-5678. Leading zeros are kept."
    d.showInputMessage = True; d.promptTitle = "SSN"; d.prompt = "9 digits (dashes optional)"
    ws.add_data_validation(d); d.add(cell)

def zip_dv(ws, cell):
    d = DataValidation(type="custom",
                       formula1=f"AND(LEN({cell})=5,SUMPRODUCT(--ISNUMBER(--MID({cell},ROW($A$1:$A$5),1)))=5)", allow_blank=True)
    d.showErrorMessage = True; d.errorTitle = "Zip code"
    d.error = "Zip code must be exactly 5 numbers, e.g. 05819."
    ws.add_data_validation(d); d.add(cell)

def SSN_FMT(ref):
    """Any SSN entry (text or old number, with or without dashes) -> 012-34-5678."""
    d = f'RIGHT("000000000"&SUBSTITUTE({ref}&"","-",""),9)'
    return f'IF({ref}="","",LEFT({d},3)&"-"&MID({d},4,2)&"-"&RIGHT({d},4))'

def q(sheet):
    return f"'{sheet}'"

# ---------------------------------------------------------------- Settings
st = wb.create_sheet("Settings")
SET = "Settings"

# ---------------------------------------------------------------- form sheet helper
class Form:
    def __init__(self, title, heading, intro):
        self.ws = wb.create_sheet(title)
        self.title = title
        ws = self.ws
        ws.sheet_view.showGridLines = False
        ws.column_dimensions["A"].width = 2
        ws.column_dimensions["B"].width = 40
        ws.column_dimensions["C"].width = 34
        ws.column_dimensions["D"].width = 46
        ws["B1"] = heading
        ws["B1"].font = f(True, 14, NAVY)
        ws["B2"] = intro
        ws["B2"].font = f(False, 9, "595959", True)
        ws["B3"] = "Yellow = type here.   Blue = filled in automatically."
        ws["B3"].font = f(False, 9, "595959", True)
        self.row = 5

    def section(self, text):
        ws = self.ws
        r = self.row + (1 if self.row > 5 else 0)
        for c in "BCD":
            ws[f"{c}{r}"].fill = HEAD_FILL
        ws[f"B{r}"] = text
        ws[f"B{r}"].font = f(True, 10, "FFFFFF")
        self.row = r + 1

    def field(self, name, label, kind="text", options=None, hint="", formula=None, export=True, width_wrap=False):
        """kind: text|date|money|num|yes|choice|calc|calcmoney|calcpct|calcdate"""
        ws = self.ws
        r = self.row
        ws[f"B{r}"] = label
        ws[f"B{r}"].font = f()
        ws[f"B{r}"].alignment = Alignment(vertical="center", wrap_text=True)
        cell = ws[f"C{r}"]
        cell.border = BOX
        cell.font = f()
        cell.alignment = Alignment(vertical="center", wrap_text=True)
        if kind.startswith("calc"):
            cell.value = formula
            cell.fill = CALC_FILL
            cell.font = f(color="1F3B57")
        else:
            cell.fill = INPUT_FILL
        if kind in ("date", "calcdate"):
            cell.number_format = "mm/dd/yyyy"
        if kind == "date":
            date_dv(ws, cell.coordinate)
        elif kind in ("money", "calcmoney"):
            cell.number_format = "$#,##0.00"
            if kind == "money": money_dv(ws, cell.coordinate)
        elif kind == "time":
            cell.number_format = "h:mm AM/PM"
        elif kind == "phone":
            cell.number_format = "@"; phone_dv(ws, cell.coordinate)
            c0 = cell.coordinate
            hint = (f'=IF({c0}="","Type 10 digits, e.g. 8025550123","Prints as: "&{PH_FMT(c0)})')
        elif kind == "zip":
            cell.number_format = "@"; zip_dv(ws, cell.coordinate)
        elif kind == "calcpct":
            cell.number_format = "0%"
        elif kind == "text":
            cell.number_format = "@"
        if kind == "yes":
            dv(ws, '"Yes"', cell.coordinate)
            hint = hint or "Pick Yes, or leave blank"
        if kind == "choice":
            dv(ws, '"' + ",".join(options) + '"', cell.coordinate)
            hint = hint or "Pick from list"
        if kind == "list":
            dv(ws, "=List_" + options, cell.coordinate)
            hint = hint or "Pick from list (same choices as GoEngage)"
        if kind == "date" and not hint:
            hint = "Example: 11/16/2026  (Ctrl+; enters today)"
        ws[f"D{r}"] = hint
        ws[f"D{r}"].font = f(False, 9, "7F7F7F", True)
        ws[f"D{r}"].alignment = Alignment(vertical="center", wrap_text=True)
        if width_wrap:
            ws.row_dimensions[r].height = 45
        ref = f"{q(self.title)}!$C${r}"
        INPUT_REF[name] = ref
        if export:
            ekind = {"calc": "text", "calcmoney": "money", "calcpct": "pct", "calcdate": "date"}.get(kind, kind)
            reg(name, ref, ekind, label)
        self.row += 1
        return ref

# ================================================================ SETTINGS sheet
st.sheet_view.showGridLines = False
st.column_dimensions["A"].width = 2
st.column_dimensions["B"].width = 34
st.column_dimensions["C"].width = 18
st.column_dimensions["D"].width = 60
st["B1"] = "Settings"; st["B1"].font = f(True, 14, NAVY)
st["B2"] = "Agency-wide values. Update the poverty table each year when HHS publishes new guidelines (usually January)."
st["B2"].font = f(False, 9, "595959", True)
st["B4"] = "HHS poverty guideline — 2026 (48 contiguous states)"; st["B4"].font = f(True, 10, "FFFFFF")
for c in "BCD": st[f"{c}4"].fill = HEAD_FILL
st["B5"] = "Household size"; st["C5"] = "100% FPL ($/yr)"
for c in "BC": st[f"{c}5"].font = f(True); st[f"{c}5"].fill = SUB_FILL
fpl = [15960, 21640, 27320, 33000, 38680, 44360, 50040, 55720]
for i, v in enumerate(fpl):
    r = 6 + i
    st[f"B{r}"] = i + 1; st[f"B{r}"].font = f(); st[f"B{r}"].alignment = Alignment(horizontal="left")
    st[f"C{r}"] = v; st[f"C{r}"].font = f(color="0000FF"); st[f"C{r}"].number_format = "$#,##0"; st[f"C{r}"].fill = INPUT_FILL
st["B14"] = "Each additional person"; st["B14"].font = f()
st["C14"] = 5680; st["C14"].font = f(color="0000FF"); st["C14"].number_format = "$#,##0"; st["C14"].fill = INPUT_FILL
st["D6"] = ("Source: HHS ASPE, 2026 Poverty Guidelines (detailed-guidelines-2026.pdf), "
            "https://aspe.hhs.gov/sites/default/files/documents/b1bfa16b20ae9b89d525bc35de7c1643/detailed-guidelines-2026.pdf")
st["D6"].font = f(False, 8, "595959", True); st["D6"].alignment = Alignment(wrap_text=True, vertical="top")
st.merge_cells("D6:D9")
st["B16"] = "Crisis Fuel income limit (% of FPL)"; st["B16"].font = f()
st["C16"] = 2.0; st["C16"].number_format = "0%"; st["C16"].font = f(color="0000FF"); st["C16"].fill = INPUT_FILL
st["D16"] = "From the cover sheet / application: under 200% FPL. Change here if the program limit changes."
st["D16"].font = f(False, 9, "7F7F7F", True)
st["B18"] = "Default invoice email (if staff email blank)"; st["B18"].font = f()
st["C18"].fill = INPUT_FILL; st["C18"].font = f(color="0000FF")
st["B19"] = "Default invoice phone (if staff phone blank)"; st["B19"].font = f()
st["C19"].fill = INPUT_FILL; st["C19"].font = f(color="0000FF")
st["D18"] = "Fuel Order 'send invoice to' uses the staff email/phone on tab 1; these are used only when those are blank."
st["D18"].font = f(False, 9, "7F7F7F", True)
for r in (5,):
    pass
SET_FPL_RANGE = "Settings!$B$6:$C$13"
SET_FPL_EXTRA = "Settings!$C$14"
SET_LIMIT = "Settings!$C$16"
reg("Invoice_Email", "Settings!$C$18", "text", "Send invoice to — email")
reg("Invoice_Phone", "Settings!$C$19", "phone", "Send invoice to — phone")
st["C19"].number_format = "@"; phone_dv(st, "C19")

# ---------------------------------------------------------------- GoEngage dropdown options (from GoEngage screens)
GE = {
    "Housing": ["Apartment", "Community Shelter", "Homeless", "Homeless No Housing", "Hotel/Motel Room", "House",
                "Migrant Housing", "Mobile Home/Trailer", "Other", "Own", "Prison", "Rent", "Shared Housing by Choice",
                "Temporary Housing"],
    "HHType": ["Single Person", "Two Parent Household", "Single Parent Female", "Single Parent Male",
               "Multigenerational Household", "Two Adults NO Children", "Non-related Adults with Children",
               "Household with multiple adults with no children", "Foster Family/Foster Placement", "Other"],
    "County": ["Caledonia", "Essex", "Orleans", "Out of County"],
    "School": ["Blue Mountain", "Caledonia Central", "Kingdom East School District", "NEK Choice",
               "North Country Supervisory Union", "Orleans Central", "St. Johnsbury"],
}
# paper Intake p2 checkbox <- GoEngage values that tick it
HOUSING_MAP = [("Own", ["Own"]), ("Rent", ["Rent"]),
               ("Homeless", ["Community Shelter", "Homeless", "Homeless No Housing", "Hotel/Motel Room", "Temporary Housing"]),
               ("Other", ["Apartment", "House", "Migrant Housing", "Mobile Home/Trailer", "Other", "Prison", "Shared Housing by Choice"])]
HHT_MAP = [("Single", ["Single Person"]), ("TwoAdults", ["Two Adults NO Children"]), ("SingleFemale", ["Single Parent Female"]),
           ("SingleMale", ["Single Parent Male"]), ("TwoParent", ["Two Parent Household"]),
           ("NonRelated", ["Non-related Adults with Children"]), ("Multigen", ["Multigenerational Household"]),
           ("Other", ["Household with multiple adults with no children", "Foster Family/Foster Placement", "Other"])]
OCC_MAP = [("EmployedFT", ["Employed Full-time", "Employed full-time and in school part-time"]),
           ("EmployedPT", ["Employed Part-time", "Employed part-time and in school part-time", "In school full-time and employed part-time"]),
           ("Retired", ["Retired"]),
           ("Student", ["School Full Time", "In school full-time and employed part-time",
                        "Employed full-time and in school part-time", "Employed part-time and in school part-time"]),
           ("UnempShort", ["Unemployed"]), ("UnempLong", ["Unemployed (6 Months or More)"]),
           ("Migrant", ["Seasonal - Agricultural"])]

def any_of(ref, vals):
    return "OR(" + ",".join(f'{ref}="{v}"' for v in vals) + ")"

# ================================================================ 1 CLIENT
c = Form("1 Client", "1 · Client & Household Info",
         "Fill in once per client. These answers fill the Intake, Consent, Release, Statement of Understanding, Fuel Order and Electric Pledge forms.")
c.section("Application & staff")
APP_DATE = c.field("App_Date", "Application date", "date", hint="Used as the date on every form. Example: 11/16/2026  (Ctrl+; enters today)")
c.field("Case_Number", "Case / file # (GoEngage)", hint="Optional. Prints on the cover sheet.", export=False)
SN = c.field("Staff_Name", "Staff name", hint="Pick from the list (Staff tab) - phone, email and initials fill in. Alt+Down opens the list.")
_sdv = DataValidation(type="list", formula1="=List_StaffNames", allow_blank=True)
_sdv.errorStyle = "warning"; _sdv.showErrorMessage = True
_sdv.errorTitle = "Not on the Staff list"
_sdv.error = "This name isn't on the Staff tab. Keep it anyway? (Add the person on the Staff tab so their phone and email fill in.)"
c.ws.add_data_validation(_sdv); _sdv.add(SN.split("!")[1].replace("$", ""))
SIO = c.field("Staff_Initials_Override", "Staff initials (only if different)", hint="Leave blank to use the Staff tab / name", export=False)
SPO = c.field("Staff_Phone", "Staff phone (only if different)", "phone", export=False)
SEO = c.field("Staff_Email", "Staff email (only if different)", hint="Leave blank to use the Staff tab", export=False)
def _staff(col):
    return f'IFERROR(INDEX(Staff!${col}$5:${col}$104,MATCH(TRIM({SN}),Staff!$B$5:$B$104,0))&"","")'
_n = f"TRIM({SN})"
_auto = (f'IF({_n}="","",UPPER(IF(ISNUMBER(FIND(",",{_n})),'
         f'LEFT(TRIM(MID({_n},FIND(",",{_n})+1,100)),1)&LEFT({_n},1),'
         f'LEFT({_n},1)&IF(ISNUMBER(FIND(" ",{_n})),LEFT(TRIM(RIGHT(SUBSTITUTE({_n}," ",REPT(" ",100)),100)),1),""))))')
c.field("Staff_Initials", "Staff initials (used on forms)", "calc",
        formula=f'=IF({SIO}<>"",UPPER({SIO}),IF({_staff("C")}<>"",UPPER({_staff("C")}),{_auto}))',
        hint="Override, else Staff tab, else first + last initial")
STAFF_PHONE = c.field("Staff_Phone_Used", "Staff phone (used on forms)", "calc",
                      formula=f'=IF({SPO}<>"",{SPO}&"",{_staff("D")})', export=False, hint="Override, else Staff tab")
STAFF_EMAIL = c.field("Staff_Email_Used", "Staff email (used on forms)", "calc",
                      formula=f'=IF({SEO}<>"",{SEO}&"",{_staff("E")})', export=False, hint="Override, else Staff tab")
# Fuel Order "send invoice to": staff contact first, Settings default as fallback
def _ph(ref): return PH_FMT(ref)
FIELDS["Invoice_Email"].update(kind="expr", ref=None, label="Send invoice to — email (staff email, else Settings default)",
    expr=f'IF({STAFF_EMAIL}<>"",{STAFF_EMAIL}&"",IF(Settings!$C$18="","",Settings!$C$18&""))')
FIELDS["Invoice_Phone"].update(kind="expr", ref=None, label="Send invoice to — phone (staff phone, else Settings default)",
    expr=f'IF({STAFF_PHONE}<>"",{_ph(STAFF_PHONE)},IF(Settings!$C$19="","",{_ph("Settings!$C$19")}))')

# ---------------------------------------------------------------- Vendors (Power Query from the SharePoint List) + lookup
vs = wb.create_sheet("Vendors")
vs.sheet_properties.tabColor = "7F7F7F"
# left empty on purpose: Power Query loads the Fuel Team Vendor List here, starting at A1 (see Start Here)
vl = wb.create_sheet("Vendor Lookup")
vl.sheet_properties.tabColor = "7F7F7F"
VCOLS = [("A", "Pick name (dropdown)", None), ("B", "Vender Name", "Vender Name"), ("C", "Location", "Location"),
         ("D", "Contact Emails", "Contact Emails"), ("E", "Contact Phone Numbers", "Contact Phone Numbers"),
         ("F", "Fuel Offered", "Fuel Offered"), ("G", "Minimum Delivery", "Minimum Delivery"),
         ("H", "Emergency Trip Fee", "Emergency Trip Fee"), ("I", "Preassure Test/Leak Check Fee", "Preassure Test/Leak Check Fee"),
         ("J", "Notes", "Notes")]
for col, h, _ in VCOLS:
    vl[f"{col}1"] = h; vl[f"{col}1"].font = f(True, 9, "FFFFFF"); vl[f"{col}1"].fill = HEAD_FILL
    vl.column_dimensions[col].width = 28 if col in "ADJ" else 18
vl["L1"] = ("Filled automatically from the Vendors tab by column NAME, so the List's column order doesn't matter. "
            "Don't type here. Multi-line emails/phones are joined with commas.")
vl["L1"].font = f(False, 9, "595959", True)
for r in range(2, 302):
    for col, _, src in VCOLS[1:]:
        val = f"INDEX(Vendors!$A$1:$BZ$400,{r},MATCH(\"{src}\",Vendors!$A$1:$BZ$1,0))&\"\""
        if col in "DEJ":
            val = f"SUBSTITUTE(SUBSTITUTE({val},CHAR(13),\"\"),CHAR(10),\", \")"
        vl[f"{col}{r}"] = f"=IFERROR({val},\"\")"
        vl[f"{col}{r}"].font = f(size=9)
    vl[f"A{r}"] = f'=IF(B{r}="","",B{r}&IF(C{r}="",""," - "&C{r}))'
    vl[f"A{r}"].font = f(size=9)
# helper columns: separate dropdown lists for fuel vendors and electric providers (by "Fuel Offered")
for col, h in (("T", "fuel #"), ("U", "Fuel vendor dropdown"), ("V", "elec #"), ("W", "Electric provider dropdown")):
    vl[f"{col}1"] = h; vl[f"{col}1"].font = f(True, 9, "FFFFFF"); vl[f"{col}1"].fill = HEAD_FILL
    vl.column_dimensions[col].width = 28 if col in "UW" else 7
for r in range(2, 302):
    _elec = f'ISNUMBER(SEARCH("Electric",F{r}))'
    _nonelec = f'SUBSTITUTE(SUBSTITUTE(SUBSTITUTE(F{r},"Electric",""),",","")," ","")<>""'
    vl[f"T{r}"] = f'=IF(AND(A{r}<>"",OR(F{r}="",{_nonelec})),MAX($T$1:T{r-1})+1,"")'
    vl[f"U{r}"] = f'=IFERROR(INDEX($A$2:$A$301,MATCH({r-1},$T$2:$T$301,0)),"")'
    vl[f"V{r}"] = f'=IF(AND(A{r}<>"",{_elec}),MAX($V$1:V{r-1})+1,"")'
    vl[f"W{r}"] = f'=IFERROR(INDEX($A$2:$A$301,MATCH({r-1},$V$2:$V$301,0)),"")'
    for col in "TUVW": vl[f"{col}{r}"].font = f(size=9)
wb.defined_names["List_Vendors"] = DefinedName("List_Vendors",
    attr_text="OFFSET('Vendor Lookup'!$U$2,0,0,MAX(1,COUNTIF('Vendor Lookup'!$U$2:$U$301,\"?*\")),1)")
wb.defined_names["List_ElecVendors"] = DefinedName("List_ElecVendors",
    attr_text="OFFSET('Vendor Lookup'!$W$2,0,0,MAX(1,COUNTIF('Vendor Lookup'!$W$2:$W$301,\"?*\")),1)")

# ---------------------------------------------------------------- Staff tab (editable list)
sf = wb.create_sheet("Staff")
sf.sheet_view.showGridLines = False
for col, w in zip("ABCDEF", [2, 26, 10, 16, 34, 30]):
    sf.column_dimensions[col].width = w
sf["B1"] = "Staff List"; sf["B1"].font = f(True, 14, NAVY)
sf["B2"] = ("Add a row for each staff member, with no blank rows in between. Their name appears in the Staff name dropdown on tab 1; "
            "picking it fills initials, phone and email on the forms. Edit or add rows any time.")
sf["B2"].font = f(False, 9, "595959", True); sf["B2"].alignment = Alignment(wrap_text=True)
sf.merge_cells("B2:F2"); sf.row_dimensions[2].height = 30
for col, t in zip("BCDE", ["Name (as it should print)", "Initials", "Phone (10 digits)", "Email"]):
    sf[f"{col}4"] = t; sf[f"{col}4"].font = f(True, 9, "FFFFFF"); sf[f"{col}4"].fill = HEAD_FILL; sf[f"{col}4"].border = BOX
for r in range(5, 105):
    for col in "BCDE":
        cell = sf[f"{col}{r}"]
        cell.fill = INPUT_FILL; cell.border = BOX; cell.font = f(); cell.number_format = "@"
    # no pop-up check on this admin list; the Phone check column flags bad numbers instead
sf["F4"] = "Phone check"; sf["F4"].font = f(True, 9, "FFFFFF"); sf["F4"].fill = HEAD_FILL
for r in range(5, 105):
    sf[f"F{r}"] = (f'=IF(D{r}="","",IF(LEN({PH_DIGITS(f"D{r}")})<>10,"⚠ Needs 10 digits — check this number",'
                  f'"Prints as: "&{PH_FMT(f"D{r}")}))')
    sf[f"F{r}"].font = f(False, 9, "7F7F7F", True)
sf.freeze_panes = "B5"
sf.sheet_properties.tabColor = "7F7F7F"
wb.defined_names["List_StaffNames"] = DefinedName("List_StaffNames",
    attr_text="OFFSET(Staff!$B$5,0,0,MAX(1,COUNTA(Staff!$B$5:$B$104)),1)")

c.section("Client")
FIRST = c.field("Client_First", "Client first name", hint="Example: Jane", export=False)
LAST = c.field("Client_Last", "Client last name", hint="Example: Smith", export=False)
c.field("Client_FullName", "Client full name", "calc", formula=f"=TRIM({FIRST}&\" \"&{LAST})", hint="Built from first + last name")
c.field("Client_DOB", "Client date of birth", "date", hint="Example: 03/14/1961")
c.field("Legal_Rep_Name", "Legal representative name (if any)", hint="ESD 3SquaresVT release only. Leave blank if none.")
c.section("Contact (Intake page 1)")
PHYS = c.field("Address_Physical", "Physical address", hint="Example: 123 Main St")
CITY = c.field("Address_City", "City/Town", hint="Example: St. Johnsbury", export=False)
STATE = c.field("Address_State", "State", hint="Example: VT", export=False)
ZIP = c.field("Address_Zip", "Zip", "zip", hint="5 numbers. Example: 05819 (leading zero is kept)", export=False)
c.field("County", "County", "list", "County", hint="For GoEngage entry (not on the paper forms)", export=False)
c.field("School_District", "School district", "list", "School", hint="For GoEngage entry (not on the paper forms)", export=False)
CSZ = c.field("Address_CityStateZip", "City/Town, State, Zip (as printed)", "calc",
              formula=f'=TRIM({CITY}&IF(AND({CITY}<>"",{STATE}&{ZIP}<>""),", ","")&{STATE}&" "&{ZIP})',
              hint="Built from the three fields above")
c.field("Address_Mailing", "Mailing address (if not same)", hint="Leave blank if same as physical")
c.field("Phone", "Phone number", "phone", hint="Type 10 digits (dashes OK): 8025550123 → (802) 555-0123")
c.field("Email", "Email (optional)")
c.field("Primary_Language", "Primary language", hint="Example: English")
c.field("Language_Other", "Other language (specify)")
c.field("English_Fluency", "English fluency (if applicable)")
c.field("Translator_Needed", "Translator needed?", "choice", ["Y", "N"])
c.section("Service address (ACCESS, Fuel Order, Electric Pledge)")
SA1 = c.field("Service_Addr1_Input", "Service address line 1", hint="Leave blank to use the physical address", export=False)
SA2 = c.field("Service_Addr2_Input", "Service address line 2", hint="Leave blank to use City/Town, State, Zip", export=False)
c.field("Service_Address_1", "Service address line 1 (used)", "calc", formula=f"=IF({SA1}=\"\",{PHYS},{SA1})")
c.field("Service_Address_2", "Service address line 2 (used)", "calc", formula=f"=IF({SA2}=\"\",{CSZ},{SA2})")

c.section("Housing & household type (Intake page 2)")
HOUSING_OPTS = ["Own", "Rent", "Homeless/Temporary Shelter", "Other"]
HT = c.field("Housing_Type_Input", "Housing type", "list", "Housing", export=False)
for (key, vals), opt in zip(HOUSING_MAP, HOUSING_OPTS):
    reg(f"Housing_{key}", HT, "choicemulti", f"Housing type: {opt}  ← " + ", ".join(vals), src=vals)
HHT_OPTS = ["Single Individual", "Two Adults NO Children", "Single Parent Female", "Single Parent Male",
            "Two Parent HH", "Non-Related Adults With Children", "Multigenerational HH", "Other"]
HHT = c.field("Household_Type_Input", "Household type", "list", "HHType", export=False)
for (key, vals), opt in zip(HHT_MAP, HHT_OPTS):
    reg(f"HHType_{key}", HHT, "choicemulti", f"Household type: {opt}  ← " + ", ".join(vals), src=vals)

c.section("Benefits received (Intake page 2)")
c.field("Benefit_SNAP", "SNAP", "yes")
c.field("SNAP_Amount", "SNAP amount", "money", hint="Example: 291.00")
SNAP_PER = c.field("SNAP_Period", "SNAP amount per", "choice", ["month", "week", "year"])
SNAP_DATE = c.field("SNAP_Verified_Date", "SNAP verified in ACCESS (date)", "date",
                    hint="Date you looked it up. Fills the ACCESS form's non-income line.", export=False)
reg("NonInc1_Text", None, "expr", "ACCESS non-income line (from SNAP on tab 1)",
    expr=(f'IF(OR({FIELDS["Benefit_SNAP"]["ref"]}<>"Yes",{FIELDS["SNAP_Amount"]["ref"]}=""),"",'
          f'"SNAP "&TEXT({FIELDS["SNAP_Amount"]["ref"]},"$#,##0.00")&IF({SNAP_PER}="","","/"&{SNAP_PER}))'))
reg("NonInc1_Date", SNAP_DATE, "date", "ACCESS non-income line: date SNAP verified")
for key, lab in [("WIC", "WIC"), ("LIHEAP", "LIHEAP"), ("HCV", "Housing Choice Voucher"), ("PublicHousing", "Public Housing"),
                 ("PSH", "Permanent Supportive Housing"), ("HUDVASH", "HUD-VASH"), ("Childcare", "Childcare Voucher"),
                 ("ACA", "Affordable Care Act Subsidy")]:
    c.field(f"Benefit_{key}", lab, "yes")

c.section("Programs / services of interest (Intake page 2)")
for key, lab in [("FoodClothing", "Food / Clothing"), ("Housing", "Housing Support"), ("Fuel", "Fuel Assistance"),
                 ("Economic", "Economic Programs (VITA, MicroBusiness, Coaching)"), ("Youth", "Youth Programs"),
                 ("Family", "Parent/Family Supports"), ("TEFAP", "TEFAP/USDA"), ("Justice", "Community Justice Programs"),
                 ("Other", "Other")]:
    c.field(f"Interest_{key}", lab, "yes")
c.field("Interest_Other_Text", "Other (specify)")


# ================================================================ 2 HOUSEHOLD
hh = wb.create_sheet("2 Household")
HH = "2 Household"
hh.sheet_view.showGridLines = False
hh["B1"] = "2 · Household Members"; hh["B1"].font = f(True, 14, NAVY)
hh["B2"] = ("One row per person. Row 1 is the client — name and date of birth come from tab 1. Rows 1–10 fill Intake page 1; rows 1–4 fill ACCESS. "
            "Pick an Occupation for members 14+ (N/A for younger) — the first four fill Intake page 2.")
hh["B2"].font = f(False, 9, "595959", True)
hh["B3"] = "Yellow = type here.   Blue = filled in automatically."
hh["B3"].font = f(False, 9, "595959", True)
# ---------------------------------------------------------------- Lists (editable dropdown options)
ls = wb.create_sheet("Lists")
ls.sheet_view.showGridLines = False
LISTS = [
    ("Gender", "Gender", ["Female", "Male", "Non-binary", "Other"]),
    ("YN", "Disabled (Y/N)", ["Y", "N"]),
    ("Grade", "Education", ["10th grade", "11th grade", "12th grade (no diploma)", "5th - 8th grade", "9th grade",
        "Associate's degree", "Bachelor's degree", "Career Ladder Level 1 Professional", "Doctorate degree",
        "High school graduate (high school diploma or equivalent)", "High School plus some college",
        "Less than or equal to 4th grade", "Master's degree", "No school completed", "Other",
        "Some college (but no degree)", "Unknown"]),
    ("Race", "Race", ["Hispanic or Latino", "Hispanic/Latino", "Middle Eastern or North African", "Two or more races", "Asian",
        "Multiracial and/or Multiethnic", "American Indian or Alaska Native", "Black or African American",
        "Native Hawaiian/Pacific Islander", "Other", "Refused", "Unknown", "White"]),
    ("Hispanic", "Hispanic/Latino", ["Yes", "No", "N/A"]),
    ("HealthIns", "Insurance", ["Direct-Purchase", "Employment Based", "Medicaid", "Medicare", "Military Health Care", "None",
        "State Children's Health Insurance Program", "State Health Insurance for Adults"]),
    ("IncomeSource", "Income source", ["None", "Employment", "Self-employment", "Social Security Retirement (SSA)",
        "Social Security Disability (SSDI)", "Supplemental Security Income (SSI)", "Pension/Retirement", "Unemployment",
        "VA Disability", "Worker's Compensation", "Child Support", "Alimony/Spousal Support", "Reach Up (TANF)", "Other"]),
    ("Military", "Military status", ["Active Military", "Never", "Unknown/not reported", "Veteran"]),
    ("Occupation", "Occupation", ["Disabled", "Employed Full-time", "Employed full-time and in school part-time",
        "Employed Part-time", "Employed part-time and in school part-time", "In job training program",
        "In school full-time and employed part-time", "N/A", "Other", "Paying job", "Retired", "School Full Time",
        "Seasonal - Agricultural", "Seasonal - Non-agricultural", "Unemployed", "Unemployed (6 Months or More)", "Volunteer"]),
    ("Housing", "Housing type", GE["Housing"]),
    ("HHType", "Household type", GE["HHType"]),
    ("County", "County", GE["County"]),
    ("School", "School district", GE["School"]),
]
ls["A1"] = "Dropdown options — edit to match GoEngage/CSBG wording. To add an option, insert a cell INSIDE a list (not below it) so the dropdown picks it up."
ls["A1"].font = f(False, 9, "595959", True)
ls["A2"] = "Don't reword Disabled, Occupation, Housing type or Household type — they tick checkboxes on ACCESS and Intake page 2 by exact wording (mapping is shown on the Field Map tab)."
ls["A2"].font = f(True, 9, "C00000")
for i, (key, title, opts) in enumerate(LISTS):
    col = get_column_letter(i + 1)
    ls.column_dimensions[col].width = 30
    ls[f"{col}3"] = title; ls[f"{col}3"].font = f(True, 9, "FFFFFF"); ls[f"{col}3"].fill = HEAD_FILL
    for j, o in enumerate(opts):
        cell = ls[f"{col}{4 + j}"]; cell.value = o; cell.font = f(size=9, color="0000FF"); cell.fill = INPUT_FILL; cell.border = BOX
    wb.defined_names[f"List_{key}"] = DefinedName(f"List_{key}", attr_text=f"Lists!${col}$4:${col}${3 + len(opts)}")
ls.sheet_properties.tabColor = "7F7F7F"

cols = [
    ("#", 4, None), ("Last name", 16, "text"), ("First name", 14, "text"), ("Date of birth", 12, "date"),
    ("Age", 6, "calc"), ("Gender identity", 14, "L:Gender"), ("Disabled", 9, "L:YN"), ("Last grade completed", 22, "L:Grade"),
    ("Race", 22, "L:Race"), ("Hispanic?", 13, "L:Hispanic"), ("Health insurance source", 22, "L:HealthIns"),
    ("Income per month", 12, "money"), ("Income source", 22, "L:IncomeSource"), ("Military status", 11, "L:Military"),
    ("SSN", 13, "text"), ("Occupation (14+)", 20, "L:Occupation"),
    ("Income verified date (ACCESS)", 13, "date"), ("Occ #", 6, "calc"),
]
HR = 5
for i, (h, w, _) in enumerate(cols):
    col = get_column_letter(i + 2)
    hh.column_dimensions[col].width = w
    cell = hh[f"{col}{HR}"]
    cell.value = h; cell.font = f(True, 9, "FFFFFF"); cell.fill = HEAD_FILL
    cell.alignment = Alignment(wrap_text=True, vertical="center", horizontal="center"); cell.border = BOX
hh.row_dimensions[HR].height = 40
hh.column_dimensions["A"].width = 2
COL = {h: get_column_letter(i + 2) for i, (h, _, _) in enumerate(cols)}
first_r, last_r = HR + 1, HR + 10
for n in range(1, 11):
    r = HR + n
    for i, (h, w, kind) in enumerate(cols):
        col = get_column_letter(i + 2)
        cell = hh[f"{col}{r}"]
        cell.border = BOX; cell.font = f(); cell.alignment = Alignment(vertical="center")
        if h == "#":
            cell.value = n; cell.alignment = Alignment(horizontal="center")
        elif h == "Age":
            d = COL["Date of birth"]
            cell.value = f"=IF({d}{r}=\"\",\"\",DATEDIF({d}{r},IF({APP_DATE}=\"\",TODAY(),{APP_DATE}),\"y\"))"
            cell.fill = CALC_FILL; cell.font = f(color=NAVY)
        elif h == "Occ #":
            o = COL["Occupation (14+)"]
            cell.value = f"=IF(OR({o}{r}=\"\",{o}{r}=\"N/A\"),\"\",COUNTIFS({o}${first_r}:{o}{r},\"?*\",{o}${first_r}:{o}{r},\"<>N/A\"))"
            cell.fill = CALC_FILL; cell.font = f(color=NAVY)
        elif n == 1 and h in ("Last name", "First name", "Date of birth"):
            src = {"Last name": LAST, "First name": FIRST, "Date of birth": FIELDS["Client_DOB"]["ref"]}[h]
            cell.value = f'=IF({src}="","",{src})'
            cell.fill = CALC_FILL; cell.font = f(color=NAVY)
            if h == "Date of birth": cell.number_format = "mm/dd/yyyy"
        elif h == "SSN":
            cell.fill = INPUT_FILL
            cell.number_format = "@"; ssn_dv(hh, cell.coordinate)
        else:
            cell.fill = INPUT_FILL
            if kind == "date": cell.number_format = "mm/dd/yyyy"; date_dv(hh, cell.coordinate)
            elif kind == "money": cell.number_format = "$#,##0.00"; money_dv(hh, cell.coordinate)
            elif kind == "text": cell.number_format = "@"
            elif isinstance(kind, str) and kind.startswith("L:"): dv(hh, "=List_" + kind[2:], cell.coordinate)
    hh.row_dimensions[r].height = 18

def hhref(h, n):
    return f"{q(HH)}!${COL[h]}${HR + n}"

# household export fields
INT1_COLS = [("Last", "Last name", "text"), ("First", "First name", "text"), ("DOB", "Date of birth", "date"),
             ("Gender", "Gender identity", "text"), ("Disabled", "Disabled", "text"), ("Grade", "Last grade completed", "text"),
             ("Race", "Race", "text"), ("Hispanic", "Hispanic?", "text"), ("HealthIns", "Health insurance source", "text"),
             ("Income", "Income per month", "money"), ("IncomeSource", "Income source", "text"),
             ("Military", "Military status", "text"), ("SSN", "SSN", "ssn")]
for n in range(1, 11):
    for key, h, kind in INT1_COLS:
        reg(f"HH{n}_{key}", hhref(h, n), kind, f"Household member {n}: {h}")
for n in range(1, 5):
    L, F = hhref("Last name", n), hhref("First name", n)
    reg(f"HH{n}_FullName", None, "expr", f"Household member {n}: full name",
        expr=f"TRIM({F}&\" \"&{L})")
    reg(f"HH{n}_Disabled_Check", hhref("Disabled", n), "choicecheck", f"Household member {n}: Disabled checkbox", src="Y")
    inc, srcc = hhref("Income per month", n), hhref("Income source", n)
    reg(f"HH{n}_Income_Text", None, "expr", f"Household member {n}: income (quantity and source)",
        expr=f"IF(AND({inc}=\"\",{srcc}=\"\"),\"\",IF({inc}=\"\",\"\",TEXT({inc},\"$#,##0.00\")&\"/mo\")&IF({srcc}=\"\",\"\",\" - \"&{srcc}))")
    reg(f"HH{n}_Income_Date", hhref("Income verified date (ACCESS)", n), "date", f"Household member {n}: income date (ACCESS)")

OCC = [("EmployedFT", "Employed FT"), ("EmployedPT", "Employed PT"), ("Retired", "Retired"), ("Student", "Student"),
       ("UnempShort", "Unemployed (< 6 Months)"), ("UnempLong", "Unemployed (6 months+)"), ("Migrant", "Migrant Worker")]
occ_rng = f"{q(HH)}!${COL['Occ #']}${first_r}:${COL['Occ #']}${last_r}"
def occ_pick(colname, k):
    rng = f"{q(HH)}!${COL[colname]}${first_r}:${COL[colname]}${last_r}"
    return f"IFERROR(INDEX({rng},MATCH({k},{occ_rng},0)),\"\")"
for k in range(1, 5):
    fn = occ_pick("First name", k); ln = occ_pick("Last name", k)
    reg(f"Occ{k}_Name", None, "expr", f"Occupation row {k}: name", expr=f"TRIM({fn}&\" \"&{ln})")
    for (key, opt), (_, vals) in zip(OCC, OCC_MAP):
        reg(f"Occ{k}_{key}", None, "expr", f"Occupation row {k}: {opt}  ← " + ", ".join(vals),
            expr=f"IF({any_of(occ_pick('Occupation (14+)', k), vals)},\"Yes\",\"Off\")")

r = last_r + 2
hh[f"B{r}"] = "Household size"; hh[f"B{r}"].font = f(True)
hh.merge_cells(f"B{r}:D{r}")
HHSIZE = f"{q(HH)}!$E${r}"
hh[f"E{r}"] = f"=COUNTIF({COL['First name']}{first_r}:{COL['First name']}{last_r},\"?*\")+COUNTIFS({COL['First name']}{first_r}:{COL['First name']}{last_r},\"\",{COL['Last name']}{first_r}:{COL['Last name']}{last_r},\"?*\")"
hh[f"E{r}"].fill = CALC_FILL; hh[f"E{r}"].font = f(True, color=NAVY); hh[f"E{r}"].border = BOX
reg("HH_Size", HHSIZE, "num", "Number of household members")
r += 1
hh[f"B{r}"] = "Total of 'Income per month' column"; hh[f"B{r}"].font = f()
hh.merge_cells(f"B{r}:D{r}")
HHINC = f"{q(HH)}!$E${r}"
hh[f"E{r}"] = f"=SUM({COL['Income per month']}{first_r}:{COL['Income per month']}{last_r})"
hh[f"E{r}"].number_format = "$#,##0.00"; hh[f"E{r}"].fill = CALC_FILL; hh[f"E{r}"].font = f(color=NAVY); hh[f"E{r}"].border = BOX
hh.freeze_panes = f"D{HR + 1}"
hh.column_dimensions[COL["Occ #"]].hidden = True

# ================================================================ 3 APPLICATION
a = Form("3 Application", "3 · NEKCA Fuel Assistance Application",
         "Fills the NEKCA Fuel Assistance Application. Client name, signature dates and staff initials come from tab 1.")
a.section("Request")
a.field("App_AAA_OnBehalf", "AAA employee applying on behalf — 3103(c)", "yes")
a.field("App_AfterHours", "Afterhours follow-up — 3105(i)", "yes")
a.field("App_SecondCrisis", "Second crisis assist — 3109(c)", "yes")
CT = a.field("Crisis_Type_Input", "Crisis type", "choice", ["Low on Fuel", "Disconnect Notice"], export=False)
reg("Crisis_LowFuel", CT, "choicecheck", "Low on Fuel", src="Low on Fuel")
reg("Crisis_Disconnect", CT, "choicecheck", "Disconnect Notice", src="Disconnect Notice")
a.field("Current_Quantity", "Current quantity of fuel", hint="Example: 1/8 tank, few bags")
a.field("Elec_For_Heat", "Electric required for heating system", "yes")
a.field("Elec_Propane", "…or Propane", "yes")
a.field("Elec_Component", "Which heating component does the electric run?", hint="Example: Pellet stove")
LV = a.field("Landlord_Verif_Input", "Landlord Verification form 204VEF required? — 3107(c)", "choice", ["Yes", "No"], export=False)
reg("Landlord_Verif_Yes", LV, "choicecheck", "Landlord verification: Yes", src="Yes")
reg("Landlord_Verif_No", LV, "choicecheck", "Landlord verification: No", src="No")
a.field("Heat_In_Rent", "Heat included in rent", "yes")
a.field("Rule_Exception", "…AND meets rule exception 3102(d)", "yes")
a.field("Life_Threatening", "Life-threatening crisis (medical documentation) — 3103", "yes")
a.field("Circumstance", "Unforeseen / extenuating circumstance — 3102(c)6", width_wrap=True, hint="Example: Medical - recent relocation")
SFA = a.field("SFA_Status_Input", "Seasonal Fuel status", "choice", ["Active", "Pending", "Closed/Denied"], export=False)
reg("SFA_Active", SFA, "choicecheck", "Seasonal Fuel: Active", src="Active")
reg("SFA_Pending", SFA, "choicecheck", "Seasonal Fuel: Pending", src="Pending")
reg("SFA_Closed", SFA, "choicecheck", "Seasonal Fuel: Closed/Denied", src="Closed/Denied")
a.field("Appeal_Notice", "Appeal notice provided — 3112", "yes")
a.field("Phone_App", "Phone application — 3104(b)", "yes")
a.field("Phone_App_Reason", "Phone application reason")
a.section("Referrals")
for key, lab in [("NETO", "Weatherization/NETO"), ("Aging", "Agency on Aging"), ("FinCoaching", "Financial Coaching"),
                 ("CommConnections", "Community Connections"), ("WholeFamily", "Whole Family Approach"), ("VITA", "VITA")]:
    a.field(f"Ref_{key}", lab, "yes")
a.section("Determination (staff)")
a.field("Denied", "Denied", "yes")
for key, lab in [("ExpeditedSFA", "Obtained Expedited SFA"), ("Over200", "Over 200% FPL"), ("NoSFB", "Not receiving SFB"),
                 ("NoDocs", "Other – Did not return with documentation"), ("NoCrisis", "No home heating crisis"),
                 ("Resources", "Accessible Resources"), ("MaxGrants", "Max # of Grants"),
                 ("NoCircumstance", "No unforeseen/extenuating circumstance"), ("FundsUse", "Use of funds over the past 30 days"),
                 ("PriorConditions", "Did not meet conditions of prior crisis assist")]:
    a.field(f"Deny_{key}", f"Denial reason: {lab}", "yes")
a.field("Warmth_Eligible", "Warmth eligible", "yes")
a.field("Warmth_Date", "Warmth eligible date", "date")
a.field("VLITE_Eligible", "VLITE eligible", "yes")
a.field("VLITE_Date", "VLITE eligible date", "date")
a.field("Crisis_Eligible", "Crisis Fuel eligible — 3108(d)", "yes")
a.field("Crisis_Elig_Date", "Crisis Fuel eligible date", "date")
a.field("Crisis_Elig_Time", "Crisis Fuel eligible time", "time", hint="Example: 2:30 PM  (Ctrl+Shift+; enters the current time)")
a.field("Chk_Intake", "NEKCA Intake forms complete", "yes")
a.field("Chk_ACCESS", "ACCESS Verification complete", "yes")
a.field("Chk_Consents", "Consent forms complete", "yes")
a.field("Chk_ServiceNote", "Circumstance included in Service Note", "yes")
a.field("Chk_MissingDocs", "Missing documents exception — 3107(e)", "yes")

# ================================================================ 4 INCOME & EXPENSES
ie = wb.create_sheet("4 Income & Expenses")
IE = "4 Income & Expenses"
ie.sheet_view.showGridLines = False
for col, w in zip("ABCDEFGHI", [2, 38, 14, 3, 30, 14, 14, 3, 30]):
    ie.column_dimensions[col].width = w
ie["B1"] = "4 · Income/Expense Worksheet & ACCESS Income"; ie["B1"].font = f(True, 14, NAVY)
ie["B2"] = "Monthly amounts. Totals, Income – Expenses and FPL % calculate automatically."
ie["B2"].font = f(False, 9, "595959", True)
ie["B3"] = "Yellow = type here.   Blue = filled in automatically."
ie["B3"].font = f(False, 9, "595959", True)

def head(ws, rng_cols, r, labels):
    for col, lab in zip(rng_cols, labels):
        cell = ws[f"{col}{r}"]
        cell.value = lab; cell.font = f(True, 9, "FFFFFF"); cell.fill = HEAD_FILL
        cell.alignment = Alignment(wrap_text=True, vertical="center"); cell.border = BOX

def inp(ws, ref, kind="money"):
    cell = ws[ref]
    cell.fill = INPUT_FILL; cell.border = BOX; cell.font = f()
    if kind == "money": cell.number_format = "$#,##0.00"; money_dv(ws, ref)
    if kind == "text": cell.number_format = "@"
    if kind == "date": cell.number_format = "mm/dd/yyyy"; date_dv(ws, ref)

def calc(ws, ref, formula, fmt="$#,##0.00", bold=False):
    cell = ws[ref]
    cell.value = formula; cell.fill = CALC_FILL; cell.border = BOX; cell.font = f(bold, color=NAVY)
    if fmt: cell.number_format = fmt

INCOMES = [("Alimony", "Alimony/Other Spousal Support"), ("ChildSupport", "Child Support"),
           ("Earned", "Earned Income (employment/self-employment)"), ("Pension", "Pension/Retirement Income from Job"),
           ("SSA", "Social Security Retirement (SSA)"), ("SSDI", "Social Security Disability (SSDI)"),
           ("SSI", "Supplemental Social Security (SSI)"), ("Unemployment", "Unemployment"), ("VA", "VA Disability"),
           ("WorkersComp", "Worker's Compensation"), ("Other", "Other")]
EXPENSES = [("AutoIns", "Auto Insurance"), ("AutoReg", "Auto Registration/Repair"), ("AutoLoan", "Auto Loan"),
            ("Childcare", "Childcare/Adult Care"), ("ChildSupport", "Child Support"), ("Cigarettes", "Cigarettes/Vapes"),
            ("CoPay", "CO Payments (Dr/Dentist)"), ("CreditCard", "Credit Card"), ("Electricity", "Electricity"),
            ("Entertainment", "Entertainment/Subscriptions"), ("Groceries", "Groceries"), ("Gas", "Gas"),
            ("Hygiene", "Hygiene/Laundry/Paper"), ("HomeHeating", "Home Heating"), ("HomeIns", "Home Insurance"),
            ("HomeRepairs", "Home Repairs"), ("Medical", "Medical Insurance/Expenses"), ("Loans", "Loans"),
            ("Rent", "Mortgage/Rent"), ("Plowing", "Plowing/Water/Sewer"), ("Phone", "Phone/WIFI"), ("Pets", "Vet/Pets")]

r0 = 5
head(ie, "BC", r0, ["Gross income", "Monthly amount ($)"])
head(ie, "EFG", r0, ["Expenses", "Paid in last 30 days ($)", "Bills ($)"])
ie.row_dimensions[r0].height = 30
for i, (key, lab) in enumerate(INCOMES):
    r = r0 + 1 + i
    ie[f"B{r}"] = lab; ie[f"B{r}"].font = f(); ie[f"B{r}"].border = BOX
    inp(ie, f"C{r}")
    reg(f"Inc_{key}", f"{q(IE)}!$C${r}", "money", f"Income: {lab}")
inc_last = r0 + len(INCOMES)
for i, (key, lab) in enumerate(EXPENSES):
    r = r0 + 1 + i
    ie[f"E{r}"] = lab; ie[f"E{r}"].font = f(); ie[f"E{r}"].border = BOX
    inp(ie, f"F{r}"); inp(ie, f"G{r}")
    reg(f"Exp_{key}_Paid", f"{q(IE)}!$F${r}", "money", f"Expense paid last 30 days: {lab}")
    reg(f"Exp_{key}_Bill", f"{q(IE)}!$G${r}", "money", f"Expense bill: {lab}")
exp_last = r0 + len(EXPENSES)

r = inc_last + 2
ie[f"B{r}"] = "Any income change in the next month?"; ie[f"B{r}"].font = f()
inp(ie, f"C{r}", "text"); dv(ie, '"Yes,No"', f"C{r}")
CHG = f"{q(IE)}!$C${r}"
reg("Inc_Change_Yes", CHG, "choicecheck", "Income change next month: Yes", src="Yes")
reg("Inc_Change_No", CHG, "choicecheck", "Income change next month: No", src="No")
r += 1
ie[f"B{r}"] = "If yes, explain:"; ie[f"B{r}"].font = f()
inp(ie, f"C{r}", "text"); ie[f"C{r}"].alignment = Alignment(wrap_text=True)
reg("Inc_Change_Explain", f"{q(IE)}!$C${r}", "text", "Income change explanation")

tr = exp_last + 1
ie[f"B{tr}"] = "Total gross monthly income"; ie[f"B{tr}"].font = f(True)
calc(ie, f"C{tr}", f"=SUM(C{r0+1}:C{inc_last})", bold=True)
INCTOT = f"{q(IE)}!$C${tr}"
reg("Inc_Total", INCTOT, "money", "Total gross monthly income (also 'Total Gross Income')")
ie[f"E{tr}"] = "Total expenses"; ie[f"E{tr}"].font = f(True)
calc(ie, f"F{tr}", f"=SUM(F{r0+1}:F{exp_last})", bold=True)
calc(ie, f"G{tr}", f"=SUM(G{r0+1}:G{exp_last})", bold=True)
reg("Exp_Total_Paid", f"{q(IE)}!$F${tr}", "money", "Total expenses paid last 30 days")
reg("Exp_Total_Bill", f"{q(IE)}!$G${tr}", "money", "Total expense bills")

r = tr + 2
head(ie, "BC", r, ["Summary & assets", ""])
rows = [("Inc_Minus_Exp", "Income – Expenses (difference)", f"={INCTOT}-{q(IE)}!$F${tr}", "calc"),
        ("Asset_Checking", "Checking", None, "in"), ("Asset_Savings", "Savings", None, "in"), ("Asset_Cash", "Cash", None, "in"),
        ("Asset_CDs", "CDs/Stocks/Bonds", None, "in"), ("Asset_Retirement", "401K/403B/Retirement", None, "in")]
for key, lab, fm, kind in rows:
    r += 1
    ie[f"B{r}"] = lab; ie[f"B{r}"].font = f(); ie[f"B{r}"].border = BOX
    if kind == "calc": calc(ie, f"C{r}", fm)
    else: inp(ie, f"C{r}")
    reg(key, f"{q(IE)}!$C${r}", "money", lab)
ie[f"E{tr+3}"] = "Income – Expenses uses 'Paid in last 30 days'."
ie[f"E{tr+3}"].font = f(False, 8, "7F7F7F", True)
r += 1
ie[f"B{r}"] = "Explanation / notes"; ie[f"B{r}"].font = f(); ie[f"B{r}"].border = BOX
inp(ie, f"C{r}", "text"); ie.merge_cells(f"C{r}:G{r}"); ie.row_dimensions[r].height = 40
ie[f"C{r}"].alignment = Alignment(wrap_text=True, vertical="top")
reg("IE_Notes", f"{q(IE)}!$C${r}", "text", "Income/expense explanation/notes")

r += 2
head(ie, "BC", r, ["Eligibility", ""])
r += 1
ie[f"B{r}"] = "# Household members (from tab 2)"; ie[f"B{r}"].font = f(); ie[f"B{r}"].border = BOX
calc(ie, f"C{r}", f"={HHSIZE}", fmt="0")
r += 1
ie[f"B{r}"] = "100% FPL for this household ($/yr)"; ie[f"B{r}"].font = f(); ie[f"B{r}"].border = BOX
calc(ie, f"C{r}", f"=IF({HHSIZE}<1,\"\",IF({HHSIZE}<=8,INDEX({SET_FPL_RANGE.replace('$B$6','$C$6')},{HHSIZE}),"
                  f"INDEX({SET_FPL_RANGE.replace('$B$6','$C$6')},8)+({HHSIZE}-8)*{SET_FPL_EXTRA}))", fmt="$#,##0")
FPLLINE = f"{q(IE)}!$C${r}"
r += 1
ie[f"B{r}"] = "FPL %  (monthly income × 12 ÷ guideline)"; ie[f"B{r}"].font = f(True); ie[f"B{r}"].border = BOX
calc(ie, f"C{r}", f"=IF(OR({FPLLINE}=\"\",{FPLLINE}=0),\"\",{INCTOT}*12/{FPLLINE})", fmt="0%", bold=True)
FPLPCT = f"{q(IE)}!$C${r}"
reg("FPL_Percent", FPLPCT, "pct", "FPL %")
r += 1
ie[f"B{r}"] = "FPL less than limit?"; ie[f"B{r}"].font = f(True); ie[f"B{r}"].border = BOX
calc(ie, f"C{r}", f"=IF({FPLPCT}=\"\",\"\",IF({FPLPCT}<{SET_LIMIT},\"Yes\",\"No\"))", fmt=None, bold=True)
reg("FPL_Under200", f"{q(IE)}!$C${r}", "choicecheck", "FPL less than 200% checkbox", src="Yes")
r += 1
ie[f"B{r}"] = "Documents attached for 30 days income"; ie[f"B{r}"].font = f(); ie[f"B{r}"].border = BOX
inp(ie, f"C{r}", "text"); dv(ie, '"Yes"', f"C{r}")
reg("Docs_30Days", f"{q(IE)}!$C${r}", "yes", "Documents attached for 30 days income")
r += 1
ie[f"B{r}"] = "Check: household income column (tab 2)"; ie[f"B{r}"].font = f(False, 9, "595959", True); ie[f"B{r}"].border = BOX
calc(ie, f"C{r}", f"={HHINC}")
ie[f"E{r}"] = f"=IF(ROUND({HHINC}-{INCTOT},2)=0,\"Matches total income\",\"Does not match total income — check both tabs\")"
ie[f"E{r}"].font = f(True, 9, "C00000")

ie.freeze_panes = "B6"

# ================================================================ 5 FUEL & ELECTRIC
fe = Form("5 Fuel & Electric", "5 · Seasonal Fuel, Fuel Order & Electric Pledge",
          "Fills the ACCESS vendor section, the NEKCA Fuel Order and the NEKCA Electric Pledge.")
fe.section("Seasonal Fuel & accounts (ACCESS)")
fe.field("SFA_Amount_Granted", "Seasonal Fuel amount granted", "money")
fe.field("SFA_Date_Granted", "Seasonal Fuel date granted", "date")
fe.field("Fuel_Type", "Fuel type", "choice", ["Oil", "Kerosene", "Propane", "Wood", "Pellet", "Electric", "Other"])
VN = fe.field("Vendor_Name", "Fuel vendor",
              hint="Pick from the vendor list (Vendors tab, from the SharePoint List). Alt+Down opens it.")
_vdv = DataValidation(type="list", formula1="=List_Vendors", allow_blank=True)
_vdv.errorStyle = "warning"; _vdv.showErrorMessage = True
_vdv.errorTitle = "Not on the vendor list"
_vdv.error = "This vendor isn't on the Vendors list. Keep it anyway? (Contact details won't fill in.)"
fe.ws.add_data_validation(_vdv); _vdv.add(VN.split("!")[1].replace("$", ""))
VEO = fe.field("Vendor_Email_Override", "Vendor contact email(s) (only if different)", hint="Leave blank to use the vendor list", export=False)
VPO = fe.field("Vendor_Phone_Override", "Vendor phone(s) (only if different)", hint="Leave blank to use the vendor list", export=False)
def _vend(col, key=None):
    return (f"IFERROR(INDEX('Vendor Lookup'!${col}$2:${col}$301,"
            f"MATCH(TRIM({key or VN}),'Vendor Lookup'!$A$2:$A$301,0))&\"\",\"\")")
fe.field("Vendor_Contact", "Vendor contact email(s) (used on forms)", "calc", formula=f'=IF({VEO}<>"",{VEO}&"",{_vend("D")})')
fe.field("Vendor_Contact2", "Vendor phone(s) (used on forms)", "calc", formula=f'=IF({VPO}<>"",{VPO}&"",{_vend("E")})')
for _lab, _col in [("Vendor location", "C"), ("Fuel offered", "F"), ("Minimum delivery", "G"), ("Emergency trip fee", "H"),
                   ("Pressure test / leak check fee", "I"), ("Vendor notes", "J")]:
    fe.field("Vendor_Info_" + _col, _lab + " (from vendor list)", "calc", formula="=" + _vend(_col), export=False)
fe.field("Vendor_Acct", "Vendor account #")
EPV = fe.field("Electric_Provider", "Electric provider",
              hint="Pick from the vendor list (providers with Electric in Fuel Offered). Alt+Down opens it.")
_edv = DataValidation(type="list", formula1="=List_ElecVendors", allow_blank=True)
_edv.errorStyle = "warning"; _edv.showErrorMessage = True
_edv.errorTitle = "Not on the vendor list"
_edv.error = "This provider isn't on the Vendors list. Keep it anyway? (Contact details won't fill in.)"
fe.ws.add_data_validation(_edv); _edv.add(EPV.split("!")[1].replace("$", ""))
EEO = fe.field("EP_Email_Override", "Electric contact email(s) (only if different)", hint="Leave blank to use the vendor list", export=False)
EPO = fe.field("EP_Phone_Override", "Electric phone(s) (only if different)", hint="Leave blank to use the vendor list", export=False)
fe.field("EP_Contact", "Electric contact email(s) (used on forms)", "calc", formula=f'=IF({EEO}<>"",{EEO}&"",{_vend("D", EPV)})')
fe.field("EP_Contact2", "Electric phone(s) (used on forms)", "calc", formula=f'=IF({EPO}<>"",{EPO}&"",{_vend("E", EPV)})')
fe.field("Electric_Acct", "Electric account #")
fe.field("ACCESS_Notes", "ACCESS notes", width_wrap=True)

fe.section("Fuel Order")
fe.field("FO_Authorized", "Authorized", "yes")
fe.field("FO_Date", "Date", "date")
fe.field("FO_Time", "Time", "time", hint="Example: 2:30 PM  (Ctrl+Shift+; enters the current time)")
fe.field("SFB_Remaining", "SFB remaining", "money")
fe.field("Tank_Size", "Size of tank", hint="Example: 275 gal")
fe.field("Fuel_Remaining", "Fuel remaining")
fe.field("Fuel_Lasts", "Time fuel will last", hint="Example: 2 days")
FOF = fe.field("FO_Fuel", "Fuel $", "money")
fe.field("FO_Gallons", "Quantity (gallons)")
FOS = fe.field("FO_SpecialTrip", "Special Trip $", "money")
FOP = fe.field("FO_PressureTest", "Pressure Test $", "money")
FOU = fe.field("FO_StartUp", "Start Up $", "money")
fe.field("FO_Total", "Total $", "calcmoney", formula=f"=IF(COUNT({FOF},{FOS},{FOP},{FOU})=0,\"\",SUM({FOF},{FOS},{FOP},{FOU}))")
fe.field("FO_EstDelivery", "Est. delivery date", "date")
fe.field("FO_Funding", "Grant/funding source")
fe.section("Fuel Order — after invoice received")
fe.field("INV_Received", "Invoice received", "yes")
IF_ = fe.field("INV_Fuel", "Fuel $", "money")
fe.field("INV_Gallons", "Quantity (gallons)")
IS = fe.field("INV_SpecialTrip", "Special Trip $", "money")
IP = fe.field("INV_PressureTest", "Pressure Test $", "money")
IU = fe.field("INV_StartUp", "Start Up $", "money")
fe.field("INV_Total", "Total $  (enter this in GoEngage as the actual fuel cost)", "calcmoney",
         formula=f"=IF(COUNT({IF_},{IS},{IP},{IU})=0,\"\",SUM({IF_},{IS},{IP},{IU}))")
fe.field("INV_DeliveryDate", "Delivery date", "date")
fe.field("INV_Funding", "Grant/funding source")

fe.section("Electric Pledge")
fe.field("EP_Authorized", "Authorized", "yes")
fe.field("EP_Date", "Date", "date")
fe.field("EP_Time", "Time", "time", hint="Example: 2:30 PM  (Ctrl+Shift+; enters the current time)")
fe.field("EP_Disconnect_Notice", "Disconnect notice attached", "yes")
EA = fe.field("EP_Amount", "Amount required $", "money")
ER = fe.field("EP_Reconnect", "Reconnection fee $", "money")
fe.field("EP_Total", "Total $", "calcmoney", formula=f"=IF(COUNT({EA},{ER})=0,\"\",SUM({EA},{ER}))")
ED = fe.field("EP_Discount_Input", "Discount/Forgiveness program", "choice", ["Yes", "No"], export=False)
reg("EP_Discount_Yes", ED, "choicecheck", "Discount/Forgiveness: Yes", src="Yes")
reg("EP_Discount_No", ED, "choicecheck", "Discount/Forgiveness: No", src="No")
fe.field("EP_Disconnect_Date", "Disconnect date", "date")
fe.field("EP_Funding", "Grant/funding source")
fe.field("EP_Remaining", "Remaining balance $", "money")
fe.field("EP_NextBill", "Next bill $", "money")
fe.field("EP_NextBill_Due", "Next bill due by", "date")
fe.field("EP_Responsibilities", "Responsibilities of the client (notes)", width_wrap=True)

# ================================================================ FIELD MAP (per form, in form order)
FORMS = [
 ("ACCESS Verification", [("HH1_FullName", "Head of Household"), ("HH1_SSN", "SSN"), ("HH1_Disabled_Check", "Disabled ☐"),
   ("HH1_Income_Text", "Income (quantity and source)"), ("HH1_Income_Date", "Date")] +
   sum([[(f"HH{n}_FullName", "Name"), (f"HH{n}_SSN", "SSN"), (f"HH{n}_Disabled_Check", "Disabled ☐"),
         (f"HH{n}_Income_Text", "Income (quantity and source)"), (f"HH{n}_Income_Date", "Date")] for n in (2, 3, 4)], []) +
   [("NonInc1_Text", "Non-Income (quantity and source)"), ("NonInc1_Date", "Date")] +
   [("SFA_Active", "Seasonal Fuel Status: Active"), ("SFA_Pending", "Pending"), ("SFA_Closed", "Closed/Denied"),
    ("SFA_Amount_Granted", "Amount Granted"), ("SFA_Date_Granted", "Date Granted"), ("Fuel_Type", "Fuel Type"),
    ("Service_Address_1", "Service Address (line 1)"), ("Service_Address_2", "Service Address (line 2)"),
    ("Vendor_Name", "Vendor"), ("Vendor_Contact", "Contact email/#"), ("Vendor_Acct", "Account #"),
    ("Electric_Provider", "Electric Provider"), ("Electric_Acct", "Account #"), ("ACCESS_Notes", "Notes"),
    ("Staff_Initials", "Staff Initials")]),
 ("Income/Expense Worksheet", [(f"Inc_{k}", lab) for k, lab in INCOMES] +
   [("Inc_Change_Yes", "Income change: Yes"), ("Inc_Change_No", "Income change: No"), ("Inc_Change_Explain", "If yes, explain")] +
   sum([[(f"Exp_{k}_Paid", f"{lab} — Paid in last 30 days"), (f"Exp_{k}_Bill", f"{lab} — Bills")] for k, lab in EXPENSES], []) +
   [("Inc_Total", "Total Gross Monthly Income"), ("Exp_Total_Paid", "Total Expenses — Paid"), ("Exp_Total_Bill", "Total Expenses — Bills"),
    ("Inc_Minus_Exp", "Income – Expenses (difference)"), ("Asset_Checking", "Checking"), ("Asset_Savings", "Savings"),
    ("Asset_Cash", "Cash"), ("Asset_CDs", "CDs/Stocks/Bonds"), ("Asset_Retirement", "401K/403B/Retirement"),
    ("IE_Notes", "Explanation/Notes"), ("Inc_Total", "Total Gross Income (same name = same value)"), ("HH_Size", "# HH Members"),
    ("FPL_Percent", "FPL %"), ("Docs_30Days", "Documents Attached for 30 days Income ☐"), ("FPL_Under200", "FPL Less Than 200% ☐"),
    ("Staff_Initials", "Staff Initials"), ("App_Date", "Date (by client signature)")]),
 ("NEKCA Standard Intake — Page 1", [("Address_Physical", "Physical Address"), ("Address_CityStateZip", "City/Town, State, Zip"),
   ("Address_Mailing", "Mailing Address"), ("Phone", "Phone Number"), ("Email", "Email"), ("Primary_Language", "Primary Language"),
   ("Language_Other", "Other (Please Specify)"), ("English_Fluency", "English Fluency"), ("Translator_Needed", "Translator Needed? (Y/N)")] +
   sum([[(f"HH{n}_{k}", f"Row {n}: {h}") for k, h, _ in INT1_COLS] for n in range(1, 11)], []) +
   [("App_Date", "Date (participant)"), ("Staff_Name", "NEKCA Staff"), ("App_Date", "Date (staff)")]),
 ("NEKCA Standard Intake — Page 2",
   [(f"Housing_{k}", f"Housing Type: {o}") for k, o in zip(["Own", "Rent", "Homeless", "Other"], HOUSING_OPTS)] +
   [(f"HHType_{k}", f"Household Type: {o}") for k, o in zip(["Single", "TwoAdults", "SingleFemale", "SingleMale", "TwoParent", "NonRelated", "Multigen", "Other"], HHT_OPTS)] +
   [("Benefit_SNAP", "SNAP"), ("SNAP_Amount", "SNAP Amount $"), ("SNAP_Period", "SNAP per (/____)")] +
   [(f"Benefit_{k}", lab) for k, lab in [("WIC", "WIC"), ("LIHEAP", "LIHEAP"), ("HCV", "Housing Choice Voucher"), ("PublicHousing", "Public Housing"),
       ("PSH", "Permanent Supportive Housing"), ("HUDVASH", "HUD-VASH"), ("Childcare", "Childcare Voucher"), ("ACA", "Affordable Care Act Subsidy")]] +
   sum([[(f"Occ{k}_Name", f"Occupation row {k}: Name")] + [(f"Occ{k}_{key}", f"Row {k}: {o}") for key, o in OCC] for k in range(1, 5)], []) +
   [(f"Interest_{k}", lab) for k, lab in [("FoodClothing", "Food / Clothing"), ("Housing", "Housing Support"), ("Fuel", "Fuel Assistance"),
       ("Economic", "Economic Programs"), ("Youth", "Youth Programs"), ("Family", "Parent/Family Supports"), ("TEFAP", "TEFAP/USDA"),
       ("Justice", "Community Justice Programs"), ("Other", "Other")]] +
   [("Interest_Other_Text", "Other: ______"), ("App_Date", "Date (participant)"), ("Staff_Name", "NEKCA Staff"), ("App_Date", "Date (staff)")]),
 ("NEKCA Fuel Assistance Application", [("Client_FullName", "Client Name"), ("App_AAA_OnBehalf", "AAA employee applying on behalf ☐"),
   ("App_AfterHours", "Afterhours Follow-up ☐"), ("App_SecondCrisis", "Second Crisis Assist ☐"), ("Crisis_LowFuel", "Low on Fuel"),
   ("Current_Quantity", "Current Quantity"), ("Crisis_Disconnect", "Disconnect Notice"), ("Elec_For_Heat", "For Electric required for heating system ☐"),
   ("Elec_Propane", "or Propane ☐"), ("Elec_Component", "Which component does the electric run?"), ("Landlord_Verif_Yes", "Landlord Verification: Yes ☐"),
   ("Landlord_Verif_No", "No ☐"), ("Heat_In_Rent", "Heat included in rent ☐"), ("Rule_Exception", "AND meets rule exception 3102(d) ☐"),
   ("Life_Threatening", "Life-threatening crisis ☐"), ("Circumstance", "Unforeseen/Extenuating Circumstance"),
   ("SFA_Active", "Seasonal Fuel Status: Active"), ("SFA_Pending", "Pending"), ("SFA_Closed", "Closed/Denied"),
   ("Appeal_Notice", "Appeal Notice provided ☐"), ("App_Date", "Date (client signature)"), ("Phone_App", "Phone application ☐"),
   ("Phone_App_Reason", "Reason"), ("Staff_Initials", "Staff Initials"),
   ("Ref_NETO", "Weatherization/NETO ☐"), ("Ref_FinCoaching", "Financial Coaching ☐"), ("Ref_WholeFamily", "Whole Family Approach ☐"),
   ("Ref_Aging", "Agency on Aging ☐"), ("Ref_CommConnections", "Community Connections ☐"), ("Ref_VITA", "VITA ☐"),
   ("Denied", "Denied ☐"), ("Deny_ExpeditedSFA", "Obtained Expedited SFA ☐"), ("Deny_NoCrisis", "No home heating crisis ☐"),
   ("Deny_NoCircumstance", "No unforeseen/extenuating circumstance ☐"), ("Deny_Over200", "Over 200% FPL ☐"),
   ("Deny_Resources", "Accessible Resources ☐"), ("Deny_FundsUse", "Use of funds over the past 30 days ☐"),
   ("Deny_NoSFB", "Not receiving SFB ☐"), ("Deny_MaxGrants", "Max # of Grants ☐"), ("Deny_PriorConditions", "Did not meet conditions of prior crisis assist ☐"),
   ("Deny_NoDocs", "Other – Did not return with documentation ☐"), ("Warmth_Eligible", "Warmth Eligible ☐"), ("Warmth_Date", "Date"),
   ("VLITE_Eligible", "VLITE Eligible ☐"), ("VLITE_Date", "Date"), ("Crisis_Eligible", "Crisis Fuel Eligible ☐"),
   ("Crisis_Elig_Date", "Date"), ("Crisis_Elig_Time", "Time"), ("Chk_Intake", "NEKCA Intake forms complete ☐"),
   ("Chk_ACCESS", "ACCESS Verification complete ☐"), ("Chk_Consents", "Consent forms complete ☐"),
   ("Chk_ServiceNote", "Circumstance included in Service Note ☐"), ("Chk_MissingDocs", "Missing documents exception ☐"),
   ("App_Date", "Date (staff signature)")]),
 ("NEKCA Consent for Release", [("Client_FullName", "I, ______ (Name of Participant)"),
   ("Client_DOB", "Date of Birth"), ("App_Date", "Date (participant)"), ("App_Date", "Date (staff)")]),
 ("Community Partners Resource Team Release", [("Client_FullName", "I, ______"), ("Client_FullName", "Client Name"), ("App_Date", "Date")]),
 ("ESD Release to 3SquaresVT", [("Client_FullName", "Client Name"), ("Client_DOB", "Date of Birth"),
   ("Legal_Rep_Name", "Name if Legal Representative"), ("Client_FullName", "Printed name"), ("App_Date", "Date")]),
 ("Crisis Fuel Statement of Understanding (204NST)", [("Client_FullName", "I ______ (Applicant Name)"), ("App_Date", "Date (applicant)"),
   ("App_Date", "Date (Crisis Fuel Program Rep)")]),
 ("NEKCA Fuel Order", [("FO_Authorized", "Authorized ☐"), ("FO_Date", "Date"), ("FO_Time", "Time"), ("Staff_Name", "Staff"),
   ("Invoice_Email", "Send invoice to: Email"), ("Invoice_Phone", "Send invoice to: Phone"), ("Vendor_Name", "Vendor"),
   ("Vendor_Contact", "Contact email/#"), ("Vendor_Contact2", "Contact email/# (2nd line: phones)"), ("SFB_Remaining", "SFB remaining"), ("Tank_Size", "Size of tank"),
   ("Fuel_Remaining", "Fuel remaining"), ("Fuel_Lasts", "Time fuel will last"), ("Client_FullName", "Client Name"),
   ("Service_Address_1", "Service Address (line 1)"), ("Service_Address_2", "Service Address (line 2)"), ("Vendor_Acct", "Account #"),
   ("Fuel_Type", "Fuel Type"), ("FO_Fuel", "Fuel $"), ("FO_Gallons", "Quantity (gallons)"), ("FO_SpecialTrip", "Special Trip $"),
   ("FO_PressureTest", "Pressure Test $"), ("FO_StartUp", "Start Up $"), ("FO_Total", "Total $"), ("FO_EstDelivery", "Est. Delivery Date"),
   ("FO_Funding", "Grant/Funding Source"), ("INV_Received", "Invoice received ☐"), ("INV_Fuel", "After invoice: Fuel $"),
   ("INV_Gallons", "Quantity (gallons)"), ("INV_SpecialTrip", "Special Trip $"), ("INV_PressureTest", "Pressure Test $"),
   ("INV_StartUp", "Start Up $"), ("INV_Total", "Total $"), ("INV_DeliveryDate", "Delivery Date"), ("INV_Funding", "Grant/Funding Source")]),
 ("NEKCA Electric Pledge", [("EP_Authorized", "Authorized ☐"), ("EP_Date", "Date"), ("EP_Time", "Time"), ("Staff_Name", "Staff"),
   ("Electric_Provider", "Vendor"), ("EP_Contact", "Contact email/#"), ("EP_Contact2", "Contact email/# (2nd line: phones)"), ("Client_FullName", "Client Name"),
   ("Service_Address_1", "Service Address (line 1)"), ("Service_Address_2", "Service Address (line 2)"), ("Electric_Acct", "Account #"),
   ("EP_Disconnect_Notice", "Disconnect Notice Attached ☐"), ("EP_Amount", "Amount Required $"), ("EP_Reconnect", "Reconnection Fee $"),
   ("EP_Total", "Total $"), ("EP_Discount_Yes", "Discount/Forgiveness: Yes"), ("EP_Discount_No", "No"),
   ("EP_Disconnect_Date", "Disconnect Date"), ("EP_Funding", "Grant/Funding Source"), ("EP_Remaining", "Remaining Balance $"),
   ("EP_NextBill", "Next Bill $"), ("EP_NextBill_Due", "Next Bill Due by"), ("EP_Responsibilities", "Responsibilities of the client (notes)")]),
]
used_on = {}
for form, items in FORMS:
    for name, _ in items:
        assert name in FIELDS, (form, name)
        used_on.setdefault(name, [])
        if form not in used_on[name]:
            used_on[name].append(form)
unused = [n for n in ORDER if n not in used_on]
assert not unused, unused

CHECK_KINDS = ("yes", "choicecheck", "choicemulti")
def is_check(name):
    k = FIELDS[name]["kind"]
    if k in CHECK_KINDS: return True
    if k == "expr" and FIELDS[name]["expr"].endswith('"Yes","Off")'): return True
    return False

fm = wb.create_sheet("Field Map")
fm.sheet_view.showGridLines = False
for col, w in zip("ABCDEF", [2, 34, 30, 11, 46, 30]):
    fm.column_dimensions[col].width = w
fm["B1"] = "Field Map — name every PDF field exactly as listed"; fm["B1"].font = f(True, 14, NAVY)
fm["B2"] = ("Go form by form, top to bottom. Text field = text box. Checkbox = checkbox with export value  Yes  . "
            "The same name on two boxes (e.g. App_Date) fills both with the same value — that's intended. Names are case-sensitive.")
fm["B2"].font = f(False, 9, "595959", True); fm["B2"].alignment = Alignment(wrap_text=True)
fm.merge_cells("B2:F2"); fm.row_dimensions[2].height = 30
r = 4
for form, items in FORMS:
    for col in "BCDEF":
        fm[f"{col}{r}"].fill = HEAD_FILL
    fm[f"B{r}"] = form; fm[f"B{r}"].font = f(True, 10, "FFFFFF")
    r += 1
    for col, lab in zip("BCDEF", ["Box on the form", "PDF field name", "Field type", "What fills it", "Also used on"]):
        fm[f"{col}{r}"] = lab; fm[f"{col}{r}"].font = f(True, 9); fm[f"{col}{r}"].fill = SUB_FILL
    r += 1
    for name, box in items:
        fm[f"B{r}"] = box
        fm[f"C{r}"] = name; fm[f"C{r}"].font = Font(name="Consolas", size=10, bold=True)
        fm[f"D{r}"] = "Checkbox" if is_check(name) else "Text"
        fm[f"E{r}"] = FIELDS[name]["label"]
        others = [x for x in used_on[name] if x != form]
        fm[f"F{r}"] = "; ".join(others)
        for col in "BDEF":
            fm[f"{col}{r}"].font = f(size=9)
        for col in "BCDEF":
            fm[f"{col}{r}"].border = BOX
        r += 1
    r += 1
fm.freeze_panes = "B4"

# ================================================================ EXPORT XFDF
ex = wb.create_sheet("Export XFDF")
ex.column_dimensions["A"].width = 110
ex.column_dimensions["B"].width = 3
ex.column_dimensions["C"].width = 26
ex.column_dimensions["D"].width = 40
ex.sheet_view.showGridLines = False

def value_formula(name):
    d = FIELDS[name]
    ref, k = d["ref"], d["kind"]
    if k == "expr":
        return d["expr"]
    if k == "yes":
        return f'IF({ref}="Yes","Yes","Off")'
    if k == "choicemulti":
        return f'IF({any_of(ref, d["src"])},"Yes","Off")'
    if k == "choicecheck":
        return f'IF({ref}="{d["src"]}","Yes","Off")'
    if k == "date":
        return f'IF({ref}="","",TEXT({ref},"mm/dd/yyyy"))'
    if k == "money":
        return f'IF({ref}="","",TEXT({ref},"$#,##0.00"))'
    if k == "ssn":
        return SSN_FMT(ref)
    if k == "phone":
        return f'IF({ref}="","",{PH_FMT(ref)})'
    if k == "time":
        return f'IF({ref}="","",IF(ISNUMBER({ref}),TEXT({ref},"h:mm AM/PM"),{ref}&""))'
    if k == "pct":
        return f'IF({ref}="","",TEXT({ref},"0%"))'
    if k == "num":
        return f'IF(OR({ref}="",{ref}=0),"",{ref}&"")'
    return f'IF({ref}="","",{ref}&"")'

def esc(cell):
    return (f'SUBSTITUTE(SUBSTITUTE(SUBSTITUTE(SUBSTITUTE(SUBSTITUTE(SUBSTITUTE({cell},"&","&amp;"),"<","&lt;"),">","&gt;"),'
            f'"""","&quot;"),CHAR(13),""),CHAR(10),"&#10;")')

ex["C1"] = "Field name"; ex["D1"] = "Value sent to the PDF"
for cc in ("C1", "D1"):
    ex[cc].font = f(True, 9, "FFFFFF"); ex[cc].fill = HEAD_FILL
ex["A1"] = "<?xml version='1.0' encoding='UTF-8'?>"
ex["A2"] = "<xfdf xmlns='http://ns.adobe.com/xfdf/' xml:space='preserve'>"
ex["A3"] = "<fields>"
r = 4
export_names = []
seen = set()
for form, items in FORMS:
    for name, _ in items:
        if name in seen: continue
        seen.add(name); export_names.append(name)
for name in export_names:
    ex[f"C{r}"] = name
    ex[f"D{r}"] = "=" + value_formula(name)
    ex[f"A{r}"] = f"=\"<field name='\"&C{r}&\"'><value>\"&{esc(f'D{r}')}&\"</value></field>\""
    for cc in (f"C{r}", f"D{r}"):
        ex[cc].font = f(size=9)
    ex[f"A{r}"].font = Font(name="Consolas", size=9)
    r += 1
ex[f"A{r}"] = "</fields>"
ex[f"A{r+1}"] = "</xfdf>"
for rr in (1, 2, 3, r, r + 1):
    ex[f"A{rr}"].font = Font(name="Consolas", size=9)
XFDF_LAST = r + 1
wb.defined_names["XFDF_Output"] = DefinedName("XFDF_Output", attr_text=f"'Export XFDF'!$A$1:$A${XFDF_LAST}")
ex.freeze_panes = "A1"

# ================================================================ EXPORT TEXT (tab-delimited)
et = wb.create_sheet("Export Text")
et.sheet_view.showGridLines = False
for i, name in enumerate(export_names):
    col = get_column_letter(i + 1)
    et[f"{col}1"] = name
    et[f"{col}2"] = f"=SUBSTITUTE(SUBSTITUTE(SUBSTITUTE('Export XFDF'!$D${4 + i},CHAR(13),\"\"),CHAR(10),\" \"),CHAR(9),\" \")"
    et[f"{col}1"].font = f(True, 9); et[f"{col}2"].font = f(size=9)
    et.column_dimensions[col].width = 16
TEXT_LAST_COL = get_column_letter(len(export_names))
wb.defined_names["Text_Output"] = DefinedName("Text_Output", attr_text=f"'Export Text'!$A$1:${TEXT_LAST_COL}$2")

exec(open('/home/claude/fa/print_tabs.py', encoding='utf-8').read())

# ================================================================ START HERE
sh = wb.create_sheet("Start Here", 0)
sh.sheet_view.showGridLines = False
sh.column_dimensions["A"].width = 2
sh.column_dimensions["B"].width = 5
sh.column_dimensions["C"].width = 100
sh["B1"] = "NEKCA Crisis Fuel / Electric — Client Intake Workbook"; sh["B1"].font = f(True, 16, NAVY)
sh["B2"] = "One copy of this workbook per client, saved in that client's folder. Type everything once here; the forms fill from it."
sh["B2"].font = f(False, 10, "595959", True)
steps = [
    ("FILL IN", None),
    ("1", "Tab 1 Client — application date, staff, client, contact, housing, benefits, interests."),
    ("2", "Tab 2 Household — one row per person, client first. Pick Occupation only for members 14+."),
    ("3", "Tab 3 Application — crisis details, Seasonal Fuel status, referrals, determination."),
    ("4", "Tab 4 Income & Expenses — monthly amounts. FPL % and the under-200% check calculate automatically."),
    ("5", "Tab 5 Fuel & Electric — vendor/accounts, then the Fuel Order or Electric Pledge. Fill 'after invoice' later."),
    ("Tab", "Press Tab to jump to the next yellow cell (locked cells are skipped). Just start typing — no double-click needed; F2 edits what's already there. To change labels or layout: Review → Unprotect Sheet (no password)."),
    ("Colors", "Yellow cells = type here.  Blue cells = calculated, don't type over them.  Grey italic text = examples of the expected format."),
    ("PRINT THE FORMS  (green tabs)", None),
    ("•", "Cover Sheet = the packet cover & status sheet, pre-filled. GoEngage Data = every value to type into GoEngage. Both are for staff only — don't upload them."),
    ("•", "Easiest: run Print Packets — it saves the cover sheet and each GoEngage packet as named PDFs in the client's folder."),
    ("1", "Hold Ctrl and click each green 'Form -' tab you need (for example both intake pages)."),
    ("2", "File → Export → Create PDF/XPS → Options → 'Active sheet(s)' → Publish. You get one PDF with those forms, ready to print and sign."),
    ("3", "Right-click any tab → Ungroup Sheets when done, so typing doesn't go to several tabs at once."),
    ("FILL THE PDFs  (XFDF — only for forms that stay as PDFs)", None),
    ("1", "Go to the Export XFDF tab. Click the column A header to select the whole column, then Ctrl+C."),
    ("2", "Open Notepad, Ctrl+V. Save As: File name  Smith_Jane.xfdf  ·  Save as type  All files  ·  Encoding  UTF-8."),
    ("3", "In PDF Craft, open each blank fillable form and use its import form data command on that .xfdf file. Every field with a matching name fills."),
    ("4", "Save each filled form as a new PDF in the client's folder (keep the blank originals untouched)."),
    ("FILL THE PDFs  (text — only if PDF Craft won't take .xfdf)", None),
    ("1", "Go to the Export Text tab. Select rows 1 and 2 (click the row 1 header, Shift+click row 2), Ctrl+C."),
    ("2", "Paste into Notepad and save as  Smith_Jane.txt  (UTF-8). Import that file into each form instead."),
    ("BUILDING THE FILLABLE PDFs", None),
    ("•", "Use the Field Map tab: it lists every box on every form, top to bottom, with the exact field name to give it."),
    ("•", "Checkboxes must have the export value  Yes  . Signature lines stay blank — clients sign on paper."),
    ("•", "Each year: update the poverty table on the Settings tab. Type the invoice email/phone there once."),
    ("VENDOR LIST  (one-time setup in the master, Excel desktop)", None),
    ("1", "Data → Get Data → From Online Services → From SharePoint Online List. Site URL: https://nekca.sharepoint.com/sites/3025CrisisFuel0731  (sign in with your work account)."),
    ("2", "Tick Fuel Team Vendor List → Transform Data. Check the vendor-name column header says  Vender Name  (rename it if it says Title)."),
    ("3", "Fuel Offered column: click the ↔ button in its header → Extract Values → Comma. (Same for any other column showing 'List'.)"),
    ("4", "Home → Close & Load To… → Table → Existing worksheet → click cell A1 on the Vendors tab → OK."),
    ("5", "Data → Queries & Connections → right-click the query → Properties → tick 'Refresh data when opening the file' → OK. Save the master."),
    ("•", "To update later: edit the SharePoint List, then Data → Refresh All. Columns are matched by name, so their order doesn't matter."),
    ("•", "The List lives on the 3025 Crisis Fuel team site, so everyone on that team can refresh it. Keep the column names it uses (Vender Name, Location, Contact Emails, Contact Phone Numbers, Fuel Offered)."),
    ("PRIVACY", None),
    ("•", "This file holds SSNs and income. Keep it only in the client's SharePoint folder — not on the desktop or in email."),
]
r = 4
for a_, b_ in steps:
    if b_ is None:
        r += 1
        sh[f"B{r}"] = a_; sh[f"B{r}"].font = f(True, 11, "FFFFFF")
        sh[f"B{r}"].fill = HEAD_FILL; sh[f"C{r}"].fill = HEAD_FILL
    else:
        sh[f"B{r}"] = a_; sh[f"B{r}"].font = f(True); sh[f"B{r}"].alignment = Alignment(horizontal="center", vertical="top")
        sh[f"C{r}"] = b_; sh[f"C{r}"].font = f(); sh[f"C{r}"].alignment = Alignment(wrap_text=True, vertical="top")
    r += 1
sh["B5"].alignment = Alignment(horizontal="left")

# legend swatches on start sheet
sh[f"C{r+1}"] = "Example yellow input cell"; sh[f"C{r+1}"].fill = INPUT_FILL; sh[f"C{r+1}"].font = f()
sh[f"C{r+2}"] = "Example blue calculated cell"; sh[f"C{r+2}"].fill = CALC_FILL; sh[f"C{r+2}"].font = f(color=NAVY)

# order: Start, 1..5, Field Map, Export XFDF, Export Text, Settings
order = ["Start Here", "1 Client", "2 Household", "3 Application", "4 Income & Expenses", "5 Fuel & Electric"] + PRINT_TABS + [
         "Export XFDF", "Export Text", "Field Map", "Staff", "Vendors", "Vendor Lookup", "Lists", "Settings"]
wb._sheets = [wb[n] for n in order]
for n, color in [("Export XFDF", "7F7F7F"), ("Export Text", "7F7F7F"), ("Field Map", "7F7F7F"), ("Settings", "7F7F7F")]:
    wb[n].sheet_properties.tabColor = color
for n in order[1:6]:
    wb[n].sheet_properties.tabColor = "F2C200"
wb.active = 0

for ws in wb.worksheets:
    if ws.title.startswith("Form - "):
        continue
    ws.sheet_properties.pageSetUpPr.fitToPage = True
    ws.page_setup.fitToWidth = 1
    ws.page_setup.fitToHeight = 0
    ws.page_setup.paperSize = ws.PAPERSIZE_LETTER
    if ws.title in ("2 Household", "4 Income & Expenses"):
        ws.page_setup.orientation = "landscape"
# ---- keyboard shortcuts banner (row 4 of each data tab). Add more shortcuts to this list.
SHORTCUTS = [
    ("Alt + ↓", "open a dropdown list"),
    ("Ctrl + ;", "today's date"),
    ("Ctrl + Shift + ;", "current time"),
    ("Tab / Shift + Tab", "next / previous field"),
]
SC_TEXT = "Keyboard shortcuts:     " + "     ·     ".join(f"{k}  {v}" for k, v in SHORTCUTS)
for name in ["1 Client", "2 Household", "3 Application", "4 Income & Expenses", "5 Fuel & Electric"]:
    ws = wb[name]
    last_col = 4 if name in ("1 Client", "3 Application", "5 Fuel & Electric") else (7 if name == "4 Income & Expenses" else 18)
    ws.merge_cells(start_row=4, start_column=2, end_row=4, end_column=last_col)
    c4 = ws.cell(row=4, column=2)
    c4.value = SC_TEXT
    c4.font = f(True, 9, NAVY)
    c4.fill = PatternFill("solid", fgColor="EDEDED")
    c4.alignment = Alignment(vertical="center", horizontal="left")
    ws.row_dimensions[4].height = 20
    if name in ("1 Client", "3 Application", "5 Fuel & Electric"):
        ws.freeze_panes = "A5"

# ---- protection: only yellow input cells are selectable, so Tab jumps between them
from openpyxl.styles import Protection
for name in ["1 Client", "2 Household", "3 Application", "4 Income & Expenses", "5 Fuel & Electric", "Settings"]:
    ws = wb[name]
    for row in ws.iter_rows():
        for cell in row:
            if cell.fill is not None and cell.fill.fgColor is not None and cell.fill.fgColor.rgb in ("00FFF2CC", "FFFFF2CC"):
                cell.protection = Protection(locked=False)
    for mr in ws.merged_cells.ranges:
        anchor = ws.cell(row=mr.min_row, column=mr.min_col)
        if anchor.protection.locked is False:
            for rr in range(mr.min_row, mr.max_row + 1):
                for cc in range(mr.min_col, mr.max_col + 1):
                    ws.cell(row=rr, column=cc).protection = Protection(locked=False)
    ws.protection.sheet = True
    ws.protection.selectLockedCells = True
    ws.protection.selectUnlockedCells = False
    ws.protection.formatColumns = False
    ws.protection.formatRows = False
out = "/home/claude/fa/Fuel_Assistance_Intake.xlsx"
wb.save(out)
print("fields:", len(export_names), "saved", out)
