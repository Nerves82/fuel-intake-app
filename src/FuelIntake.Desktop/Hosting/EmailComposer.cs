using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using FuelIntake.Core.Email;
using FuelIntake.Core.Hosting;
using Microsoft.Win32;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Opens a new email for staff to check and send.
/// 1. Classic Outlook, only when it's really installed (through its automation interface, no admin rights):
///    To, subject, message above the staff member's signature, and the PDF attached.
/// 2. Otherwise (new Outlook or no Outlook): the default mail app via a mailto: link, or Outlook on the web
///    when staff choose it. Neither can carry an attachment, so File Explorer opens with the PDF selected.
/// </summary>
public sealed class EmailComposer(Dispatcher dispatcher) : IEmailComposer
{
    public Task<ComposeResult> ComposeAsync(EmailDraft draft, string? attachmentLocation, EmailRoute route = EmailRoute.Auto, CancellationToken ct = default) =>
        dispatcher.InvokeAsync(() =>
        {
            if (route == EmailRoute.Auto)
            {
                try
                {
                    if (TryOutlook(draft, attachmentLocation)) return new ComposeResult(attachmentLocation is not null, "Outlook");
                }
                catch (Exception ex)
                {
                    StartupLog.Write("Email: classic Outlook couldn't open a new message; using the mail app instead (won't try it again on this computer)", ex);
                    RememberClassicOutlookFailed(ex);
                }
            }
            string how;
            if (route == EmailRoute.OutlookWeb) { OutlookWeb(draft); how = "Outlook on the web"; }
            else { MailTo(draft); how = "your mail app"; }
            ShowInFolder(attachmentLocation);
            return new ComposeResult(false, how);
        }).Task;

    /// <summary>File Explorer on the attachment's folder with the file selected, ready to drag into the email.</summary>
    private static void ShowInFolder(string? file)
    {
        if (file is null || !File.Exists(file)) return;
        // explorer wants /select,"path" as written: passing it as one quoted argument opens Documents instead
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = false });
    }

    /// <summary>
    /// Classic Outlook is only used when it's really set up: OUTLOOK.EXE exists, the user has an Outlook mail
    /// profile, and it hasn't failed on this computer before. On NEKCA laptops Office 2016 lists Outlook without
    /// it being set up; asking Windows for it then starts the "installing Outlook 2016" prompt.
    /// </summary>
    private static bool ClassicOutlookReady() =>
        !File.Exists(SkipFile) && ClassicOutlookInstalled() && HasOutlookProfile();

    /// <summary>Written after classic Outlook fails once, so this computer stops trying it (and stops prompting).</summary>
    private static string SkipFile => Path.Combine(StartupLog.AppDataFolder, "classic-outlook-unavailable.txt");

    private static void RememberClassicOutlookFailed(Exception ex)
    {
        try { File.WriteAllText(SkipFile, $"Classic Outlook failed {DateTimeOffset.Now:u}: {ex.GetType().Name}. Delete this file to try it again.\r\n"); }
        catch { /* best effort */ }
    }

    /// <summary>A mail profile exists only once someone has actually set up classic Outlook.</summary>
    private static bool HasOutlookProfile()
    {
        foreach (var version in new[] { "16.0", "15.0" })
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Office\{version}\Outlook\Profiles");
                if (k is not null && k.SubKeyCount > 0) return true;
            }
            catch { /* not readable: try the next */ }
        }
        return false;
    }

    private static bool ClassicOutlookInstalled()
    {
        const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\OUTLOOK.EXE";
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var k = root.OpenSubKey(key);
                if (k?.GetValue(null) is string exe && File.Exists(exe.Trim('"'))) return true;
            }
            catch { /* key not readable: try the next */ }
        }
        return false;
    }

    private static bool TryOutlook(EmailDraft draft, string? attachment)
    {
        if (!ClassicOutlookReady()) return false;
        var type = Type.GetTypeFromProgID("Outlook.Application");
        if (type is null) return false;
        dynamic? app = null, mail = null;
        try
        {
            app = Activator.CreateInstance(type);
            if (app is null) return false;
            mail = app.CreateItem(0); // olMailItem
            mail.To = string.Join("; ", draft.To);
            mail.Subject = draft.Subject;
            _ = mail.GetInspector; // makes Outlook put the user's default signature into the body
            string withSignature = mail.HTMLBody ?? "";
            mail.HTMLBody = InsertAfterBodyTag(withSignature, ToHtml(draft.Body));
            if (attachment is not null) mail.Attachments.Add(attachment);
            mail.Display(false);
            return true;
        }
        finally
        {
            if (mail is not null) Marshal.FinalReleaseComObject(mail);
            if (app is not null) Marshal.FinalReleaseComObject(app);
        }
    }

    private static string ToHtml(string text) =>
        "<div style=\"font-family:Calibri,Arial,sans-serif;font-size:11pt\">" +
        string.Join("<br>", text.Replace("\r\n", "\n").Split('\n').Select(WebUtility.HtmlEncode)) +
        "<br><br></div>";

    private static string InsertAfterBodyTag(string html, string insert)
    {
        var i = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        var end = i < 0 ? -1 : html.IndexOf('>', i);
        return end < 0 ? insert + html : html[..(end + 1)] + insert + html[(end + 1)..];
    }

    /// <summary>A new message in Outlook on the web (the same mailbox the new Outlook uses).</summary>
    private static void OutlookWeb(EmailDraft draft)
    {
        var url = "https://outlook.office.com/mail/deeplink/compose?to=" + Uri.EscapeDataString(string.Join(";", draft.To)) +
                  "&subject=" + Uri.EscapeDataString(draft.Subject) +
                  "&body=" + Uri.EscapeDataString(draft.Body.Replace("\r\n", "\n"));
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void MailTo(EmailDraft draft)
    {
        var url = "mailto:" + string.Join(",", draft.To.Select(Uri.EscapeDataString)) +
                  "?subject=" + Uri.EscapeDataString(draft.Subject) +
                  "&body=" + Uri.EscapeDataString(draft.Body.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
