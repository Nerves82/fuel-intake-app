# Included into build.py via exec(): builds print-ready form tabs.
from openpyxl.drawing.image import Image as XLImage
from openpyxl.worksheet.page import PageMargins

EXROW = {name: 4 + i for i, name in enumerate(export_names)}
SYM = "Segoe UI Symbol"

def V(name):
    """Reference to the formatted export value of a field."""
    return f"'Export XFDF'!$D${EXROW[name]}"

def CB(name):
    return f'IF({V(name)}="Yes","☒","☐")'

class PrintTab:
    def __init__(self, title, widths, landscape=False):
        ws = wb.create_sheet(title)
        self.ws = ws
        ws.sheet_view.showGridLines = False
        ws.sheet_view.view = "pageLayout"
        for i, w in enumerate(widths):
            ws.column_dimensions[get_column_letter(i + 1)].width = w
        self.ncols = len(widths)
        ws.page_setup.paperSize = ws.PAPERSIZE_LETTER
        ws.page_setup.orientation = "landscape" if landscape else "portrait"
        ws.sheet_properties.pageSetUpPr.fitToPage = True
        ws.page_setup.fitToWidth = 1
        ws.page_setup.fitToHeight = 1
        ws.page_margins = PageMargins(left=0.4, right=0.4, top=0.4, bottom=0.4, header=0.2, footer=0.2)
        ws.print_options.horizontalCentered = True
        ws.sheet_properties.tabColor = "70AD47"

    def put(self, r, c1, c2, value, bold=False, size=10, italic=False, underline=None, align="left",
            valign="center", wrap=False, line=False, fill=None, sym=False, rotate=0, border=False, color="000000"):
        ws = self.ws
        a = f"{get_column_letter(c1)}{r}"
        if c2 > c1:
            ws.merge_cells(f"{a}:{get_column_letter(c2)}{r}")
        cell = ws[a]
        cell.value = value
        cell.font = Font(name=SYM if sym else FONT, bold=bold, size=size, italic=italic, underline=underline, color=color)
        cell.alignment = Alignment(horizontal=align, vertical=valign, wrap_text=wrap, text_rotation=rotate)
        for c in range(c1, c2 + 1):
            x = ws.cell(row=r, column=c)
            if line:
                x.border = Border(bottom=Side(style="thin", color="000000"))
            if border:
                x.border = BOX_BLACK
            if fill:
                x.fill = PatternFill("solid", fgColor=fill)
        return cell

    def val(self, r, c1, c2, name, **kw):
        kw.setdefault("color", "1F3B57")
        return self.put(r, c1, c2, f"={V(name)}", line=True, **kw)

    def h(self, r, height):
        self.ws.row_dimensions[r].height = height

BOX_BLACK = Border(left=Side(style="thin", color="000000"), right=Side(style="thin", color="000000"),
                   top=Side(style="thin", color="000000"), bottom=Side(style="thin", color="000000"))
YELLOW, CYAN = "FFFF00", "00FFFF"

# ================================================================ INTAKE PAGE 1 (landscape)
p = PrintTab("Form - Intake p1", [16, 14, 11, 7, 5, 8, 9, 8, 11, 9, 11, 9, 12], landscape=True)
ws = p.ws
logo = XLImage("/home/claude/fa/img/logo-000.png")
logo.width, logo.height = 150, 88
ws.add_image(logo, "A1")
p.put(1, 3, 13, "NEKCA STANDARD INTAKE FORM – Page 1 of 2", bold=True, size=14, underline="single", align="center")
p.h(1, 24)
p.put(2, 3, 5, "Physical Address:"); p.val(2, 6, 13, "Address_Physical")
p.put(3, 3, 5, "City/Town, State, Zip:"); p.val(3, 6, 13, "Address_CityStateZip")
p.put(4, 3, 5, "Mailing Address: Same or")
p.put(4, 6, 13, '=IF(' + V("Address_Mailing") + '="","Same",' + V("Address_Mailing") + ')', line=True, color="1F3B57")
p.put(5, 3, 5, "Phone Number:"); p.val(5, 6, 8, "Phone")
p.put(5, 9, 10, "Email (optional):", align="right"); p.val(5, 11, 13, "Email")
p.put(6, 1, 1, "Primary Language:"); p.val(6, 2, 3, "Primary_Language")
p.put(6, 4, 7, "Other (Please Specify):", align="right"); p.val(6, 8, 10, "Language_Other")
p.put(7, 1, 3, "English Fluency (if applicable):"); p.val(7, 4, 6, "English_Fluency")
p.put(7, 8, 10, "Translator Needed? (Y/N):", align="right"); p.val(7, 11, 11, "Translator_Needed", align="center")
for r in range(2, 8): p.h(r, 18)
p.put(8, 1, 13, "Please complete the information below for every member of your household. These include questions "
      "mandated by our federal funding source. Thank you for your assistance.", bold=True, italic=True, underline="single",
      align="center", wrap=True)
p.h(8, 30)
heads = ["Last Name", "First Name", "Date of Birth", "Gender Identity", "Disabled (Y/N)", "Last Grade Completed", "Race",
         "Hispanic or Not Hispanic", "Health Insurance Source (Employer, Medicaid, Etc.)", "Income per month", "Source",
         "Military Status (Veteran/ Active/ None)", "SS #"]
keys = ["Last", "First", "DOB", "Gender", "Disabled", "Grade", "Race", "Hispanic", "HealthIns", "Income", "IncomeSource", "Military", "SSN"]
for i, hd in enumerate(heads):
    p.put(9, i + 1, i + 1, hd, size=8, rotate=90, align="center", valign="bottom", wrap=True, border=True)
p.h(9, 95)
for n in range(1, 11):
    r = 9 + n
    for i, k in enumerate(keys):
        p.put(r, i + 1, i + 1, f"={V(f'HH{n}_{k}')}", size=8, border=True, wrap=True, color="1F3B57",
              align="center" if k in ("Disabled", "DOB", "Income") else "left")
    p.h(r, 26)
p.put(21, 1, 6, "I certify that all information is correct to the best of my knowledge.", italic=True)
p.put(21, 8, 13, "All information provided is private and confidential.", italic=True)
p.put(22, 1, 2, "Participant Signature:", fill=YELLOW); p.put(22, 3, 4, None, line=True)
p.put(22, 5, 5, "Date:", fill=YELLOW, align="right"); p.val(22, 6, 7, "App_Date", size=9)
p.put(22, 8, 9, "NEKCA Staff:", fill=CYAN); p.val(22, 10, 11, "Staff_Name")
p.put(22, 12, 12, "Date:", fill=CYAN, align="right"); p.val(22, 13, 13, "App_Date", size=9)
p.h(22, 22)
p.put(23, 10, 13, "PLEASE CONTINUE TO PAGE 2", bold=True, size=9, align="right")
ws.print_area = "A1:M23"

# ================================================================ APPLICATION (portrait)
p = PrintTab("Form - Application", [10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 11])
ws = p.ws
L = 11  # last text column; column 12 holds rule citations
def rule(r, text):
    p.put(r, 12, 12, text, size=9, align="right", valign="top", wrap=True)
