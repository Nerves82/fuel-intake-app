<#
  Load-TestData.ps1
  Fills a Fuel Assistance intake workbook with FAKE client data so you can test
  every tab and form. Pick a test set, or "Clear all entries" to empty it again.

  Uses the workbook already open in Excel if there is one; otherwise asks you to pick a file.
  Fields are found by their LABEL on each tab (not cell addresses), so it keeps working
  as the workbook changes - as long as a label isn't renamed. Anything it can't find is
  listed at the end.

  ALL NAMES, NUMBERS AND SSNs BELOW ARE MADE UP. Never run this on a real client's workbook.
#>

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$Today = (Get-Date).Date
$Now   = (Get-Date).TimeOfDay
function D($y, $m, $d) { Get-Date -Year $y -Month $m -Day $d -Hour 0 -Minute 0 -Second 0 -Millisecond 0 }
function T($h, $m) { New-TimeSpan -Hours $h -Minutes $m }

# =====================================================================================
# TEST SETS
#   Fields  : @(tab, label text, value [, which match if the label repeats])
#             Label text matches the whole label, or else any label containing it.
#   Income/Expenses: @('Label', monthly)  /  @('Label', paid30, bills)
#   Household: one hashtable per person, keys = column headings on tab 2.
#              Person 1 is the client - name and date of birth come from tab 1 automatically.
# =====================================================================================
$C = '1 Client'; $A = '3 Application'; $F = '5 Fuel & Electric'; $I = '4 Income & Expenses'

$Common = @(
    @($C, 'Application date', $Today),
    @($C, 'Staff name', 'Test Staffer'),
    @($C, 'Staff phone', '8025550198'),
    @($C, 'Staff email', 'test.staffer@example.org'),
    @($C, 'State', 'VT'),
    @($C, 'Primary language', 'English'),
    @($C, 'Translator needed?', 'N')
)

$Sets = [ordered]@{}

# ---------------------------------------------------------------- 1
$Sets['1 - Single senior, low on oil, Fuel Order'] = @{
    Fields = @(
        @($C, 'Client first name', 'Margaret'),
        @($C, 'Client last name', 'Testerson'),
        @($C, 'Client date of birth', (D 1948 4 12)),
        @($C, 'Physical address', '12 Maple St'),
        @($C, 'City/Town', 'Lyndonville'),
        @($C, 'Zip', '05851'),
        @($C, 'County', 'Caledonia'),
        @($C, 'School district', 'Caledonia Central'),
        @($C, 'Phone number', '8025550101'),
        @($C, 'Housing type', 'Own'),
        @($C, 'Household type', 'Single Person'),
        @($C, 'SNAP', 'Yes'), @($C, 'SNAP amount', 23), @($C, 'SNAP amount per', 'month'),
        @($C, 'SNAP verified in ACCESS (date)', $Today),
        @($C, 'LIHEAP', 'Yes'),
        @($C, 'Fuel Assistance', 'Yes'),
        @($A, 'Crisis type', 'Low on Fuel'),
        @($A, 'Current quantity of fuel', '1/8 tank'),
        @($A, 'Unforeseen / extenuating circumstance', 'Unexpected furnace repair used the money set aside for fuel.'),
        @($A, 'Seasonal Fuel status', 'Active'),
        @($A, 'Appeal notice provided', 'Yes'),
        @($A, 'Agency on Aging', 'Yes'),
        @($A, 'Warmth eligible', 'Yes', 1), @($A, 'Warmth eligible date', $Today),
        @($A, 'Crisis Fuel eligible', 'Yes', 1), @($A, 'Crisis Fuel eligible date', $Today), @($A, 'Crisis Fuel eligible time', $Now),
        @($A, 'NEKCA Intake forms complete', 'Yes'), @($A, 'ACCESS Verification complete', 'Yes'), @($A, 'Consent forms complete', 'Yes'),
        @($A, 'Circumstance included in Service Note', 'Yes'),
        @($I, 'Any income change in the next month?', 'No'),
        @($I, 'Checking', 120), @($I, 'Savings', 300),
        @($I, 'Explanation / notes', 'Fixed income; heating bill doubled after repair.'),
        @($I, 'Documents attached for 30 days income', 'Yes'),
        @($F, 'Seasonal Fuel amount granted', 900), @($F, 'Seasonal Fuel date granted', (D 2026 10 1)),
        @($F, 'Fuel type', 'Oil'),
        @($F, 'Fuel vendor', 'Test Fuel Co.'), @($F, 'Vendor contact email(s) (only if different)', 'orders@testfuel.example'), @($F, 'Vendor account #', 'TF-1001'),
        @($F, 'Electric provider', 'Example Electric'), @($F, 'Electric account #', '00000-1'),
        @($F, 'Authorized', 'Yes', 1), @($F, 'Date', $Today, 1), @($F, 'Time', $Now, 1),
        @($F, 'SFB remaining', 0), @($F, 'Size of tank', '275 gal'), @($F, 'Fuel remaining', '1/8 tank'), @($F, 'Time fuel will last', '2 days'),
        @($F, 'Fuel $', 400, 1), @($F, 'Quantity (gallons)', '100', 1), @($F, 'Start Up $', 50, 1),
        @($F, 'Est. delivery date', $Today.AddDays(2)), @($F, 'Grant/funding source', 'Crisis Fuel', 1)
    )
    Income   = @( ,@('Social Security Retirement (SSA)', 1420) )
    Expenses = @( @('Electricity', 85, 90), @('Groceries', 250, $null), @('Home Heating', 0, 450),
                  @('Mortgage/Rent', 600, $null), @('Phone/WIFI', 60, $null), @('Medical Insurance/Expenses', 45, $null) )
    Household = @(
        @{ 'Gender identity'='Female'; 'Disabled'='N'; 'Last grade completed'='High school graduate (high school diploma or equivalent)';
           'Race'='White'; 'Hispanic?'='No'; 'Health insurance source'='Medicare'; 'Income per month'=1420;
           'Income source'='Social Security Retirement (SSA)'; 'Military status'='Veteran'; 'SSN'=900120001;
           'Occupation (14+)'='Retired'; 'Income verified date (ACCESS)'=$Today }
    )
}

