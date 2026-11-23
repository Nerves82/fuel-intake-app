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
                    StartupLog.Write("Email: classic Outlook couldn't open a new message; using the mail app instead", ex);
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
    /// Classic Outlook is installed when Windows has a path to OUTLOOK.EXE that exists. Without this check,
    /// asking for Outlook on a laptop that only has the new Outlook makes Office offer to install classic Outlook.
    /// </summary>
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
        if (!ClassicOutlookInstalled()) return false;
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