r = 1
p.put(r, 1, 12, "NEKCA Fuel Assistance Application", bold=True, size=13, align="center"); p.h(r, 24)
r = 2
p.put(r, 1, 2, "Client Name:"); p.val(r, 3, 10, "Client_FullName", size=11); p.h(r, 22)
r = 3
p.put(r, 1, 4, f'="AAA employee applying on behalf "&{CB("App_AAA_OnBehalf")}&"  3103(c)"', size=9)
p.put(r, 5, 8, f'="Afterhours Follow-up "&{CB("App_AfterHours")}&"  3105(i)"', size=9, align="center")
p.put(r, 9, 12, f'="Second Crisis Assist "&{CB("App_SecondCrisis")}&"  3109(c)"', size=9, align="right")
r = 4
p.put(r, 1, 3, f'={CB("Crisis_LowFuel")}&" Low on Fuel"', bold=True)
p.put(r, 4, 5, "Current Quantity:", align="right"); p.val(r, 6, 9, "Current_Quantity")
rule(r, "3102(c)5i, 5ii, 5iii"); p.h(r, 26)
r = 5
p.put(r, 1, 3, f'={CB("Crisis_Disconnect")}&" Disconnect Notice"', bold=True)
r = 6
p.put(r, 1, 11, f'="      For Electric required for heating system "&{CB("Elec_For_Heat")}&"      or Propane "&{CB("Elec_Propane")}')
r = 7
p.put(r, 1, 6, "      Which component of the heating system does the electric run?"); p.val(r, 7, 11, "Elec_Component")
r = 8
p.put(r, 1, 11, f'="      Landlord Verification form required (rents, multi-family home AND heating system located outside of"&CHAR(10)&"      living unit) Form 204VEF?          Yes "&{CB("Landlord_Verif_Yes")}&"          No "&{CB("Landlord_Verif_No")}',
      wrap=True)
rule(r, "3107(c)"); p.h(r, 30)
r = 9
p.put(r, 1, 11, f'="Heat included in rent "&{CB("Heat_In_Rent")}&"          AND meets rule exception 3102(d) "&{CB("Rule_Exception")}')
rule(r, "3102(d)"); p.h(r, 22)
r = 10
p.put(r, 1, 11, f'="Is this a life-threatening crisis (documentation of medical condition must be provided)? "&{CB("Life_Threatening")}')
rule(r, "3103"); p.h(r, 22)
r = 11
p.put(r, 1, 6, "Unforeseen/Extenuating Circumstance:"); rule(r, "3102(c)6")
r = 12
p.put(r, 1, 11, f"={V('Circumstance')}", wrap=True, valign="top", color="1F3B57", line=True); p.h(r, 42)
r = 13
p.put(r, 1, 11, f'="Seasonal Fuel Status:          "&{CB("SFA_Active")}&" Active          "&{CB("SFA_Pending")}&" Pending          "&{CB("SFA_Closed")}&" Closed/Denied"')
p.h(r, 24)
r = 14
p.put(r, 1, 11, "NOTICE:  If documents are not provided, this application will be closed in 5 business days. If entire "
      "application is not signed, no future assistance will be provided.", wrap=True, size=9)
rule(r, "3104(f)"); p.h(r, 28)
r = 15
p.put(r, 1, 11, f'="Appeal Notice provided "&{CB("Appeal_Notice")}'); rule(r, "3112")
r = 16
p.put(r, 1, 2, "Client Signature:", fill=YELLOW); p.put(r, 3, 8, None, line=True)
p.put(r, 9, 9, "Date:", fill=YELLOW, align="right"); p.val(r, 10, 11, "App_Date"); p.h(r, 24)
r = 17
p.put(r, 1, 11, f'="Phone application "&{CB("Phone_App")}'); rule(r, "3104(b)")
r = 18
p.put(r, 1, 1, "Reason:"); p.val(r, 2, 7, "Phone_App_Reason")
p.put(r, 8, 9, "Staff Initials:", align="right"); p.val(r, 10, 11, "Staff_Initials")
r = 19
for c in range(1, 13):
    ws.cell(row=r, column=c).border = Border(bottom=Side(style="medium", color="000000"))
p.h(r, 8)
r = 20
p.put(r, 1, 3, "Referrals", bold=True); p.h(r, 20)
r = 21
p.put(r, 1, 4, f'="     Weatherization/NETO "&{CB("Ref_NETO")}')
p.put(r, 5, 8, f'="Financial Coaching "&{CB("Ref_FinCoaching")}', align="center")
p.put(r, 9, 12, f'="Whole Family Approach "&{CB("Ref_WholeFamily")}', align="right")
r = 22
p.put(r, 1, 4, f'="     Agency on Aging "&{CB("Ref_Aging")}')
p.put(r, 5, 8, f'="Community Connections "&{CB("Ref_CommConnections")}', align="center")
p.put(r, 9, 12, f'="VITA "&{CB("Ref_VITA")}', align="right")
r = 24
p.put(r, 1, 3, f'="Denied "&{CB("Denied")}', bold=True)
den = [("Deny_ExpeditedSFA", "Obtained Expedited SFA"), ("Deny_NoCrisis", "No home heating crisis"),
       ("Deny_NoCircumstance", "No unforeseen/extenuating circumstance"),
       ("Deny_Over200", "Over 200% FPL"), ("Deny_Resources", "Accessible Resources"), ("Deny_FundsUse", "Use of funds over the past 30 days"),
       ("Deny_NoSFB", "Not receiving SFB"), ("Deny_MaxGrants", "Max # of Grants"), ("Deny_PriorConditions", "Did not meet conditions of prior crisis assist")]
for i, (k, lab) in enumerate(den):
    rr = 25 + i // 3
    c1 = [1, 5, 8][i % 3]; c2 = [4, 7, 12][i % 3]
    p.put(rr, c1, c2, f'="{lab} "&{CB(k)}', size=9)
p.put(28, 1, 6, f'="Other – Did not return with documentation "&{CB("Deny_NoDocs")}', size=9)
r = 30
p.put(r, 1, 2, f'="Warmth Eligible "&{CB("Warmth_Eligible")}', bold=True)
p.put(r, 3, 3, "Date:", align="right"); p.val(r, 4, 5, "Warmth_Date")
p.put(r, 7, 8, f'="VLITE Eligible "&{CB("VLITE_Eligible")}', bold=True)
p.put(r, 9, 9, "Date:", align="right"); p.val(r, 10, 11, "VLITE_Date"); p.h(r, 22)
r = 31
p.put(r, 1, 2, f'="Crisis Fuel Eligible "&{CB("Crisis_Eligible")}', bold=True)
p.put(r, 3, 3, "Date:", align="right"); p.val(r, 4, 5, "Crisis_Elig_Date")
p.put(r, 7, 7, "Time:", align="right"); p.val(r, 8, 9, "Crisis_Elig_Time"); rule(r, "3108(d)"); p.h(r, 22)
r = 32
p.put(r, 1, 4, f'="NEKCA Intake forms complete "&{CB("Chk_Intake")}', size=9)
p.put(r, 5, 8, f'="ACCESS Verification complete "&{CB("Chk_ACCESS")}', size=9, align="center")
p.put(r, 9, 12, f'="Consent forms complete "&{CB("Chk_Consents")}', size=9, align="right")
r = 33
p.put(r, 1, 5, f'="Circumstance included in Service Note "&{CB("Chk_ServiceNote")}', size=9)
p.put(r, 6, 11, f'="Missing documents exception "&{CB("Chk_MissingDocs")}&"  3107(e)"', size=9)
r = 35
p.put(r, 1, 2, "Staff Signature:", fill=CYAN); p.put(r, 3, 8, None, line=True)
p.put(r, 9, 9, "Date:", fill=CYAN, align="right"); p.val(r, 10, 11, "App_Date"); p.h(r, 26)
for rr in range(1, 36):
    if ws.row_dimensions[rr].height is None:
        ws.row_dimensions[rr].height = 19
ws.print_area = "A1:L35"


def lv(p, r, c1, c2, label, c3, c4, name, lalign="left", **kw):
    p.put(r, c1, c2, label, align=lalign)
    p.val(r, c3, c4, name, **kw)