# ---------------------------------------------------------------- 2
$Sets['2 - Family of 5, electric disconnect, Electric Pledge'] = @{
    Fields = @(
        @($C, 'Client first name', 'Daniel'),
        @($C, 'Client last name', 'Sampleton'),
        @($C, 'Client date of birth', (D 1988 7 22)),
        @($C, 'Physical address', '45 River Rd'),
        @($C, 'City/Town', 'Newport'), @($C, 'Zip', '05855'),
        @($C, 'County', 'Orleans'), @($C, 'School district', 'North Country Supervisory Union'),
        @($C, 'Mailing address (if not same)', 'PO Box 12, Newport, VT 05855'),
        @($C, 'Phone number', '8025550102'), @($C, 'Email (optional)', 'dan.sample@example.com'),
        @($C, 'Housing type', 'Rent'), @($C, 'Household type', 'Two Parent Household'),
        @($C, 'SNAP', 'Yes'), @($C, 'SNAP amount', 612), @($C, 'SNAP amount per', 'month'),
        @($C, 'SNAP verified in ACCESS (date)', $Today),
        @($C, 'WIC', 'Yes'), @($C, 'Childcare Voucher', 'Yes'),
        @($C, 'Fuel Assistance', 'Yes'), @($C, 'Youth Programs', 'Yes'), @($C, 'Parent/Family Supports', 'Yes'),
        @($A, 'Crisis type', 'Disconnect Notice'),
        @($A, 'Electric required for heating system', 'Yes'),
        @($A, 'Which heating component does the electric run?', 'Pellet stove igniter and auger'),
        @($A, 'Landlord Verification form', 'Yes'),
        @($A, 'Unforeseen / extenuating circumstance', 'Hours cut at work for 6 weeks; fell behind on the electric bill.'),
        @($A, 'Seasonal Fuel status', 'Pending'),
        @($A, 'Appeal notice provided', 'Yes'),
        @($A, 'Weatherization/NETO', 'Yes'), @($A, 'Financial Coaching', 'Yes'), @($A, 'Whole Family Approach', 'Yes'),
        @($A, 'VLITE eligible', 'Yes', 1), @($A, 'VLITE eligible date', $Today),
        @($A, 'Crisis Fuel eligible', 'Yes', 1), @($A, 'Crisis Fuel eligible date', $Today), @($A, 'Crisis Fuel eligible time', (T 10 15)),
        @($A, 'NEKCA Intake forms complete', 'Yes'), @($A, 'ACCESS Verification complete', 'Yes'), @($A, 'Consent forms complete', 'Yes'),
        @($I, 'Any income change in the next month?', 'Yes'),
        @($I, 'If yes, explain:', 'Hours go back to full-time in January.'),
        @($I, 'Checking', 45), @($I, 'Cash', 20),
        @($I, 'Documents attached for 30 days income', 'Yes'),
        @($F, 'Fuel type', 'Pellet'),
        @($F, 'Electric provider', 'Example Electric Coop'), @($F, 'Electric account #', '12345-02'),
        @($F, 'Authorized', 'Yes', 2), @($F, 'Date', $Today, 2), @($F, 'Time', (T 10 30), 2),
        @($F, 'Electric contact email(s) (only if different)', 'billing@electric.example'),
        @($F, 'Disconnect notice attached', 'Yes'),
        @($F, 'Amount required $', 640), @($F, 'Reconnection fee $', 35),
        @($F, 'Discount/Forgiveness program', 'No'),
        @($F, 'Disconnect date', $Today.AddDays(5)),
        @($F, 'Grant/funding source', 'Crisis Electric', 3),
        @($F, 'Remaining balance $', 0), @($F, 'Next bill $', 180), @($F, 'Next bill due by', $Today.AddDays(30)),
        @($F, 'Responsibilities of the client (notes)', 'Client will apply for the utility discount program and set up a payment plan for future bills.')
    )
    Income   = @( ,@('Earned Income (employment/self-employment)', 3000) )
    Expenses = @( @('Mortgage/Rent', 1250, $null), @('Electricity', 210, 640), @('Groceries', 700, $null),
                  @('Childcare/Adult Care', 300, $null), @('Auto Loan', 320, $null), @('Auto Insurance', 140, $null),
                  @('Phone/WIFI', 90, $null) )
    Household = @(
        @{ 'Gender identity'='Male'; 'Disabled'='N'; 'Last grade completed'='High School plus some college'; 'Race'='White';
           'Hispanic?'='No'; 'Health insurance source'='Employment Based'; 'Income per month'=2100; 'Income source'='Employment';
           'Military status'='Never'; 'SSN'=900120011; 'Occupation (14+)'='Employed Full-time'; 'Income verified date (ACCESS)'=$Today },
        @{ 'Last name'='Sampleton'; 'First name'='Rosa'; 'Date of birth'=(D 1990 2 3); 'Gender identity'='Female'; 'Disabled'='N';
           'Last grade completed'="Associate's degree"; 'Race'='Two or more races'; 'Hispanic?'='Yes';
           'Health insurance source'='Employment Based'; 'Income per month'=900; 'Income source'='Employment';
           'Military status'='Never'; 'SSN'=900120012; 'Occupation (14+)'='Employed Part-time'; 'Income verified date (ACCESS)'=$Today },
        @{ 'Last name'='Sampleton'; 'First name'='Eli'; 'Date of birth'=(D 2010 9 15); 'Gender identity'='Male'; 'Disabled'='N';
           'Last grade completed'='9th grade'; 'Race'='Two or more races'; 'Hispanic?'='Yes';
           'Health insurance source'="State Children's Health Insurance Program"; 'Income per month'=0; 'Income source'='None';
           'Military status'='Never'; 'SSN'=900120013; 'Occupation (14+)'='School Full Time' },
        @{ 'Last name'='Sampleton'; 'First name'='Mia'; 'Date of birth'=(D 2016 5 1); 'Gender identity'='Female'; 'Disabled'='N';
           'Last grade completed'='5th - 8th grade'; 'Race'='Two or more races'; 'Hispanic?'='Yes';
           'Health insurance source'="State Children's Health Insurance Program"; 'Military status'='Never'; 'SSN'=900120014;
           'Occupation (14+)'='N/A' },
        @{ 'Last name'='Sampleton'; 'First name'='Leo'; 'Date of birth'=(D 2022 11 20); 'Gender identity'='Male'; 'Disabled'='N';
           'Last grade completed'='No school completed'; 'Race'='Two or more races'; 'Hispanic?'='Yes';
           'Health insurance source'='Medicaid'; 'Military status'='Never'; 'SSN'=900120015; 'Occupation (14+)'='N/A' }
    )
}

