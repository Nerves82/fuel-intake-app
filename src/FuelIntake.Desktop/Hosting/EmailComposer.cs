using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using FuelIntake.Core.Email;
using FuelIntake.Core.Hosting;

namespace FuelIntake.Desktop.Hosting;

/// <summary>
/// Opens a new email for staff to check and send.
/// 1. Classic Outlook (through its automation interface, no admin rights): To, subject, message above
///    the staff member's signature, and the PDF attached.
/// 2. Otherwise (new Outlook or no Outlook): the default mail app via a mailto: link, which can't carry
///    an attachment, and File Explorer opens with the PDF selected so it can be dragged in.
/// </summary>
public sealed class EmailComposer(Dispatcher dispatcher) : IEmailComposer
{
    public Task<ComposeResult> ComposeAsync(EmailDraft draft, string? attachmentLocation, CancellationToken ct = default) =>
        dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (TryOutlook(draft, attachmentLocation)) return new ComposeResult(attachmentLocation is not null, "Outlook");
            }
            catch (Exception ex)
            {
                StartupLog.Write("Email: Outlook couldn't open a new message; using the mail app instead", ex);
            }
            MailTo(draft);
            if (attachmentLocation is not null && File.Exists(attachmentLocation))
                Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select," + attachmentLocation }, UseShellExecute = false });
            return new ComposeResult(false, "your mail app");
        }).Task;

    private static bool TryOutlook(EmailDraft draft, string? attachment)
    {
        var type = Type.GetTypeFromProgID("Outlook.Application");
        if (type is null) return false; // no classic Outlook
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

    private static void MailTo(EmailDraft draft)
    {
        var url = "mailto:" + string.Join(",", draft.To.Select(Uri.EscapeDataString)) +
                  "?subject=" + Uri.EscapeDataString(draft.Subject) +
                  "&body=" + Uri.EscapeDataString(draft.Body.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