def money(p, r, c1, c2, label, c3, c4, name):
    p.put(r, c1, c2, label, align="right")
    p.val(r, c3, c4, name, align="right")

def finish(p, last_row, last_col, default_h=20):
    for rr in range(1, last_row + 1):
        if p.ws.row_dimensions[rr].height is None:
            p.ws.row_dimensions[rr].height = default_h
    p.ws.print_area = f"A1:{get_column_letter(last_col)}{last_row}"

def opt(name, text):
    return f'{CB(name)}&" {text}"'

# ================================================================ INTAKE PAGE 2 (landscape)
p = PrintTab("Form - Intake p2", [11] * 13, landscape=True)
ws = p.ws
p.put(1, 1, 13, "NEKCA INTAKE PAGE 2 of 2", bold=True, size=13, align="center"); p.h(1, 24)
p.put(2, 1, 13, "Please complete the information below for your household. These include questions mandated by our federal "
      "funding source. Thank you for your assistance.", size=9, align="center", wrap=True); p.h(2, 26)
r = 4
p.put(r, 1, 2, "Housing Type:", bold=True)
p.put(r, 3, 4, f'={opt("Housing_Own", "Own")}'); p.put(r, 5, 6, f'={opt("Housing_Rent", "Rent")}')
p.put(r, 7, 10, f'={opt("Housing_Homeless", "Homeless/Temporary Shelter")}'); p.put(r, 11, 13, f'={opt("Housing_Other", "Other")}')
r = 6
p.put(r, 1, 2, "Household Type:", bold=True)
p.put(r, 3, 5, f'={opt("HHType_Single", "Single Individual")}'); p.put(r, 6, 8, f'={opt("HHType_TwoAdults", "Two Adults NO Children")}')
p.put(r, 9, 10, f'={opt("HHType_SingleFemale", "Single Parent Female")}'); p.put(r, 11, 13, f'={opt("HHType_SingleMale", "Single Parent Male")}')
r = 7
p.put(r, 3, 5, f'={opt("HHType_TwoParent", "Two Parent HH")}'); p.put(r, 6, 8, f'={opt("HHType_NonRelated", "Non-Related Adults With Children")}')
p.put(r, 9, 10, f'={opt("HHType_Multigen", "Multigenerational HH")}'); p.put(r, 11, 13, f'={opt("HHType_Other", "Other")}')
r = 9
p.put(r, 1, 13, "Do You Receive Any Of The Following? (Please Check All That Apply):", bold=True)
r = 10
snap = (f'={CB("Benefit_SNAP")}&" SNAP (Amount: "&IF({V("SNAP_Amount")}="","________",{V("SNAP_Amount")})'
        f'&" / "&IF({V("SNAP_Period")}="","_____",{V("SNAP_Period")})&")"')
p.put(r, 1, 3, snap); p.put(r, 4, 5, f'={opt("Benefit_WIC", "WIC")}'); p.put(r, 6, 7, f'={opt("Benefit_LIHEAP", "LIHEAP")}')
p.put(r, 8, 10, f'={opt("Benefit_HCV", "Housing Choice Voucher")}'); p.put(r, 11, 13, f'={opt("Benefit_PublicHousing", "Public Housing")}')
r = 11
p.put(r, 1, 3, f'={opt("Benefit_PSH", "Permanent Supportive Housing")}'); p.put(r, 4, 5, f'={opt("Benefit_HUDVASH", "HUD-VASH")}')
p.put(r, 6, 8, f'={opt("Benefit_Childcare", "Childcare Voucher")}'); p.put(r, 9, 12, f'={opt("Benefit_ACA", "Affordable Care Act Subsidy")}')
r = 13
p.put(r, 1, 13, "FOR Household Members 14 + (Occupation Status)", bold=True)
OCC_LAB = [("EmployedFT", "Employed FT"), ("EmployedPT", "Employed PT"), ("Retired", "Retired"), ("Student", "Student"),
           ("UnempShort", "Unemployed (< 6 Months)"), ("UnempLong", "Unemployed (6 months+)"), ("Migrant", "Migrant Worker")]
spans = [(1, 2), (3, 4), (5, 5), (6, 6), (7, 9), (10, 11), (12, 13)]
r = 14
for k in range(1, 5):
    p.put(r, 1, 1, "Name:"); p.val(r, 2, 5, f"Occ{k}_Name")
    r += 1
    for (key, lab), (c1, c2) in zip(OCC_LAB, spans):
        p.put(r, c1, c2, f'={opt(f"Occ{k}_{key}", lab)}', size=9)
    r += 2
r = 26
p.put(r, 1, 13, "Programs / Service Categories of Interest:", bold=True)
r = 27
p.put(r, 1, 2, f'={opt("Interest_FoodClothing", "Food / Clothing")}'); p.put(r, 3, 5, f'={opt("Interest_Housing", "Housing Support")}')
p.put(r, 6, 7, f'={opt("Interest_Fuel", "Fuel Assistance")}'); p.put(r, 8, 13, f'={opt("Interest_Economic", "Economic Programs (e.g. VITA, MicroBusiness, Coaching)")}')
r = 28
p.put(r, 1, 2, f'={opt("Interest_Youth", "Youth Programs")}'); p.put(r, 3, 5, f'={opt("Interest_Family", "Parent/Family Supports")}')
p.put(r, 6, 7, f'={opt("Interest_TEFAP", "TEFAP/USDA")}'); p.put(r, 8, 10, f'={opt("Interest_Justice", "Community Justice Programs")}')
p.put(r, 11, 11, f'={opt("Interest_Other", "Other:")}'); p.val(r, 12, 13, "Interest_Other_Text")
r = 30
p.put(r, 1, 6, "I certify that all information is correct to the best of my knowledge.", italic=True)
p.put(r, 8, 13, "All information provided is private and confidential.", italic=True)
r = 31
p.put(r, 1, 2, "Participant Signature:", fill=YELLOW); p.put(r, 3, 4, None, line=True)
p.put(r, 5, 5, "Date:", fill=YELLOW, align="right"); p.val(r, 6, 6, "App_Date", size=9)
p.put(r, 8, 9, "NEKCA Staff:", fill=CYAN); p.val(r, 10, 11, "Staff_Name")
p.put(r, 12, 12, "Date:", fill=CYAN, align="right"); p.val(r, 13, 13, "App_Date", size=9)
p.h(r, 22)
finish(p, 31, 13, 18)

# ================================================================ ACCESS VERIFICATION (portrait)
p = PrintTab("Form - ACCESS", [10] * 12)
p.put(1, 1, 12, "ACCESS Verification", bold=True, size=13, align="center"); p.h(1, 24)
p.put(2, 11, 12, "3104(e)", size=9, align="right")
r = 3
for n in range(1, 5):
    if n == 1:
        p.put(r, 1, 2, "Head of Household:"); p.val(r, 3, 7, f"HH{n}_FullName")
    else:
        p.put(r, 1, 1, "Name:"); p.val(r, 2, 7, f"HH{n}_FullName")
    p.put(r, 8, 8, "SSN:", align="right"); p.val(r, 9, 10, f"HH{n}_SSN")
    p.put(r, 11, 12, f'="Disabled "&{CB(f"HH{n}_Disabled_Check")}', align="right")
    r += 1
    p.put(r, 1, 3, "Income (quantity and source):"); p.val(r, 4, 9, f"HH{n}_Income_Text")
    p.put(r, 10, 10, "Date:", align="right"); p.val(r, 11, 12, f"HH{n}_Income_Date")
    p.h(r, 22)
    r += 2