# ---------------------------------------------------------------- 3
$Sets['3 - Over income, denied, phone application'] = @{
    Fields = @(
        @($C, 'Staff initials (only if different)', 'TQ'),
        @($C, 'Client first name', 'Alex'),
        @($C, 'Client last name', "O'Testa-Smith"),
        @($C, 'Client date of birth', (D 1975 11 30)),
        @($C, 'Physical address', '7 Hill St Apt 2'),
        @($C, 'City/Town', 'St. Johnsbury'), @($C, 'Zip', '05819'),
        @($C, 'County', 'Caledonia'), @($C, 'School district', 'St. Johnsbury'),
        @($C, 'Phone number', '8025550103'),
        @($C, 'Housing type', 'Temporary Housing'), @($C, 'Household type', 'Two Adults NO Children'),
        @($A, 'Second crisis assist', 'Yes'),
        @($A, 'Crisis type', 'Low on Fuel'),
        @($A, 'Current quantity of fuel', '10%'),
        @($A, 'or Propane', 'Yes'),
        @($A, 'Unforeseen / extenuating circumstance', 'Car repair.'),
        @($A, 'Seasonal Fuel status', 'Closed/Denied'),
        @($A, 'Appeal notice provided', 'Yes'),
        @($A, 'Phone application', 'Yes', 1),
        @($A, 'Phone application reason', 'Client snowed in, no transportation'),
        @($A, 'Denied', 'Yes'),
        @($A, 'Over 200% FPL', 'Yes'),
        @($A, 'Financial Coaching', 'Yes'), @($A, 'VITA', 'Yes'),
        @($I, 'Any income change in the next month?', 'No'),
        @($I, 'Savings', 2500), @($I, '401K/403B/Retirement', 18000),
        @($F, 'Fuel type', 'Propane'),
        @($F, 'Fuel vendor', 'Example Propane'), @($F, 'Vendor account #', 'EP-7788')
    )
    Income   = @( @('Earned Income (employment/self-employment)', 4800), @('Social Security Disability (SSDI)', 1500) )
    Expenses = @( @('Mortgage/Rent', 1400, $null), @('Groceries', 600, $null), @('Credit Card', 250, 3200) )
    Household = @(
        @{ 'Gender identity'='Non-binary'; 'Disabled'='N'; 'Last grade completed'="Bachelor's degree"; 'Race'='Asian';
           'Hispanic?'='No'; 'Health insurance source'='Direct-Purchase'; 'Income per month'=4800; 'Income source'='Employment';
           'Military status'='Never'; 'SSN'=900120021; 'Occupation (14+)'='Employed Full-time' },
        @{ 'Last name'='Partner'; 'First name'='Jordan'; 'Date of birth'=(D 1978 6 9); 'Gender identity'='Other'; 'Disabled'='Y';
           'Last grade completed'='Some college (but no degree)'; 'Race'='Middle Eastern or North African'; 'Hispanic?'='N/A';
           'Health insurance source'='Medicare'; 'Income per month'=1500; 'Income source'='Social Security Disability (SSDI)';
           'Military status'='Veteran'; 'SSN'=900120022; 'Occupation (14+)'='Disabled' }
    )
}

