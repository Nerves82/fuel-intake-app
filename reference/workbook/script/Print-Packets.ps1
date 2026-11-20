<#
  Print-Packets.ps1
  Saves the client's filled-in forms from the intake workbook as combined PDFs.

  - Pick a ready-made pack, or tick the forms you want (that makes it a Custom pack).
  - Edit the file name if you like (.pdf is added for you).
  - Click Save PDF. The window stays open so you can save another pack.

  Every tab whose name starts with "Form - " shows up in the list automatically.
  Forms are always saved in the order their tabs appear in the workbook.

  EDIT THE SETTINGS BELOW to change the ready-made packs or their file names.
#>

# ======================= SETTINGS =======================

# File name codes: {Initial} {Last} {First} {Date}
$DateFormat = 'MM_dd_yy'

# Ready-made packs. Tabs = form tab names without the "Form - " part.
# FuelOrPledge = $true adds the Fuel Order or Electric Pledge, based on the crisis type on tab 3.
$Packs = @(
    @{ Name = 'Cover Sheet + GoEngage Data (staff only - do not upload)'
       File = '{Initial}_{Last}_cover_sheet_{Date}'
       Tabs = @('Cover Sheet', 'GoEngage Data') },
    @{ Name = 'Intake Form Package'
       File = '{Initial}_{Last}_signed_intake_{Date}'
       Tabs = @('Intake p1', 'Intake p2') },
    @{ Name = 'Application Forms Pack'
       File = '{Initial}_{Last}_fuel_assistance_{Date}'
       Tabs = @('Application', 'ACCESS', 'Income Expense')
       FuelOrPledge = $true },
    @{ Name = 'All forms'
       File = '{Initial}_{Last}_all_forms_{Date}'
       Tabs = @('*') }
)
$CustomFile = '{Initial}_{Last}_forms_{Date}'

# Where client folders live - keep these the same as in New-ClientFolder.ps1.
# The PDFs go into "<ClientsRootFolder>\<Last><FolderNameSeparator><First>" when that folder exists.
$ClientsRootFolder   = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Client Files - Fuel 26-27'
$FolderNameSeparator = ', '

# ===================== END SETTINGS =====================

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

function Show-Message($text, $icon = 'Information') {
    [void][System.Windows.Forms.MessageBox]::Show($text, 'Print Packets', 'OK', $icon)
}