p.put(r, 1, 4, "Non-Income (quantity and source, ex. SNAP):"); p.val(r, 5, 9, "NonInc1_Text")
p.put(r, 10, 10, "Date:", align="right"); p.val(r, 11, 12, "NonInc1_Date"); r += 1
for k in (2, 3):
    p.put(r, 5, 9, None, line=True); p.put(r, 10, 10, "Date:", align="right"); p.put(r, 11, 12, None, line=True); r += 1
r += 1
p.put(r, 1, 12, f'="Seasonal Fuel Status:          "&{CB("SFA_Active")}&" Active          "&{CB("SFA_Pending")}&" Pending          "&{CB("SFA_Closed")}&" Closed/Denied"')
r += 2
p.put(r, 1, 2, "Amount Granted:"); p.val(r, 3, 5, "SFA_Amount_Granted")
p.put(r, 7, 8, "Date Granted:", align="right"); p.val(r, 9, 11, "SFA_Date_Granted")
r += 2
p.put(r, 1, 1, "Fuel Type:"); p.val(r, 2, 4, "Fuel_Type")
p.put(r, 5, 6, "Service Address:", align="right"); p.val(r, 7, 12, "Service_Address_1"); r += 1
p.val(r, 7, 12, "Service_Address_2")
r += 2
p.put(r, 1, 1, "Vendor:"); p.val(r, 2, 6, "Vendor_Name")
p.put(r, 7, 8, "Contact email/#:", align="right"); p.val(r, 9, 11, "Vendor_Contact"); p.put(r, 12, 12, "3104(m)1", size=9, align="right")
r += 1
p.put(r, 1, 1, "Account #:"); p.val(r, 2, 4, "Vendor_Acct"); p.put(r, 12, 12, "3104(m)2", size=9, align="right")
r += 1
p.put(r, 1, 2, "Electric Provider:"); p.val(r, 3, 6, "Electric_Provider")
p.put(r, 7, 8, "Account #:", align="right"); p.val(r, 9, 11, "Electric_Acct"); p.put(r, 12, 12, "3104(m)3", size=9, align="right")
r += 2
p.put(r, 1, 1, "Notes:", valign="top")
p.put(r, 2, 12, f"={V('ACCESS_Notes')}", wrap=True, valign="top", color="1F3B57"); p.h(r, 60)
r += 2
p.put(r, 1, 2, "Staff Initials:"); p.val(r, 3, 4, "Staff_Initials")
finish(p, r, 12, 22)

# ================================================================ INCOME / EXPENSE WORKSHEET (portrait)
p = PrintTab("Form - Income Expense", [40, 12, 2, 29, 12, 12])
p.put(1, 1, 6, "Income/Expense Worksheet", bold=True, size=13, align="center"); p.h(1, 24)
r = 2
for c, t in zip(range(1, 7), ["Gross Income", "Monthly Amount ($)", None, "Expenses", "Paid in Last 30 Days ($)", "Bills ($)"]):
    if t: p.put(r, c, c, t, bold=True, size=9, wrap=True, border=True, fill="D9D9D9", align="center" if c != 1 and c != 4 else "left")
p.h(r, 30)
for i, (key, lab) in enumerate(INCOMES):
    rr = 3 + i
    p.put(rr, 1, 1, lab, size=9, border=True); p.put(rr, 2, 2, f"={V('Inc_' + key)}", size=9, border=True, align="right", color="1F3B57")
for i, (key, lab) in enumerate(EXPENSES):
    rr = 3 + i
    p.put(rr, 4, 4, lab, size=9, border=True)
    p.put(rr, 5, 5, f"={V('Exp_' + key + '_Paid')}", size=9, border=True, align="right", color="1F3B57")
    p.put(rr, 6, 6, f"={V('Exp_' + key + '_Bill')}", size=9, border=True, align="right", color="1F3B57")
rr = 3 + len(INCOMES) + 1
p.put(rr, 1, 1, "Any Income Change in the next month?", size=9, align="right")
p.put(rr, 2, 2, f'={CB("Inc_Change_Yes")}&" Yes"', size=9)
p.put(rr + 1, 2, 2, f'={CB("Inc_Change_No")}&" No"', size=9)
p.put(rr + 2, 1, 1, "If yes, explain:", size=9)
p.ws.merge_cells(start_row=rr + 3, start_column=1, end_row=rr + 6, end_column=2)
c_ = p.ws.cell(row=rr + 3, column=1); c_.value = f"={V('Inc_Change_Explain')}"
c_.font = Font(name=FONT, size=9, color="1F3B57"); c_.alignment = Alignment(wrap_text=True, vertical="top")
tr = 3 + len(EXPENSES)
p.put(tr, 1, 1, "Total Gross Monthly Income:", bold=True, size=9, border=True)
p.put(tr, 2, 2, f"={V('Inc_Total')}", bold=True, size=9, border=True, align="right", color="1F3B57")
p.put(tr, 4, 4, "Total Expenses:", bold=True, size=9, border=True)
p.put(tr, 5, 5, f"={V('Exp_Total_Paid')}", bold=True, size=9, border=True, align="right", color="1F3B57")
p.put(tr, 6, 6, f"={V('Exp_Total_Bill')}", bold=True, size=9, border=True, align="right", color="1F3B57")
r = tr + 2
p.put(r, 1, 1, "Income – Expenses (difference):", align="right"); p.val(r, 2, 2, "Inc_Minus_Exp", align="right")
p.put(r, 4, 4, "Checking:", align="right"); p.val(r, 5, 5, "Asset_Checking", align="right")
r += 1
p.put(r, 1, 1, "Savings:", align="right"); p.val(r, 2, 2, "Asset_Savings", align="right")
p.put(r, 4, 4, "Cash:", align="right"); p.val(r, 5, 5, "Asset_Cash", align="right")
r += 1
p.put(r, 1, 1, "CDs/Stocks/Bonds:", align="right"); p.val(r, 2, 2, "Asset_CDs", align="right")
p.put(r, 4, 4, "401K/403B/Retirement:", align="right"); p.val(r, 5, 5, "Asset_Retirement", align="right")
r += 2
p.put(r, 1, 1, "Explanation/Notes:", valign="top")
p.put(r + 1, 1, 6, f"={V('IE_Notes')}", wrap=True, valign="top", color="1F3B57"); p.h(r + 1, 44)
r += 3
p.put(r, 1, 1, "Total Gross Income:", align="right"); p.val(r, 2, 2, "Inc_Total", align="right")
p.put(r, 4, 4, "# HH Members:", align="right"); p.val(r, 5, 5, "HH_Size", align="center")
r += 1
p.put(r, 4, 4, "FPL %:", align="right", bold=True); p.val(r, 5, 5, "FPL_Percent", align="center", bold=True)
r += 2
p.put(r, 1, 1, f'="Documents Attached for 30 days Income "&{CB("Docs_30Days")}')
p.put(r, 4, 4, f'="FPL Less Than 200% "&{CB("FPL_Under200")}')
p.put(r, 5, 5, "Staff Initials:", align="right"); p.val(r, 6, 6, "Staff_Initials")
r += 2
p.put(r, 1, 1, "Client Signature:", fill=YELLOW); p.put(r, 2, 4, None, line=True)
p.put(r, 5, 5, "Date:", fill=YELLOW, align="right"); p.val(r, 6, 6, "App_Date"); p.h(r, 24)
finish(p, r, 6, 17)

# ================================================================ FUEL ORDER (portrait) - matches the original layout
def outline(ws, r1, c1, r2, c2, style="medium"):
    """Draw a box around r1..r2 x c1..c2, keeping any underline borders already on the cells."""
    s = Side(style=style, color="000000")
    for r in range(r1, r2 + 1):
        for c in range(c1, c2 + 1):
            cell = ws.cell(row=r, column=c)
            b = cell.border
            cell.border = Border(left=s if c == c1 else b.left, right=s if c == c2 else b.right,
                                 top=s if r == r1 else b.top, bottom=s if r == r2 else b.bottom)