# ---------------------------------------------------------------- 4
$members4 = @(
    @{ 'Gender identity'='Female'; 'Disabled'='N'; 'Last grade completed'='12th grade (no diploma)'; 'Race'='White'; 'Hispanic?'='Yes';
       'Health insurance source'='Medicaid'; 'Income per month'=600; 'Income source'='Self-employment'; 'Military status'='Never';
       'SSN'=900120031; 'Occupation (14+)'='Seasonal - Agricultural'; 'Income verified date (ACCESS)'=$Today }
)
$people = @(
    @('Elder', 'Rosa', (D 1940 1 5), 'Female', 'Y', 'Less than or equal to 4th grade', 'Medicare', 980, 'Social Security Retirement (SSA)', 'Retired'),
    @('Elder', 'Tomas', (D 1942 3 9), 'Male', 'Y', 'No school completed', 'Medicare', 760, 'Supplemental Security Income (SSI)', 'Retired'),
    @('Longname-Testcase', 'Marco', (D 1984 8 1), 'Male', 'N', '10th grade', 'None', 1100, 'Employment', 'Seasonal - Agricultural'),
    @('Longname-Testcase', 'Ana', (D 2008 4 4), 'Female', 'N', '11th grade', "State Children's Health Insurance Program", 0, 'None', 'In school full-time and employed part-time'),
    @('Longname-Testcase', 'Luis', (D 2011 6 6), 'Male', 'N', '9th grade', "State Children's Health Insurance Program", 0, 'None', 'School Full Time'),
    @('Longname-Testcase', 'Sofia', (D 2014 2 2), 'Female', 'N', '5th - 8th grade', 'Medicaid', 0, 'None', 'N/A'),
    @('Longname-Testcase', 'Diego', (D 2017 9 9), 'Male', 'Y', 'Less than or equal to 4th grade', 'Medicaid', 0, 'None', 'N/A'),
    @('Longname-Testcase', 'Lucia', (D 2020 12 12), 'Female', 'N', 'No school completed', 'Medicaid', 0, 'None', 'N/A'),
    @('Cousin', 'Pat', (D 1999 7 7), 'Non-binary', 'N', 'Some college (but no degree)', 'State Health Insurance for Adults', 400, 'Unemployment', 'Unemployed (6 Months or More)')
)
$n = 32
foreach ($p in $people) {
    $members4 += @{ 'Last name'=$p[0]; 'First name'=$p[1]; 'Date of birth'=$p[2]; 'Gender identity'=$p[3]; 'Disabled'=$p[4];
        'Last grade completed'=$p[5]; 'Race'='Hispanic or Latino'; 'Hispanic?'='Yes'; 'Health insurance source'=$p[6];
        'Income per month'=$p[7]; 'Income source'=$p[8]; 'Military status'='Never'; 'SSN'=(900120000 + $n);
        'Occupation (14+)'=$p[9]; 'Income verified date (ACCESS)'=$Today }
    $n++
}
$Sets['4 - Household of 10, every field, long text'] = @{
    Fields = @(
        @($C, 'Client first name', 'Elizabeth'),
        @($C, 'Client last name', 'Longname-Testcase'),
        @($C, 'Client date of birth', (D 1986 10 10)),
        @($C, 'Legal representative name (if any)', 'Pat Guardian'),
        @($C, 'Physical address', '1234 Very Long Road Name Extension, Lot 56'),
        @($C, 'City/Town', 'Island Pond'), @($C, 'Zip', '05846'),
        @($C, 'County', 'Essex'), @($C, 'School district', 'NEK Choice'),
        @($C, 'Mailing address (if not same)', 'c/o Example Farm, 99 Farm Ln, Brighton, VT 05846'),
        @($C, 'Phone number', '8025550104'), @($C, 'Email (optional)', 'elizabeth.longname-testcase@example.com'),
        @($C, 'Primary language', 'Spanish'), @($C, 'Other language (specify)', 'Portuguese'),
        @($C, 'English fluency (if applicable)', 'Limited'), @($C, 'Translator needed?', 'Y'),
        @($C, 'Service address line 1', 'Farm Worker Housing Unit B'), @($C, 'Service address line 2', '99 Farm Ln, Brighton, VT 05846'),
        @($C, 'Housing type', 'Migrant Housing'), @($C, 'Household type', 'Multigenerational Household'),
        @($C, 'SNAP', 'Yes'), @($C, 'SNAP amount', 1350.5), @($C, 'SNAP amount per', 'month'), @($C, 'SNAP verified in ACCESS (date)', $Today),
        @($C, 'WIC', 'Yes'), @($C, 'LIHEAP', 'Yes'), @($C, 'Housing Choice Voucher', 'Yes'), @($C, 'Public Housing', 'Yes'),
        @($C, 'Permanent Supportive Housing', 'Yes'), @($C, 'HUD-VASH', 'Yes'), @($C, 'Childcare Voucher', 'Yes'),
        @($C, 'Affordable Care Act Subsidy', 'Yes'),
        @($C, 'Food / Clothing', 'Yes'), @($C, 'Housing Support', 'Yes'), @($C, 'Fuel Assistance', 'Yes'), @($C, 'Economic Programs', 'Yes'),
        @($C, 'Youth Programs', 'Yes'), @($C, 'Parent/Family Supports', 'Yes'), @($C, 'TEFAP/USDA', 'Yes'),
        @($C, 'Community Justice Programs', 'Yes'), @($C, 'Other', 'Yes'), @($C, 'Other (specify)', 'English classes'),
        @($A, 'AAA employee applying on behalf', 'Yes'), @($A, 'Afterhours follow-up', 'Yes'),
        @($A, 'Crisis type', 'Low on Fuel'), @($A, 'Current quantity of fuel', 'Empty - ran out overnight'),
        @($A, 'Electric required for heating system', 'Yes'), @($A, 'Which heating component does the electric run?', 'Kerosene space heater fan'),
        @($A, 'Landlord Verification form', 'No'), @($A, 'Heat included in rent', 'Yes'), @($A, 'AND meets rule exception', 'Yes'),
        @($A, 'Life-threatening crisis', 'Yes'),
        @($A, 'Unforeseen / extenuating circumstance', ('This is a deliberately long circumstance to test wrapping on the printed application. ' +
            'Two elderly household members have medical conditions made worse by cold; seasonal farm work ended early because of an early frost, ' +
            'and the family had unexpected vehicle repairs needed to get to medical appointments.')),
        @($A, 'Seasonal Fuel status', 'Pending'), @($A, 'Appeal notice provided', 'Yes'),
        @($A, 'Weatherization/NETO', 'Yes'), @($A, 'Agency on Aging', 'Yes'), @($A, 'Financial Coaching', 'Yes'),
        @($A, 'Community Connections', 'Yes'), @($A, 'Whole Family Approach', 'Yes'), @($A, 'VITA', 'Yes'),
        @($A, 'Warmth eligible', 'Yes', 1), @($A, 'Warmth eligible date', $Today),
        @($A, 'VLITE eligible', 'Yes', 1), @($A, 'VLITE eligible date', $Today),
        @($A, 'Crisis Fuel eligible', 'Yes', 1), @($A, 'Crisis Fuel eligible date', $Today), @($A, 'Crisis Fuel eligible time', (T 16 45)),
        @($A, 'NEKCA Intake forms complete', 'Yes'), @($A, 'ACCESS Verification complete', 'Yes'), @($A, 'Consent forms complete', 'Yes'),
        @($A, 'Circumstance included in Service Note', 'Yes'), @($A, 'Missing documents exception', 'Yes'),
        @($I, 'Any income change in the next month?', 'Yes'),
        @($I, 'If yes, explain:', 'Seasonal work ends; unemployment claim pending; SSI review scheduled next month.'),
        @($I, 'Checking', 15.75), @($I, 'Savings', 0), @($I, 'Cash', 40), @($I, 'CDs/Stocks/Bonds', 0), @($I, '401K/403B/Retirement', 0),
        @($I, 'Explanation / notes', ('Long note test: household shares one income stream from seasonal farm work plus SSA/SSI for the two elders. ' +
            'Expenses exceed income this month because of medical co-pays, vehicle repair and a short fuel delivery.')),
        @($I, 'Documents attached for 30 days income', 'Yes'),
        @($F, 'Seasonal Fuel amount granted', 1250), @($F, 'Seasonal Fuel date granted', (D 2026 9 15)),
        @($F, 'Fuel type', 'Kerosene'),
        @($F, 'Fuel vendor', 'Example Kerosene & Oil Delivery Service'), @($F, 'Vendor contact email(s) (only if different)', '802-555-0177 / dispatch@kero.example'),
        @($F, 'Vendor account #', 'KX-000001'),
        @($F, 'Electric provider', 'Example Electric'), @($F, 'Electric account #', '55555-04'),
        @($F, 'ACCESS notes', 'ACCESS checked for all adults; SNAP active; elder SSI verified.'),
        @($F, 'Authorized', 'Yes', 1), @($F, 'Date', $Today, 1), @($F, 'Time', (T 16 50), 1),
        @($F, 'SFB remaining', 125.5), @($F, 'Size of tank', '330 gal'), @($F, 'Fuel remaining', 'Empty'), @($F, 'Time fuel will last', '0 days'),
        @($F, 'Fuel $', 650, 1), @($F, 'Quantity (gallons)', '150', 1), @($F, 'Special Trip $', 75, 1), @($F, 'Pressure Test $', 40, 1),
        @($F, 'Start Up $', 60, 1), @($F, 'Est. delivery date', $Today.AddDays(1)), @($F, 'Grant/funding source', 'Crisis Fuel', 1),
        @($F, 'Invoice received', 'Yes'),
        @($F, 'Fuel $', 641.25, 2), @($F, 'Quantity (gallons)', '147.5', 2), @($F, 'Special Trip $', 75, 2), @($F, 'Pressure Test $', 40, 2),
        @($F, 'Start Up $', 60, 2), @($F, 'Delivery date', $Today.AddDays(1)), @($F, 'Grant/funding source', 'Crisis Fuel', 2)
    )
    Income   = @( @('Alimony/Other Spousal Support', 50), @('Child Support', 200), @('Earned Income (employment/self-employment)', 1700),
                  @('Pension/Retirement Income from Job', 0), @('Social Security Retirement (SSA)', 980), @('Social Security Disability (SSDI)', 0),
                  @('Supplemental Social Security (SSI)', 760), @('Unemployment', 400), @('VA Disability', 0), @("Worker's Compensation", 0),
                  @('Other', 25) )
    Expenses = @( @('Auto Insurance', 110, 110), @('Auto Registration/Repair', 900, 1200), @('Auto Loan', 0, $null), @('Childcare/Adult Care', 0, $null),
                  @('Child Support', 0, $null), @('Cigarettes/Vapes', 40, $null), @('CO Payments (Dr/Dentist)', 180, 320), @('Credit Card', 75, 900),
                  @('Electricity', 160, 160), @('Entertainment/Subscriptions', 15, $null), @('Groceries', 900, $null), @('Gas', 220, $null),
                  @('Hygiene/Laundry/Paper', 80, $null), @('Home Heating', 300, 650), @('Home Insurance', 0, $null), @('Home Repairs', 50, $null),
                  @('Medical Insurance/Expenses', 60, $null), @('Loans', 0, $null), @('Mortgage/Rent', 0, $null), @('Plowing/Water/Sewer', 35, $null),
                  @('Phone/WIFI', 95, 95), @('Vet/Pets', 20, $null) )
    Household = $members4
}