function Get-ByLabel($wb, $tab, $label) {
    $ws = $wb.Worksheets.Item($tab)
    $vals = $ws.Range('B1:C300').Value2
    for ($r = 1; $r -le 300; $r++) {
        if (([string]$vals[$r, 1]).Trim() -eq $label) { return ([string]$vals[$r, 2]).Trim() }
    }
    return ''
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
$wb = Find-IntakeWorkbook 'save PDFs from'
if ($wb) {
    $xl = $wb.Application
} else {
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Title = "No intake workbook is open - pick the client's workbook"
    $dlg.Filter = 'Excel workbook (*.xlsx)|*.xlsx'
    $dlg.InitialDirectory = [Environment]::GetFolderPath('MyDocuments')
    if ($dlg.ShowDialog() -ne 'OK') { exit 0 }
    $xl = New-Object -ComObject Excel.Application
    $xl.Visible = $true
    $wb = $xl.Workbooks.Open($dlg.FileName)
}

# ---- client details and form tabs
$first = Get-ByLabel $wb '1 Client' 'Client first name'
$last  = Get-ByLabel $wb '1 Client' 'Client last name'
if (-not $first -or -not $last) { Show-Message "Enter the client's first and last name on tab 1 first." 'Warning'; exit 1 }
$crisis = Get-ByLabel $wb '3 Application' 'Crisis type'
$codes = @{ First = $first; Last = $last; Initial = $first.Substring(0, 1); Date = (Get-Date).ToString($DateFormat) }
# Save folder:
#   1. the workbook's own folder, if it is inside ClientsRootFolder
#   2. the client's folder "<ClientsRootFolder>\<Last>, <First>", if it exists
#   3. otherwise you must pick the folder yourself (the picker opens automatically)
$clientFolder = Join-Path $ClientsRootFolder ($last + $FolderNameSeparator + $first)
function Get-LocalFolder($path) {
    # Excel reports OneDrive/SharePoint files as web addresses (https://...).
    # Find the matching synced folder on this PC by trying each sync location.
    if (-not $path) { return '' }
    if ($path -notmatch '^https?://') { return $path }
    $segs = @(([Uri]$path).AbsolutePath.Split('/') | Where-Object { $_ } | ForEach-Object { [Uri]::UnescapeDataString($_) })
    $roots = @(@($env:OneDriveCommercial, $env:OneDrive, $env:OneDriveConsumer) | Where-Object { $_ })
    $roots += @(Get-ChildItem -LiteralPath $env:USERPROFILE -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
    foreach ($root in ($roots | Select-Object -Unique)) {
        for ($i = 0; $i -lt $segs.Count; $i++) {
            $cand = Join-Path $root ($segs[$i..($segs.Count - 1)] -join '\')
            if (Test-Path -LiteralPath $cand -PathType Container) { return $cand }
        }
    }
    return ''
}
$wbFolder = Get-LocalFolder ([string]$wb.Path)
$rootFull = [System.IO.Path]::GetFullPath($ClientsRootFolder).TrimEnd('\') + '\'
$script:OutFolder = ''
if ($wbFolder -and $wbFolder -notmatch '^https?://' -and (Test-Path -LiteralPath $wbFolder -PathType Container)) {
    $wbFull = [System.IO.Path]::GetFullPath($wbFolder).TrimEnd('\') + '\'
    if ($wbFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase) -and $wbFull -ne $rootFull) { $script:OutFolder = $wbFolder }
}
if (-not $script:OutFolder -and (Test-Path -LiteralPath $clientFolder -PathType Container)) { $script:OutFolder = $clientFolder }

function Pick-Folder {
    $fb = New-Object System.Windows.Forms.FolderBrowserDialog
    $fb.Description = "Pick the client's folder for $first $last"
    $fb.SelectedPath = $(if ($script:OutFolder) { $script:OutFolder } elseif (Test-Path -LiteralPath $ClientsRootFolder) { $ClientsRootFolder } else { [Environment]::GetFolderPath('MyDocuments') })
    if ($fb.ShowDialog() -eq 'OK') { $script:OutFolder = $fb.SelectedPath; return $true }
    return $false
}
function Folder-Text { if ($script:OutFolder) { "Save to: $script:OutFolder" } else { "Save to: (no folder chosen - click Change...)" } }

if (-not $script:OutFolder) {
    Show-Message "Couldn't find a client folder for $first $last.`n`nLooked for:`n$clientFolder`n`nPick the right folder in the next window." 'Warning'
    [void](Pick-Folder)
}

$FormTabs = @()
foreach ($s in $wb.Worksheets) { if ($s.Name -like 'Form - *') { $FormTabs += $s.Name.Substring(7) } }
if (-not $FormTabs.Count) { Show-Message 'No "Form - " tabs found in this workbook.' 'Error'; exit 1 }

function Expand-Name($pattern) {
    $n = $pattern
    foreach ($k in $codes.Keys) { $n = $n.Replace('{' + $k + '}', $codes[$k]) }
    return $n
}

function Get-PackTabs($pack) {
    if ($pack.Tabs -contains '*') { return $FormTabs }
    $t = @($pack.Tabs)
    if ($pack.FuelOrPledge) { $t += $(if ($crisis -eq 'Disconnect Notice') { 'Electric Pledge' } else { 'Fuel Order' }) }
    return $t
}

# ---- window
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Print Packets'
$form.StartPosition = 'CenterScreen'; $form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false; $form.MinimizeBox = $false; $form.TopMost = $true
$form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
$form.ClientSize = New-Object System.Drawing.Size(520, 520)

$lblClient = New-Object System.Windows.Forms.Label
$lblClient.Text = "Client: $first $last"; $lblClient.Location = '12,10'; $lblClient.AutoSize = $true
$lblFolder = New-Object System.Windows.Forms.Label
$lblFolder.Text = Folder-Text; $lblFolder.Location = '12,34'; $lblFolder.Size = '390,22'; $lblFolder.AutoEllipsis = $true
if (-not $script:OutFolder) { $lblFolder.ForeColor = [System.Drawing.Color]::DarkRed }
$btnFolder = New-Object System.Windows.Forms.Button
$btnFolder.Text = 'Change...'; $btnFolder.Location = '410,30'; $btnFolder.Width = 98

$lblPack = New-Object System.Windows.Forms.Label
$lblPack.Text = 'Pack:'; $lblPack.Location = '12,72'; $lblPack.AutoSize = $true
$cboPack = New-Object System.Windows.Forms.ComboBox
$cboPack.DropDownStyle = 'DropDownList'; $cboPack.Location = '70,68'; $cboPack.Width = 438
foreach ($p in $Packs) { [void]$cboPack.Items.Add($p.Name) }
[void]$cboPack.Items.Add('Custom (tick the forms below)')

$lblForms = New-Object System.Windows.Forms.Label
$lblForms.Text = 'Forms in this pack (saved in workbook tab order):'; $lblForms.Location = '12,106'; $lblForms.AutoSize = $true
$lstForms = New-Object System.Windows.Forms.CheckedListBox
$lstForms.Location = '12,130'; $lstForms.Size = '496,230'; $lstForms.CheckOnClick = $true
foreach ($t in $FormTabs) { [void]$lstForms.Items.Add($t) }

$lblName = New-Object System.Windows.Forms.Label
$lblName.Text = 'File name:'; $lblName.Location = '12,376'; $lblName.AutoSize = $true
$txtName = New-Object System.Windows.Forms.TextBox
$txtName.Location = '90,372'; $txtName.Width = 380
$lblPdf = New-Object System.Windows.Forms.Label
$lblPdf.Text = '.pdf'; $lblPdf.Location = '474,376'; $lblPdf.AutoSize = $true

$chkOpen = New-Object System.Windows.Forms.CheckBox
$chkOpen.Text = 'Open the PDF after saving'; $chkOpen.Checked = $true; $chkOpen.Location = '12,410'; $chkOpen.AutoSize = $true

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Location = '12,440'; $lblStatus.Size = '496,24'; $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen; $lblStatus.AutoEllipsis = $true

$btnSave = New-Object System.Windows.Forms.Button
$btnSave.Text = 'Save PDF'; $btnSave.Location = '310,476'; $btnSave.Width = 98
$btnClose = New-Object System.Windows.Forms.Button
$btnClose.Text = 'Close'; $btnClose.Location = '410,476'; $btnClose.Width = 98; $btnClose.DialogResult = 'Cancel'

$form.Controls.AddRange(@($lblClient, $lblFolder, $btnFolder, $lblPack, $cboPack, $lblForms, $lstForms,
    $lblName, $txtName, $lblPdf, $chkOpen, $lblStatus, $btnSave, $btnClose))
$form.AcceptButton = $btnSave; $form.CancelButton = $btnClose

# ---- behavior
$script:Updating = $false
$customIndex = $cboPack.Items.Count - 1

$cboPack.Add_SelectedIndexChanged({
    if ($script:Updating) { return }
    $i = $cboPack.SelectedIndex
    if ($i -eq $customIndex) { return }          # Custom: leave ticks and name as they are
    $script:Updating = $true
    $want = Get-PackTabs $Packs[$i]
    for ($k = 0; $k -lt $lstForms.Items.Count; $k++) { $lstForms.SetItemChecked($k, ($want -contains $lstForms.Items[$k])) }
    $txtName.Text = Expand-Name $Packs[$i].File
    $script:Updating = $false
})

$lstForms.Add_ItemCheck({
    if ($script:Updating) { return }
    $script:Updating = $true
    if ($cboPack.SelectedIndex -ne $customIndex) {
        $cboPack.SelectedIndex = $customIndex
        $txtName.Text = Expand-Name $CustomFile
    }
    $script:Updating = $false
})

$btnFolder.Add_Click({
    if (Pick-Folder) { $lblFolder.Text = Folder-Text; $lblFolder.ForeColor = [System.Drawing.Color]::Black }
})

$btnSave.Add_Click({
    $tabs = @()
    foreach ($t in $FormTabs) { if (@($lstForms.CheckedItems) -contains $t) { $tabs += "Form - $t" } }
    if (-not $tabs.Count) { Show-Message 'Tick at least one form.' 'Warning'; return }

    $name = $txtName.Text.Trim()
    if ($name.ToLower().EndsWith('.pdf')) { $name = $name.Substring(0, $name.Length - 4) }
    foreach ($c in [System.IO.Path]::GetInvalidFileNameChars()) { $name = $name.Replace([string]$c, '') }
    if (-not $name) { Show-Message 'Type a file name.' 'Warning'; return }
    if (-not $script:OutFolder -or -not (Test-Path -LiteralPath $script:OutFolder -PathType Container)) {
        Show-Message 'Pick the folder to save in first (Change...).' 'Warning'
        if (-not (Pick-Folder)) { return }
        $lblFolder.Text = Folder-Text; $lblFolder.ForeColor = [System.Drawing.Color]::Black
    }
    $path = Join-Path $script:OutFolder ($name + '.pdf')

    if (Test-Path -LiteralPath $path) {
        $ans = [System.Windows.Forms.MessageBox]::Show("$name.pdf already exists.`nReplace it?", 'Print Packets', 'YesNo', 'Question')
        if ($ans -ne 'Yes') { return }
    }

    $form.Cursor = 'WaitCursor'; $lblStatus.ForeColor = [System.Drawing.Color]::Black; $lblStatus.Text = 'Saving...'; $form.Refresh()
    try {
        $firstTab = $true
        foreach ($t in $tabs) {
            $ws = $wb.Worksheets.Item($t)
            if ($firstTab) { [void]$ws.Select($true); $firstTab = $false } else { [void]$ws.Select($false) }
        }
        $sheet = $xl.ActiveSheet
        try {
            [void][System.__ComObject].InvokeMember('ExportAsFixedFormat', [System.Reflection.BindingFlags]::InvokeMethod,
                $null, $sheet, @([int]0, [string]$path))      # 0 = PDF
        } catch {
            $sheet.ExportAsFixedFormat(0, $path)
        }
        $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
        $lblStatus.Text = "Saved $name.pdf ($($tabs.Count) form$(if ($tabs.Count -ne 1) {'s'}))"
        if ($chkOpen.Checked) { Invoke-Item -LiteralPath $path }
    } catch {
        $lblStatus.ForeColor = [System.Drawing.Color]::DarkRed
        $lblStatus.Text = 'Not saved - see message.'
        Show-Message "Could not save the PDF:`n$($_.Exception.Message)" 'Error'
    } finally {
        try { [void]$wb.Worksheets.Item('1 Client').Select($true) } catch { }   # ungroup tabs
        $form.Cursor = 'Default'
    }
})

# start on the Application Forms Pack if it exists, else the first pack
$start = 0
for ($i = 0; $i -lt $Packs.Count; $i++) { if ($Packs[$i].Name -eq 'Application Forms Pack') { $start = $i } }
$cboPack.SelectedIndex = $start
[void]$form.ShowDialog()