def bottom_align(ws, skip=()):
    """Sit text on its underline: bottom-align every cell except the ones in skip."""
    from copy import copy
    for row in ws.iter_rows():
        for c in row:
            if c.coordinate in skip or c.value is None:
                continue
            al = copy(c.alignment); al.vertical = "bottom"; c.alignment = al

def nodollar(name):
    return f'=SUBSTITUTE({V(name)},"$","")'

p = PrintTab("Form - Fuel Order", [2] + [9] * 12 + [2])
ws = p.ws
logo = XLImage("/home/claude/fa/img/logo-000.png")
logo.width, logo.height = 150, 88
ws.add_image(logo, "B1")
p.put(2, 5, 10, "NEKCA Fuel Order", bold=True, size=12, align="center")
for r in (1, 2, 3, 4): p.h(r, 18)
r = 6
p.put(r, 2, 3, f'="Authorized  "&{CB("FO_Authorized")}', bold=True)
p.put(r, 4, 4, "Date:", align="right"); p.val(r, 5, 6, "FO_Date")
p.put(r, 7, 7, "Time:", align="right"); p.val(r, 8, 8, "FO_Time")
p.put(r, 9, 9, "Staff:", align="right"); p.val(r, 10, 12, "Staff_Name")
p.put(r, 13, 13, "3108(d)", align="right")
p.put(8, 2, 6, "PLEASE SEND INVOICE TO")
r = 10
p.put(r, 2, 2, "Email:"); p.val(r, 3, 7, "Invoice_Email")
p.put(r, 8, 8, "Phone:", align="right"); p.val(r, 9, 13, "Invoice_Phone")
outline(ws, 9, 1, 11, 14)
r = 13
p.put(r, 2, 2, "Vendor:"); p.val(r, 3, 7, "Vendor_Name")
p.put(r, 8, 9, "Contact email/#:", align="right"); p.val(r, 10, 13, "Vendor_Contact", wrap=True, size=8)
p.val(14, 10, 13, "Vendor_Contact2", size=8)
r = 16
p.put(r, 2, 3, "SFB remaining:"); p.put(r, 4, 4, nodollar("SFB_Remaining"), line=True, color="1F3B57", align="right")
p.put(r, 5, 6, "Size of tank:", align="right"); p.val(r, 7, 7, "Tank_Size")
p.put(r, 8, 9, "Fuel remaining:", align="right"); p.val(r, 10, 10, "Fuel_Remaining")
p.put(r, 11, 12, "Time fuel will last:", align="right"); p.val(r, 13, 13, "Fuel_Lasts")
# ---- main box 18..28
r = 19
p.put(r, 2, 3, "Client Name:"); p.val(r, 4, 7, "Client_FullName")
p.put(r, 8, 9, "Service Address:", align="right"); p.val(r, 10, 13, "Service_Address_1")
r = 20
p.put(r, 2, 3, "Account #:"); p.val(r, 4, 6, "Vendor_Acct"); p.val(r, 10, 13, "Service_Address_2")
r = 21
p.put(r, 2, 3, "Fuel Type:"); p.val(r, 4, 6, "Fuel_Type"); p.put(r, 10, 13, None, line=True)

def money_row(r, label, name, bold=False):
    p.put(r, 2, 4, label + " $", align="right", bold=bold)
    p.put(r, 5, 6, nodollar(name), line=True, color="1F3B57", align="right", bold=bold)

def amounts_block(r, pre, mid_label, mid_name):
    money_row(r, "Fuel:", f"{pre}_Fuel")
    p.put(r, 8, 10, "Quantity (gallons):", align="right"); p.val(r, 11, 13, f"{pre}_Gallons")
    money_row(r + 1, "Special Trip:", f"{pre}_SpecialTrip")
    money_row(r + 2, "Pressure Test:", f"{pre}_PressureTest")
    p.put(r + 2, 8, 10, mid_label, align="right"); p.val(r + 2, 11, 13, mid_name)
    money_row(r + 3, "Start Up:", f"{pre}_StartUp")
    p.put(r + 3, 8, 10, "Grant/Funding Source:", align="right"); p.val(r + 3, 11, 13, f"{pre}_Funding")
    money_row(r + 4, "Total:", f"{pre}_Total", bold=True)

amounts_block(23, "FO", "Est. Delivery Date:", "FO_EstDelivery")
outline(ws, 18, 1, 28, 14)
# ---- after invoice box 31..37
p.put(30, 1, 6, "AFTER INVOICE RECEIVED")
p.put(30, 10, 14, f'="Invoice received  "&{CB("INV_Received")}', align="right")
amounts_block(32, "INV", "Delivery Date:", "INV_DeliveryDate")
outline(ws, 31, 1, 37, 14)
for rr in range(1, 38):
    if ws.row_dimensions[rr].height is None:
        ws.row_dimensions[rr].height = 29
for rr in (5, 7, 12, 15, 17, 22, 29): p.h(rr, 12)
for rr in (9, 11, 18, 28, 31, 37): p.h(rr, 7)
p.h(10, 30)
for rr in (1, 2, 3, 4): p.h(rr, 18)
finish(p, 37, 14, 29)
bottom_align(ws)

# ================================================================ ELECTRIC PLEDGE (portrait) - matches the original layout
p = PrintTab("Form - Electric Pledge", [2] + [9] * 12 + [2])
ws = p.ws
logo = XLImage("/home/claude/fa/img/logo-000.png")
logo.width, logo.height = 150, 88
ws.add_image(logo, "B1")
p.put(2, 5, 10, "NEKCA Electric Pledge", bold=True, size=12, align="center")
r = 6
p.put(r, 2, 3, f'="Authorized  "&{CB("EP_Authorized")}', bold=True)
p.put(r, 4, 4, "Date:", align="right"); p.val(r, 5, 6, "EP_Date")
p.put(r, 7, 7, "Time:", align="right"); p.val(r, 8, 8, "EP_Time")
p.put(r, 9, 9, "Staff:", align="right"); p.val(r, 10, 12, "Staff_Name")
p.put(r, 13, 13, "3108(d)", align="right")
r = 8
p.put(r, 2, 2, "Vendor:"); p.val(r, 3, 7, "Electric_Provider")
p.put(r, 8, 9, "Contact email/#:", align="right"); p.val(r, 10, 13, "EP_Contact", wrap=True, size=8)
p.val(9, 10, 13, "EP_Contact2", size=8)
# ---- box 1: rows 11..22
r = 12
p.put(r, 2, 3, "Client Name:"); p.val(r, 4, 7, "Client_FullName")
p.put(r, 8, 9, "Service Address:", align="right"); p.val(r, 10, 13, "Service_Address_1")
r = 13
p.put(r, 2, 3, "Account #:"); p.val(r, 4, 6, "Electric_Acct"); p.val(r, 9, 13, "Service_Address_2")
r = 14
p.put(r, 2, 6, f'="Disconnect Notice Attached  "&{CB("EP_Disconnect_Notice")}', bold=True); p.put(r, 9, 13, None, line=True)
def ep_money(r, label, name, bold=False):
    p.put(r, 2, 4, label + " $", align="right", bold=bold)
    p.put(r, 5, 6, nodollar(name), line=True, color="1F3B57", align="right", bold=bold)