# =====================================================================================
# Engine - no edits needed below
# =====================================================================================
$DataTabs = @('1 Client', '2 Household', '3 Application', '4 Income & Expenses', '5 Fuel & Electric')
$script:Missing = New-Object System.Collections.Generic.List[string]
$script:LabelCache = @{}

function Get-Labels($ws, $col) {
    $key = "$($ws.Name)|$col"
    if (-not $script:LabelCache.ContainsKey($key)) {
        $vals = $ws.Range("${col}1:${col}300").Value2
        $list = @()
        for ($r = 1; $r -le 300; $r++) { $list += [string]$vals[$r, 1] }
        $script:LabelCache[$key] = $list
    }
    return $script:LabelCache[$key]
}

function Find-LabelRow($labels, $text, $nth = 1) {
    $t = $text.Trim()
    $exact = @(); $part = @()
    for ($i = 0; $i -lt $labels.Count; $i++) {
        $l = ([string]$labels[$i]).Trim()
        if (-not $l) { continue }
        if ($l -ieq $t) { $exact += ($i + 1) }
        elseif ($l.IndexOf($t, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $part += ($i + 1) }
    }
    $hits = if ($exact.Count -ge $nth) { $exact } else { $part }
    if ($hits.Count -ge $nth) { return $hits[$nth - 1] }
    return $null
}

function Set-Cell($cell, $value) {
    if ($null -eq $value) { [void]$cell.ClearContents(); return }
    # Numbers, dates and times are passed to Excel directly through COM (PowerShell's normal
    # property setter can fail with "Unable to cast System.Double to System.String").
    if ($value -is [datetime])     { $v = [double]$value.ToOADate() }
    elseif ($value -is [timespan]) { $v = [double]$value.TotalDays }
    elseif ($value -is [int] -or $value -is [long] -or $value -is [double] -or $value -is [decimal]) { $v = [double]$value }
    else { $v = [string]$value }
    try {
        [void][System.__ComObject].InvokeMember('Value2', [System.Reflection.BindingFlags]::SetProperty, $null, $cell, @($v))
    } catch {
        # Fallback: type it in as text the way a person would; Excel converts it to a number/date/time.
        $inv = [System.Globalization.CultureInfo]::InvariantCulture
        if ($value -is [datetime])     { $txt = $value.ToString('MM/dd/yyyy', $inv) }
        elseif ($value -is [timespan]) { $txt = (Get-Date).Date.Add($value).ToString('h:mm tt', $inv) }
        else                           { $txt = [string]::Format($inv, '{0}', $value) }
        $cell.Formula = $txt
    }
}

function Get-ByLabel($wb, $tab, $label) {
    $ws = $wb.Worksheets.Item($tab)
    $row = Find-LabelRow (Get-Labels $ws 'B') $label 1
    if (-not $row) { return '' }
    return ([string]$ws.Cells.Item($row, 3).Value2).Trim()
}

function Set-ByLabel($wb, $tab, $label, $value, $nth = 1, $labelCol = 'B', $offset = 1) {
    try {
        $ws = $wb.Worksheets.Item($tab)
        $row = Find-LabelRow (Get-Labels $ws $labelCol) $label $nth
        if (-not $row) { $script:Missing.Add("$tab : '$label'" + $(if ($nth -gt 1) { " (#$nth)" } else { '' })); return }
        $colIdx = $ws.Range("${labelCol}1").Column + $offset
        Set-Cell $ws.Cells.Item($row, $colIdx) $value
    } catch {
        $script:Missing.Add("$tab : '$label' - $($_.Exception.Message)")
    }
}

function Clear-Entries($wb) {
    foreach ($tab in $DataTabs) {
        $ws = $wb.Worksheets.Item($tab)
        foreach ($cell in $ws.UsedRange.Cells) {
            if ($cell.Locked -eq $false -and -not $cell.HasFormula) {
                if ($cell.MergeCells) {
                    if ($cell.Address() -eq $cell.MergeArea.Cells.Item(1, 1).Address()) { $cell.MergeArea.ClearContents() | Out-Null }
                } elseif ($null -ne $cell.Value2) {
                    $cell.ClearContents() | Out-Null
                }
            }
        }
    }
}

function Fill-Household($wb, $people) {
    $ws = $wb.Worksheets.Item('2 Household')
    $hdr = @{}
    for ($c = 1; $c -le 30; $c++) {
        $h = [string]$ws.Cells.Item(5, $c).Value2
        if ($h) { $hdr[$h.Trim()] = $c }
    }
    for ($i = 0; $i -lt $people.Count -and $i -lt 10; $i++) {
        $row = 6 + $i
        foreach ($k in $people[$i].Keys) {
            if ($i -eq 0 -and $k -in @('Last name', 'First name', 'Date of birth')) { continue }   # client row fills from tab 1
            if (-not $hdr.ContainsKey($k)) { $script:Missing.Add("2 Household : column '$k'"); continue }
            try { Set-Cell $ws.Cells.Item($row, $hdr[$k]) $people[$i][$k] }
            catch { $script:Missing.Add("2 Household : row $($i+1) '$k' - $($_.Exception.Message)") }
        }
    }
}

function Fill-Set($wb, $set) {
    foreach ($f in ($Common + $set.Fields)) {
        $nth = if ($f.Count -ge 4) { $f[3] } else { 1 }
        Set-ByLabel $wb $f[0] $f[1] $f[2] $nth
    }
    foreach ($x in $set.Income)   { Set-ByLabel $wb $I $x[0] $x[1] 1 'B' 1 }
    foreach ($x in $set.Expenses) { Set-ByLabel $wb $I $x[0] $x[1] 1 'E' 1; Set-ByLabel $wb $I $x[0] $x[2] 1 'E' 2 }
    Fill-Household $wb $set.Household
}

function Pick-Set {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'Load Test Data'
    $form.Size = New-Object System.Drawing.Size(460, 300)
    $form.StartPosition = 'CenterScreen'; $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false; $form.MinimizeBox = $false; $form.TopMost = $true
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $lbl = New-Object System.Windows.Forms.Label
    $lbl.Text = 'Pick a fake client to load (replaces everything typed in the workbook):'
    $lbl.Location = '12,10'; $lbl.Size = '420,40'
    $lb = New-Object System.Windows.Forms.ListBox
    $lb.Location = '12,52'; $lb.Size = '420,150'
    [void]$lb.Items.Add('Clear all entries (empty workbook)')
    foreach ($k in $Sets.Keys) { [void]$lb.Items.Add($k) }
    $lb.SelectedIndex = 1
    $ok = New-Object System.Windows.Forms.Button
    $ok.Text = 'Load'; $ok.Location = '256,212'; $ok.Width = 85; $ok.DialogResult = 'OK'
    $cancel = New-Object System.Windows.Forms.Button
    $cancel.Text = 'Cancel'; $cancel.Location = '347,212'; $cancel.Width = 85; $cancel.DialogResult = 'Cancel'
    $lb.Add_DoubleClick({ $form.DialogResult = 'OK'; $form.Close() })
    $form.Controls.AddRange(@($lbl, $lb, $ok, $cancel))
    $form.AcceptButton = $ok; $form.CancelButton = $cancel
    if ($form.ShowDialog() -ne 'OK') { return $null }
    return $lb.SelectedItem
}

# ---- find open intake workbooks in ANY running Excel window (not just the first Excel found)
if (-not ('OpenWorkbooks' -as [type])) {
Add-Type -Language CSharp -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public static class OpenWorkbooks {
    [DllImport("ole32.dll")] static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable rot);
    [DllImport("ole32.dll")] static extern int CreateBindCtx(int reserved, out IBindCtx ctx);
    public static List<object> Get() {
        var found = new List<object>();
        IRunningObjectTable rot; IBindCtx ctx;
        if (GetRunningObjectTable(0, out rot) != 0 || CreateBindCtx(0, out ctx) != 0) return found;
        IEnumMoniker e; rot.EnumRunning(out e);
        var m = new IMoniker[1];
        while (e.Next(1, m, IntPtr.Zero) == 0) {
            try {
                string name; m[0].GetDisplayName(ctx, null, out name);
                string lower = name.ToLowerInvariant();
                if (!(lower.EndsWith(".xlsx") || lower.EndsWith(".xlsm"))) continue;
                object o; rot.GetObject(m[0], out o);
                if (o != null) found.Add(o);
            } catch { }
        }
        return found;
    }
}
"@
}

