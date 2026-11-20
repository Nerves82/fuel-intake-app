<#
  New-ClientFolder.ps1
  Asks for a client's name, creates their folder, copies the listed templates into it
  with a standard name, fills the client's name and the staff name into the intake workbook (tab 1;
  household row 1 picks it up automatically), writes a log, then opens the folder and the workbook.

  EDIT THE SETTINGS BELOW. Nothing else needs changing.
#>

# ======================= SETTINGS =======================

# Only these template files are copied (exact names, with extension).
$FilesToInclude = @(
    'FA (pg. 1.1) - Application.pdf',
    'FA (pg. 1.2) - ACCESS Verification.pdf',
    'FA (pg. 1.3) - Income+Expenses.pdf',
    'FA (pg. 2.2) - Intake OR GoEngage Equivalent.pdf',
    'FA (pg. 2.3-2.6) - Consent Forms.pdf',
    'FA (pg. 3.1) - Fuel Order.pdf',
    'FA (pg. 3.2) - Electric Pledge.pdf',
    'Fuel_Assistance_Intake.xlsx'
)

# Folder that holds the master templates
$TemplateFolder = Join-Path $env:USERPROFILE 'Northeast Kingdom Community Action, Inc\3025 Crisis Fuel (07 31) - General\Navigator\Masters\Fuel Assistance - Applications and Forms'

# Folder where each client's folder is created (inside your Documents folder)
$ClientsRootFolder = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Client Files - Fuel 26-27'

$FolderNameSeparator = ', '     # between last and first name in the folder name
$FileNameSeparator   = '_'      # between the parts of each file name
$AddDateToFileNames  = $true    # $true adds _MM_dd_yy to the end of each file name, $false leaves it off

# The intake workbook from the list above. Its copy gets the client's first and last
# name typed into tab "1 Client". Set to '' to skip this step.
$IntakeWorkbook = 'Fuel_Assistance_Intake.xlsx'

# Open the new client folder when finished? $true or $false
$OpenFolderWhenDone = $true

# ===================== END SETTINGS =====================

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

function Show-Message($text, $icon = 'Information') {
    [void][System.Windows.Forms.MessageBox]::Show($text, 'New Client Folder', 'OK', $icon)
}

function Get-ClientName {
    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'New Client'
    $form.Size = New-Object System.Drawing.Size(380, 210)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.TopMost = $true
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 10)

    $l1 = New-Object System.Windows.Forms.Label
    $l1.Text = 'First name:'; $l1.Location = '15,20'; $l1.AutoSize = $true
    $t1 = New-Object System.Windows.Forms.TextBox
    $t1.Location = '115,17'; $t1.Width = 230

    $l2 = New-Object System.Windows.Forms.Label
    $l2.Text = 'Last name:'; $l2.Location = '15,60'; $l2.AutoSize = $true
    $t2 = New-Object System.Windows.Forms.TextBox
    $t2.Location = '115,57'; $t2.Width = 230

    $ok = New-Object System.Windows.Forms.Button
    $ok.Text = 'Create'; $ok.Location = '170,110'; $ok.Width = 85
    $ok.DialogResult = [System.Windows.Forms.DialogResult]::OK
    $cancel = New-Object System.Windows.Forms.Button
    $cancel.Text = 'Cancel'; $cancel.Location = '260,110'; $cancel.Width = 85
    $cancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel

    $form.Controls.AddRange(@($l1, $t1, $l2, $t2, $ok, $cancel))
    $form.AcceptButton = $ok
    $form.CancelButton = $cancel
    $form.Add_Shown({ $t1.Focus() })

    if ($form.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return $null }
    return @{ First = $t1.Text.Trim(); Last = $t2.Text.Trim() }
}

function Write-Log($text) {
    $line = '{0}  {1}' -f (Get-Date).ToString('MM/dd/yyyy HH:mm:ss'), $text
    Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8
}

function New-ProgressWindow($max) {
    $f = New-Object System.Windows.Forms.Form
    $f.Text = 'New Client Folder - working...'
    $f.Size = New-Object System.Drawing.Size(440, 140)
    $f.StartPosition = 'CenterScreen'
    $f.FormBorderStyle = 'FixedDialog'
    $f.ControlBox = $false
    $f.TopMost = $true
    $f.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $lbl = New-Object System.Windows.Forms.Label
    $lbl.Location = '15,15'; $lbl.Size = '400,20'; $lbl.AutoEllipsis = $true; $lbl.Text = 'Starting...'
    $bar = New-Object System.Windows.Forms.ProgressBar
    $bar.Location = '15,45'; $bar.Size = '395,22'; $bar.Minimum = 0; $bar.Maximum = $max
    $f.Controls.AddRange(@($lbl, $bar))
    $f.Show(); $f.Refresh()
    return @{ Form = $f; Label = $lbl; Bar = $bar }
}