r = 16
ep_money(r, "Amount Required:", "EP_Amount")
p.put(r, 8, 13, f'="Discount/Forgiveness program:     Yes "&{CB("EP_Discount_Yes")}&"     No "&{CB("EP_Discount_No")}', align="right")
r = 17
ep_money(r, "Reconnection Fee:", "EP_Reconnect")
p.put(r, 8, 10, "Disconnect Date:", align="right"); p.val(r, 11, 13, "EP_Disconnect_Date")
r = 18
ep_money(r, "Total:", "EP_Total", bold=True)
p.put(r, 8, 10, "Grant/Funding Source:", align="right"); p.val(r, 11, 13, "EP_Funding")
outline(ws, 11, 1, 19, 14)
# ---- box 2: rows 21..33
r = 22
ep_money(r, "Remaining Balance:", "EP_Remaining")
p.put(r, 8, 10, "Next Bill Due by:", align="right"); p.val(r, 11, 13, "EP_NextBill_Due")
r = 23
ep_money(r, "Next Bill:", "EP_NextBill")
r = 25
p.put(r, 2, 8, "Responsibilities of the client (notes):")
ws.merge_cells(start_row=26, start_column=2, end_row=33, end_column=13)
c_ = ws.cell(row=26, column=2); c_.value = f"={V('EP_Responsibilities')}"
c_.font = Font(name=FONT, size=10, color="1F3B57"); c_.alignment = Alignment(wrap_text=True, vertical="top")
outline(ws, 21, 1, 34, 14)
for rr in range(1, 35):
    if ws.row_dimensions[rr].height is None:
        ws.row_dimensions[rr].height = 29
for rr in (1, 2, 3, 4): p.h(rr, 18)
for rr in (5, 7, 10, 15, 20, 24): p.h(rr, 12)
for rr in (11, 19, 21, 34): p.h(rr, 8)
finish(p, 34, 14, 29)
bottom_align(ws, skip=("B26",))

# ================================================================ helpers for raw input cells
def RAW(name): return INPUT_REF[name]
def TXT(ref): return f'IF({ref}="","",{ref}&"")'
def DT(ref): return f'IF({ref}="","",TEXT({ref},"mm/dd/yyyy"))'
def MON(ref): return f'IF({ref}="","",TEXT({ref},"$#,##0.00"))'
def BX(cond): return f'IF({cond},"☒","☐")'
GREYLBL = "F2F2F2"

def sect(p, r, text, c1, c2):
    p.put(r, c1, c2, text, bold=True, size=9, color="FFFFFF", fill=NAVY)

def lbl(p, r, c1, c2, text):
    p.put(r, c1, c2, text, bold=True, size=8, fill=GREYLBL, border=True)

def fld(p, r, c1, c2, formula, **kw):
    kw.setdefault("color", "1F3B57"); kw.setdefault("size", 8)
    p.put(r, c1, c2, "=" + formula, border=True, **kw)

# ================================================================ COVER & STATUS SHEET (portrait)
p = PrintTab("Form - Cover Sheet", [11] * 12)
ws = p.ws
p.put(1, 1, 12, "NEKCA Crisis Fuel / Electric Assistance — Packet Cover & Status Sheet", bold=True, size=13, color=NAVY)
p.put(2, 1, 12, "Staple to the front of the packet. Update the Status block each time the file is touched; the last checked stage is the current status.",
      italic=True, size=8, color="595959")
r = 3; sect(p, r, "CASE INFORMATION", 1, 12)
ft = RAW("Fuel_Type"); ct = RAW("Crisis_Type_Input"); sfa = RAW("SFA_Status_Input")
rows = [
    ("Client name", TXT(RAW("Client_First")) + '&" "&' + TXT(RAW("Client_Last")), "Case / file #", TXT(RAW("Case_Number"))),
    ("First contact", DT(RAW("App_Date")), "Intake staff", TXT(RAW("Staff_Name"))),
    ("Request type", f'{BX(ct+"=\"Low on Fuel\"")}&" Fuel   "&{BX(ct+"=\"Disconnect Notice\"")}&" Electric   ☐ Both"', "Assigned worker", '""'),
    ("Fuel type", f'{BX(ft+"=\"Oil\"")}&" Oil  "&{BX(ft+"=\"Kerosene\"")}&" Kero  "&{BX(ft+"=\"Propane\"")}&" Propane  "&{BX("OR("+ft+"=\"Wood\","+ft+"=\"Pellet\")")}&" Wood/Pellet  "&{BX("OR("+ft+"=\"Electric\","+ft+"=\"Other\")")}&" Other"',
     "Seasonal Fuel", f'{BX(sfa+"=\"Active\"")}&" Active  "&{BX(sfa+"=\"Pending\"")}&" Pending  "&{BX(sfa+"=\"Closed/Denied\"")}&" Closed/Denied"'),
    ("Fuel remaining", f'{V("Current_Quantity")}&IF({V("Fuel_Lasts")}="","","   Lasts: "&{V("Fuel_Lasts")})', "Disconnect date", V("EP_Disconnect_Date")),
    ("HH size", f'{V("HH_Size")}&"     FPL %: "&{V("FPL_Percent")}&"     Under 200%: "&{BX(V("FPL_Under200")+"=\"Yes\"")}&" Yes "&{BX("AND("+V("FPL_Percent")+"<>\"\","+V("FPL_Under200")+"<>\"Yes\")")}&" No"',
     "Gross mo. income", V("Inc_Total")),
]
for lab1, f1, lab2, f2 in rows:
    r += 1
    lbl(p, r, 1, 2, lab1); fld(p, r, 3, 7, f1); lbl(p, r, 8, 9, lab2); fld(p, r, 10, 12, f2)

r += 2; sect(p, r, "STATUS  (check each stage as it is reached)", 1, 12)
stages = ["1 Intake started", "2 Waiting on client", "3 Packet complete", "4 Eligible / authorized", "5 Ordered / pledge sent",
          "6 Delivered / invoice in", "7 Closed", "Denied", "Withdrawn"]
r += 1
for side, c0 in ((0, 1), (1, 7)):
    for col, t in zip(range(c0, c0 + 6), ["", "Stage", "", "", "Date", "Init"]):
        pass
lbl(p, r, 1, 3, "Stage"); lbl(p, r, 4, 5, "Date"); lbl(p, r, 6, 6, "Init")
lbl(p, r, 7, 9, "Stage"); lbl(p, r, 10, 11, "Date"); lbl(p, r, 12, 12, "Init")
for i, stg in enumerate(stages):
    rr = r + 1 + (i % 5); c0 = 1 if i < 5 else 7
    first = (i == 0)
    p.put(rr, c0, c0 + 2, (f'={BX(RAW("App_Date")+"<>\"\"")}&" {stg}"' if first else f"☐ {stg}"), size=8, border=True)
    if first:
        p.put(rr, c0 + 3, c0 + 4, "=" + DT(RAW("App_Date")), size=8, border=True, color="1F3B57")
        p.put(rr, c0 + 5, c0 + 5, f"={V('Staff_Initials')}", size=8, border=True, color="1F3B57")
    else:
        p.put(rr, c0 + 3, c0 + 4, None, border=True); p.put(rr, c0 + 5, c0 + 5, None, border=True)
p.put(r + 5, 7, 9, None, border=True); p.put(r + 5, 10, 11, None, border=True); p.put(r + 5, 12, 12, None, border=True)
r += 6

r += 1; sect(p, r, "PACKET CHECKLIST  (intake #1–2 is done in GoEngage; paper intake is the backup only)", 1, 12)
r += 1
for (c1, c2), t in zip([(1, 1), (2, 4), (5, 8), (9, 9), (10, 10), (11, 11), (12, 12)],
                       ["#", "Form", "Must have", "Client sig", "Staff sig", "Done / N/A", "GoEngage"]):
    lbl(p, r, c1, c2, t)