function Find-IntakeWorkbook($purpose) {
    # Returns the open workbook that has a "1 Client" tab. If several are open, asks which one.
    $cands = @()
    try { $all = [OpenWorkbooks]::Get() } catch { $all = @() }
    foreach ($o in $all) {
        try {
            $has = $false
            foreach ($s in $o.Worksheets) { if ($s.Name -eq '1 Client') { $has = $true; break } }
            if ($has) { $cands += $o }
        } catch { }
    }
    if ($cands.Count -eq 0) { return $null }
    if ($cands.Count -eq 1) { return $cands[0] }

    $f = New-Object System.Windows.Forms.Form
    $f.Text = 'Which workbook?'; $f.StartPosition = 'CenterScreen'; $f.FormBorderStyle = 'FixedDialog'
    $f.MaximizeBox = $false; $f.MinimizeBox = $false; $f.TopMost = $true
    $f.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $f.ClientSize = New-Object System.Drawing.Size(460, 250)
    $l = New-Object System.Windows.Forms.Label
    $l.Text = "Several intake workbooks are open. Pick the one to $purpose`:"; $l.Location = '12,10'; $l.Size = '436,40'
    $lb = New-Object System.Windows.Forms.ListBox
    $lb.Location = '12,52'; $lb.Size = '436,150'
    foreach ($c in $cands) { [void]$lb.Items.Add($c.Name) }
    $lb.SelectedIndex = 0
    for ($i = 0; $i -lt $cands.Count; $i++) {
        try { if ($cands[$i].Application.ActiveWorkbook.FullName -eq $cands[$i].FullName -and $cands[$i].Application.Visible) { $lb.SelectedIndex = $i } } catch { }
    }
    $ok = New-Object System.Windows.Forms.Button; $ok.Text = 'OK'; $ok.Location = '262,212'; $ok.Width = 90; $ok.DialogResult = 'OK'
    $cn = New-Object System.Windows.Forms.Button; $cn.Text = 'Cancel'; $cn.Location = '358,212'; $cn.Width = 90; $cn.DialogResult = 'Cancel'
    $lb.Add_DoubleClick({ $f.DialogResult = 'OK'; $f.Close() })
    $f.Controls.AddRange(@($l, $lb, $ok, $cn)); $f.AcceptButton = $ok; $f.CancelButton = $cn
    if ($f.ShowDialog() -ne 'OK') { exit 0 }
    return $cands[$lb.SelectedIndex]
}