function Update-Progress($p, $text) {
    $p.Label.Text = $text
    if ($p.Bar.Value -lt $p.Bar.Maximum) { $p.Bar.Value++ }
    $p.Form.Refresh()
    [System.Windows.Forms.Application]::DoEvents()
}

function Set-WorkbookName($path, $first, $last) {
    # Types the client's name into tab "1 Client" next to the matching labels, using Excel.
    $xl = $null; $wb = $null
    try {
        $xl = New-Object -ComObject Excel.Application
        $xl.Visible = $false
        $xl.DisplayAlerts = $false
        $wb = $xl.Workbooks.Open($path)
        $ws = $wb.Worksheets.Item('1 Client')

        # Staff name: the name signed in to Office; falls back to the Windows account name
        $staff = "$($xl.UserName)".Trim()
        if (-not $staff) {
            try { $staff = "$(([adsi]"WinNT://$env:USERDOMAIN/$env:USERNAME,user").FullName)".Trim() } catch { }
        }
        if (-not $staff) { $staff = $env:USERNAME }
        $script:StaffName = $staff

        $done = 0
        foreach ($pair in @(@('Client first name', $first), @('Client last name', $last))) {
            $hit = $ws.Range('B1:B200').Find($pair[0], [Type]::Missing, -4163, 1)   # xlValues, xlWhole
            if ($hit) { $hit.Offset(0, 1).Value2 = $pair[1]; $done++ }
        }
        # Match this person on the Staff tab (by work email, then by name). If found, writing their
        # name is enough - initials, phone and email fill in from the Staff tab.
        $upn = ''
        try { $upn = "$(& whoami.exe /upn 2>$null)".Trim() } catch { }
        if ($upn -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { $upn = '' }
        $listed = ''
        try {
            $sv = $wb.Worksheets.Item('Staff').Range('B5:E104').Value2
            for ($r = 1; $r -le 100; $r++) {
                $nm = ([string]$sv[$r, 1]).Trim(); $em = ([string]$sv[$r, 4]).Trim()
                if (-not $nm) { continue }
                if (($upn -and $em -ieq $upn) -or ($nm -ieq $staff)) { $listed = $nm; break }
            }
        } catch { }
        $hit = $ws.Range('B1:B200').Find('Staff name', [Type]::Missing, -4163, 1)
        if ($listed) {
            if ($hit) { $hit.Offset(0, 1).Value2 = $listed }
            $script:StaffName = "$listed (from Staff tab)"
        } else {
            if ($hit) { $hit.Offset(0, 1).Value2 = $staff }
            if ($upn) {
                $hit = $ws.Range('B1:B200').Find('Staff email (only if different)', [Type]::Missing, -4163, 1)
                if ($hit) { $hit.Offset(0, 1).Value2 = $upn }
                $script:StaffName = "$staff <$upn> - not on the Staff tab yet"
            } else {
                $script:StaffName = "$staff - not on the Staff tab yet"
            }
        }
        $wb.Save()
        return $done -eq 2
    } finally {
        if ($wb) { $wb.Close($false); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($wb) }
        if ($xl) { $xl.Quit(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl) }
        [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    }
}

# ---- checks
if (-not (Test-Path -LiteralPath $TemplateFolder -PathType Container)) {
    Show-Message "Template folder not found:`n$TemplateFolder`n`nOpen the script in Notepad and fix the TemplateFolder setting." 'Error'; exit 1
}
if (-not (Test-Path -LiteralPath $ClientsRootFolder -PathType Container)) {
    $answer = [System.Windows.Forms.MessageBox]::Show("Clients folder not found:`n$ClientsRootFolder`n`nCreate it now?",
        'New Client Folder', 'YesNo', 'Question')
    if ($answer -ne 'Yes') { exit 1 }
    New-Item -ItemType Directory -Path $ClientsRootFolder | Out-Null
}

# ---- ask for the name (repeat until valid or cancelled)
$badChars = [System.IO.Path]::GetInvalidFileNameChars()
while ($true) {
    $name = Get-ClientName
    if ($null -eq $name) { exit 0 }
    if (-not $name.First -or -not $name.Last) { Show-Message 'Please enter both a first and last name.' 'Warning'; continue }
    if (($name.First + $name.Last).IndexOfAny($badChars) -ge 0) {
        Show-Message 'Names cannot contain any of these characters:  \ / : * ? " < > |' 'Warning'; continue
    }
    break
}
$first = $name.First; $last = $name.Last
$initial = $first.Substring(0, 1)
$today = (Get-Date).ToString('MM_dd_yy')

# ---- create the client folder
$clientFolder = Join-Path $ClientsRootFolder ($last + $FolderNameSeparator + $first)
if (Test-Path -LiteralPath $clientFolder) {
    $answer = [System.Windows.Forms.MessageBox]::Show(
        "A folder already exists for this client:`n$clientFolder`n`nCopy any missing templates into it? (Existing files are never overwritten.)",
        'New Client Folder', 'YesNo', 'Question')
    if ($answer -ne 'Yes') { exit 0 }
} else {
    New-Item -ItemType Directory -Path $clientFolder | Out-Null
}
$script:LogFile = Join-Path $clientFolder 'automationlog.txt'
Write-Log "Run started for $first $last"
Write-Log "Templates: $TemplateFolder"
$progress = New-ProgressWindow ($FilesToInclude.Count + 1)

# ---- copy and rename the listed templates
$copied = 0; $existing = @(); $failed = @(); $workbookCopy = $null
$templates = Get-ChildItem -LiteralPath $TemplateFolder -File
foreach ($want in $FilesToInclude) {
    Update-Progress $progress "Copying: $want"
    $file = $templates | Where-Object { $_.Name -eq $want } | Select-Object -First 1
    if (-not $file) { $failed += "$want (not found in template folder)"; Write-Log "MISSING   $want"; continue }

    $parts = @($initial, $last, $file.BaseName)
    if ($AddDateToFileNames) { $parts += $today }
    $newName = ($parts -join $FileNameSeparator) + $file.Extension
    $dest = Join-Path $clientFolder $newName

    if (Test-Path -LiteralPath $dest) { $existing += $newName; Write-Log "EXISTS    $newName (not overwritten)"; continue }
    try {
        Copy-Item -LiteralPath $file.FullName -Destination $dest -ErrorAction Stop
        $copied++
        Write-Log "COPIED    $($file.Name)  ->  $newName"
        if ($IntakeWorkbook -and $file.Name -eq $IntakeWorkbook) { $workbookCopy = $dest }
    } catch {
        $failed += "$($file.Name): $($_.Exception.Message)"
        Write-Log "FAILED    $($file.Name): $($_.Exception.Message)"
    }
}
foreach ($t in $templates) {
    if ($FilesToInclude -notcontains $t.Name) { Write-Log "NOT LISTED $($t.Name) (not copied)" }
}

# ---- put the client's name into the intake workbook
Update-Progress $progress "Filling in client name in the workbook..."
$wbNote = ''
if ($workbookCopy) {
    try {
        if (Set-WorkbookName $workbookCopy $first $last) {
            Write-Log "WORKBOOK  name filled in: $first $last; staff: $script:StaffName"
            $wbNote = "`nIntake workbook: client name filled in; staff = $script:StaffName."
        } else {
            Write-Log "WORKBOOK  could not find the name cells on tab '1 Client'"
            $wbNote = "`nIntake workbook: copied, but the name cells weren't found - type the name in tab 1."
        }
    } catch {
        Write-Log "WORKBOOK  Excel step failed: $($_.Exception.Message)"
        $wbNote = "`nIntake workbook: copied, but the name couldn't be filled in (see log) - type it in tab 1."
    }
}
Write-Log "Run finished: $copied copied, $($existing.Count) already there, $($failed.Count) problems"

$progress.Form.Close()

# ---- report
$msg = "Client folder:`n$clientFolder`n`nCopied: $copied file(s)"
if ($existing.Count) { $msg += "`nAlready there (not overwritten): $($existing.Count)" }
$msg += $wbNote
if ($failed.Count) { $msg += "`n`nPROBLEMS:`n" + ($failed -join "`n") }
Show-Message $msg $(if ($failed.Count) { 'Warning' } else { 'Information' })

if ($OpenFolderWhenDone) { Invoke-Item -LiteralPath $clientFolder }

# ---- open the client's intake workbook
if ($workbookCopy -and (Test-Path -LiteralPath $workbookCopy)) {
    Start-Sleep -Seconds 2
    Invoke-Item -LiteralPath $workbookCopy
}