forms = [
    ("Intake p.1 — GoEngage / paper backup", "Address, phone; row for every household member", "☐", "☐"),
    ("Intake p.2 — GoEngage / paper backup", "Housing & HH type, benefits, occupation (14+), programs", "☐", "☐"),
    ("NEKCA Fuel Assistance Application", "Crisis type, circumstance, SFA status, appeal notice, eligibility", "☐", "☐"),
    ("ACCESS Verification", "SSN & income per member, SFA status, vendor/electric acct #", "—", "☐"),
    ("Income/Expense Worksheet", "Income, 30-day expenses, assets, FPL %, income docs", "☐", "☐"),
    ("NEKCA Consent for Release", "Name, DOB, date, expiration condition", "☐", "☐"),
    ("Community Partners Release", "Name, date; \"do not share\" agencies initialed", "☐", "—"),
    ("ESD Release to 3SquaresVT", "Name, DOB, printed name, date (copy to client)", "☐", "—"),
    ("Crisis Fuel Stmt of Understanding (204NST)", "Name printed, dates (copy to client)", "☐", "☐"),
    ("Right to Appeal notice", "Given to client", "—", "—"),
    ("NEKCA Fuel Order", "Authorized, vendor, SFB left, tank size, fuel left, amounts", "—", "☐"),
    ("NEKCA Electric Pledge", "Authorized, disconnect notice, amounts, client duties", "—", "☐"),
]
for i, (fm, must, cs, ss) in enumerate(forms):
    r += 1
    p.put(r, 1, 1, str(i + 1), size=8, border=True, align="center")
    p.put(r, 2, 4, fm, size=8, border=True)
    p.put(r, 5, 8, must, size=7, border=True)
    for c, t in ((9, cs), (10, ss), (11, "☐   ☐"), (12, "☐")):
        p.put(r, c, c, t, size=8, border=True, align="center")

r += 2; sect(p, r, "ELIGIBILITY", 1, 6); sect(p, r, "ASSISTANCE ISSUED", 7, 12)
elig = ["30 days income docs, all earners", "Income & FPL % calculated", "ESD / Seasonal Fuel status checked",
        "Vendor & account # confirmed", "Disconnect notice on file (electric)", "Applied at ¼ tank or above (repeat)"]
r0 = r + 1
lbl(p, r0, 1, 4, "Step"); lbl(p, r0, 5, 5, "Date"); lbl(p, r0, 6, 6, "Init")
for i, e in enumerate(elig):
    p.put(r0 + 1 + i, 1, 4, f"☐ {e}", size=8, border=True); p.put(r0 + 1 + i, 5, 5, None, border=True); p.put(r0 + 1 + i, 6, 6, None, border=True)
lbl(p, r0, 7, 8, ""); lbl(p, r0, 9, 10, "Fuel order"); lbl(p, r0, 11, 12, "Electric pledge")
assist = [("Vendor", V("Vendor_Name"), V("Electric_Provider")),
          ("Amount authorized", V("FO_Total"), V("EP_Total")),
          ("Gallons / reconnect", V("FO_Gallons"), V("EP_Reconnect")),
          ("Funding source", V("FO_Funding"), V("EP_Funding")),
          ("Authorized date", V("FO_Date"), V("EP_Date")),
          ("Sent to vendor", '""', '""')]
for i, (lab, f1, f2) in enumerate(assist):
    rr = r0 + 1 + i
    lbl(p, rr, 7, 8, lab); fld(p, rr, 9, 10, f1); fld(p, rr, 11, 12, f2)
r = r0 + len(elig)

r += 2; sect(p, r, "DENIAL & APPEAL  (appeal heard same/next business day; fair hearing request within 90 days of written decision)", 1, 12)
r += 1
den = [(1, 3, f'={BX(V("Denied")+"=\"Yes\"")}&" Denied (written decision)"'), (4, 6, "☐ Appeal requested ☐V ☐W"),
       (7, 8, "☐ Appeal heard"), (9, 10, "Decision: ☐ Rev ☐ Upheld"), (11, 12, "☐ Fair hearing req.")]
for c1, c2, t in den:
    p.put(r, c1, c2, t, size=8, border=True, bold=True, fill=GREYLBL)
    p.put(r + 1, c1, c2, "Date:            Init:", size=8, border=True)
r += 1

r += 2; sect(p, r, "AFTER APPOINTMENT & INVOICE", 1, 12)
r += 1
aft = [(1, 3, "☐ Service note added in GoEngage"), (4, 5, "☐ Invoice packet assembled"), (6, 7, "☐ Sent to coordinator"),
       (8, 9, "☐ Uploaded to GoEngage"), (10, 12, f'="☐ Actual fuel cost in GoEngage: "&IF({V("INV_Total")}="","$_______",{V("INV_Total")})')]
for c1, c2, t in aft:
    p.put(r, c1, c2, t, size=8, border=True, bold=True, fill=GREYLBL)
    p.put(r + 1, c1, c2, "Date:            Init:", size=8, border=True)
r += 1

r += 2; sect(p, r, "GOENGAGE UPLOAD — scan and combine into these PDF packets before uploading", 1, 12)
r += 1
lbl(p, r, 1, 3, "PDF packet"); lbl(p, r, 4, 8, "File name"); lbl(p, r, 9, 12, "Uploaded to GoEngage")
ini = f'LEFT({RAW("Client_First")},1)&"_"&{RAW("Client_Last")}'
ad = f'IF({RAW("App_Date")}="","mm_dd_yy",TEXT({RAW("App_Date")},"mm")&"_"&TEXT({RAW("App_Date")},"dd")&"_"&TEXT({RAW("App_Date")},"yy"))'
pk = [("Intake Form Package", f'{ini}&"_signed_intake_"&{ad}'),
      ("Consent Forms Pack", f'{ini}&"_fa_consent_forms_26/27"'),
      ("Application Forms Pack", f'{ini}&"_fuel_assistance_"&{ad}'),
      ("Invoice Packet", f'{ini}&"_invoice_packet_mm_dd_yy"')]
for name, fn in pk:
    r += 1
    p.put(r, 1, 3, name, size=8, border=True, bold=True)
    fld(p, r, 4, 8, f'IF({RAW("Client_Last")}="","",{fn})')
    p.put(r, 9, 12, "☐  Date:            Init:", size=8, border=True)

r += 2; sect(p, r, "FOLLOW-UP LOG", 1, 12)
r += 1
for (c1, c2), t in zip([(1, 2), (3, 7), (8, 9), (10, 10), (11, 11), (12, 12)], ["Date", "Missing item / action", "Contact method", "Due by", "Init", "Resolved"]):
    lbl(p, r, c1, c2, t)
for k in range(2):
    r += 1
    for c1, c2 in [(1, 2), (3, 7), (8, 9), (10, 10), (11, 11)]:
        p.put(r, c1, c2, None, border=True)
    p.put(r, 12, 12, "☐", size=8, border=True, align="center")

r += 2; sect(p, r, "CLOSEOUT", 1, 12)
r += 1
for c1, c2, t in [(1, 3, "☐ All forms complete or N/A"), (4, 6, "☐ All docs uploaded to GoEngage"),
                  (7, 9, "File closed — Date:        Init:"), (10, 12, "Supervisor — Date:        Init:")]:
    p.put(r, c1, c2, t, size=8, border=True)
for rr in range(3, r + 1):
    if ws.row_dimensions[rr].height is None:
        ws.row_dimensions[rr].height = 16.5
p.h(1, 22)
ws.print_area = f"A1:L{r}"
ws.page_margins = PageMargins(left=0.25, right=0.25, top=0.3, bottom=0.3, header=0.1, footer=0.1)