# ---- find the workbook
$xl = $null
$wb = Find-IntakeWorkbook 'load test data into'
if ($wb) {
    $xl = $wb.Application
} else {
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Title = 'No intake workbook is open - pick a COPY to fill with test data'
    $dlg.Filter = 'Excel workbook (*.xlsx)|*.xlsx'
    $dlg.InitialDirectory = [Environment]::GetFolderPath('MyDocuments')
    if ($dlg.ShowDialog() -ne 'OK') { exit 0 }
    $xl = New-Object -ComObject Excel.Application
    $xl.Visible = $true
    $wb = $xl.Workbooks.Open($dlg.FileName)
}

$choice = Pick-Set
if (-not $choice) { exit 0 }

$ans = [System.Windows.Forms.MessageBox]::Show("This replaces everything typed in:`n$($wb.Name)`n(the client name and staff details already there are kept)`n`nOnly use a test copy - never a real client's workbook. Continue?",
    'Load Test Data', 'YesNo', 'Warning')
if ($ans -ne 'Yes') { exit 0 }

# Keep the name and staff that New Client Folder put in, so the test workbook still matches
# its client folder - only everything else becomes fake.
$KeepLabels = @('Client first name', 'Client last name', 'Staff name', 'Staff initials (only if different)', 'Staff phone (only if different)', 'Staff email (only if different)')
$kept = [ordered]@{}
foreach ($l in $KeepLabels) { $v = Get-ByLabel $wb '1 Client' $l; if ($v) { $kept[$l] = $v } }

$xl.ScreenUpdating = $false
try {
    Clear-Entries $wb
    if ($choice -ne 'Clear all entries (empty workbook)') { Fill-Set $wb $Sets[$choice] }
    foreach ($l in $kept.Keys) { Set-ByLabel $wb '1 Client' $l $kept[$l] }
} finally {
    $xl.ScreenUpdating = $true
}
$xl.Visible = $true
$wb.Worksheets.Item('1 Client').Activate()

$msg = if ($choice -like 'Clear*') { 'All entries cleared.' } else { "Loaded: $choice" }
if ($kept.Count) { $msg += "`n`nKept from the workbook: " + (($kept.Keys | ForEach-Object { "$_ = $($kept[$_])" }) -join '; ') }
if ($script:Missing.Count) {
    $msg += "`n`nCould not find these (label renamed or removed?):`n" + ($script:Missing -join "`n")
}
[void][System.Windows.Forms.MessageBox]::Show($msg, 'Load Test Data', 'OK', $(if ($script:Missing.Count) { 'Warning' } else { 'Information' }))