# ================================================================ GOENGAGE DATA SHEET (landscape)
p = PrintTab("Form - GoEngage Data", [11, 10, 9, 5, 13, 11, 13, 6, 13, 14, 10, 14, 15, 10, 13, 10], landscape=True)
ws = p.ws
p.put(1, 1, 16, "GoEngage Data Sheet — values to enter in GoEngage (dropdown wording matches GoEngage)", bold=True, size=13, color=NAVY)
p.put(2, 1, 16, "CONFIDENTIAL — contains SSNs and income. Keep with the client file; shred when no longer needed.", italic=True, size=8, color="C00000")
r = 3; sect(p, r, "CLIENT & ADDRESS", 1, 16)
cl = [("Name", TXT(RAW("Client_First")) + '&" "&' + TXT(RAW("Client_Last")), "DOB", V("Client_DOB"), "Phone", V("Phone"), "Email", V("Email")),
      ("Street", V("Address_Physical"), "City", TXT(RAW("Address_City")), "State", TXT(RAW("Address_State")), "Zip", TXT(RAW("Address_Zip"))),
      ("County", TXT(RAW("County")), "School district", TXT(RAW("School_District")), "Mailing", V("Address_Mailing"), "Case #", TXT(RAW("Case_Number"))),
      ("Language", V("Primary_Language") + '&IF(' + V("Language_Other") + '="",""," / "&' + V("Language_Other") + ')', "Translator", V("Translator_Needed"),
       "Housing type", TXT(RAW("Housing_Type_Input")), "Household type", TXT(RAW("Household_Type_Input")))]
spans = [(1, 1, 2, 4), (5, 5, 6, 8), (9, 9, 10, 12), (13, 13, 14, 16)]
for row in cl:
    r += 1
    for (lc1, lc2, vc1, vc2), (lab, fml) in zip(spans, zip(row[0::2], row[1::2])):
        lbl(p, r, lc1, lc2, lab); fld(p, r, vc1, vc2, fml)
r += 1
bens = [("Benefit_SNAP", "SNAP"), ("Benefit_WIC", "WIC"), ("Benefit_LIHEAP", "LIHEAP"), ("Benefit_HCV", "Housing Choice Voucher"),
        ("Benefit_PublicHousing", "Public Housing"), ("Benefit_PSH", "Perm. Supportive Housing"), ("Benefit_HUDVASH", "HUD-VASH"),
        ("Benefit_Childcare", "Childcare Voucher"), ("Benefit_ACA", "ACA Subsidy")]
bens_f = "&".join(f'IF({V(k)}="Yes","{t}, ","")' for k, t in bens)
lbl(p, r, 1, 1, "Benefits"); fld(p, r, 2, 8, f'{bens_f}&IF({V("SNAP_Amount")}="",""," (SNAP "&{V("SNAP_Amount")}&"/"&{V("SNAP_Period")}&")")')
ints = [("Interest_FoodClothing", "Food/Clothing"), ("Interest_Housing", "Housing"), ("Interest_Fuel", "Fuel"), ("Interest_Economic", "Economic"),
        ("Interest_Youth", "Youth"), ("Interest_Family", "Parent/Family"), ("Interest_TEFAP", "TEFAP"), ("Interest_Justice", "Community Justice"),
        ("Interest_Other", "Other")]
ints_f = "&".join(f'IF({V(k)}="Yes","{t}, ","")' for k, t in ints)
lbl(p, r, 9, 9, "Interests"); fld(p, r, 10, 16, f'{ints_f}&{V("Interest_Other_Text")}')

r += 2; sect(p, r, "HOUSEHOLD MEMBERS", 1, 16)
r += 1
hcols = [("Last name", "Last"), ("First name", "First"), ("Date of birth", "DOB"), ("Age", "Age"), ("Gender identity", "Gender"),
         ("Race", "Race"), ("Hispanic?", "Hisp."), ("Disabled", "Dis."), ("Last grade completed", "Education"),
         ("Health insurance source", "Insurance"), ("Military status", "Military"), ("Occupation (14+)", "Occupation"),
         ("SSN", "SSN"), ("Income per month", "Income/mo"), ("Income source", "Source")]
lbl(p, r, 1, 1, hcols[0][1])
for i, (h, short) in enumerate(hcols[1:], start=2):
    lbl(p, r, i, i, short)
lbl(p, r, 16, 16, "Verified")
for n in range(1, 11):
    r += 1
    for i, (h, short) in enumerate(hcols, start=1):
        ref = hhref(h, n)
        if h == "Date of birth": f_ = DT(ref)
        elif h == "Income per month": f_ = MON(ref)
        elif h == "SSN": f_ = SSN_FMT(ref)
        else: f_ = TXT(ref)
        fld(p, r, i, i, f_, wrap=True, size=7)
    fld(p, r, 16, 16, DT(hhref("Income verified date (ACCESS)", n)), size=7)
    p.h(r, 22)

r += 2; sect(p, r, "INCOME & ELIGIBILITY", 1, 8); sect(p, r, "FUEL / ELECTRIC", 9, 16)
inc_items = [(f"Inc_{k}", lab) for k, lab in INCOMES]
inc_f = "&".join(f'IF({V(k)}="","","{lab.split(" (")[0]}: "&{V(k)}&";  ")' for k, lab in inc_items)
r0 = r
lbl(p, r0 + 1, 1, 2, "Income sources"); fld(p, r0 + 1, 3, 8, inc_f, wrap=True); p.h(r0 + 1, 34)
lbl(p, r0 + 2, 1, 2, "Total gross monthly"); fld(p, r0 + 2, 3, 4, V("Inc_Total")); lbl(p, r0 + 2, 5, 6, "HH size / FPL %"); fld(p, r0 + 2, 7, 8, f'{V("HH_Size")}&"  /  "&{V("FPL_Percent")}')
lbl(p, r0 + 3, 1, 2, "SNAP (ACCESS)"); fld(p, r0 + 3, 3, 6, V("NonInc1_Text")); fld(p, r0 + 3, 7, 8, V("NonInc1_Date"))
lbl(p, r0 + 4, 1, 2, "Crisis / SFA"); fld(p, r0 + 4, 3, 5, TXT(RAW("Crisis_Type_Input"))); fld(p, r0 + 4, 6, 8, f'"SFA: "&{TXT(RAW("SFA_Status_Input"))}&IF({V("SFA_Amount_Granted")}="",""," "&{V("SFA_Amount_Granted")})')
fe_rows = [("Fuel type / vendor", f'{V("Fuel_Type")}&IF({V("Vendor_Name")}="",""," — "&{V("Vendor_Name")})', "Vendor acct #", V("Vendor_Acct")),
           ("Electric provider", V("Electric_Provider"), "Electric acct #", V("Electric_Acct")),
           ("Fuel order total", V("FO_Total"), "Pledge total", V("EP_Total")),
           ("Actual (invoice) total", V("INV_Total"), "Delivery date", V("INV_DeliveryDate"))]
for i, (l1, f1, l2, f2) in enumerate(fe_rows):
    rr = r0 + 1 + i
    lbl(p, rr, 9, 10, l1); fld(p, rr, 11, 12, f1); lbl(p, rr, 13, 14, l2); fld(p, rr, 15, 16, f2)
r = r0 + 5
lbl(p, r, 1, 2, "Circumstance"); fld(p, r, 3, 16, V("Circumstance"), wrap=True); p.h(r, 30)
for rr in range(3, r + 1):
    if ws.row_dimensions[rr].height is None:
        ws.row_dimensions[rr].height = 16
p.h(1, 22)
ws.print_area = f"A1:P{r}"

PRINT_TABS = ["Form - Cover Sheet", "Form - GoEngage Data", "Form - Intake p1", "Form - Intake p2", "Form - Application", "Form - ACCESS", "Form - Income Expense",
              "Form - Fuel Order", "Form - Electric Pledge"]
