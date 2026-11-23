using FuelIntake.Core.Email;

namespace FuelIntake.Core.Hosting;

/// <summary>How a new email was opened, for the message on screen.</summary>
/// <param name="Attached">True when the attachment is already in the email.</param>
/// <param name="How">"Outlook", "your mail app", …</param>
public sealed record ComposeResult(bool Attached, string How);

/// <summary>
/// Opens a new email for staff to check and send themselves (the app never sends mail on its own).
/// Phase 1 (desktop): classic Outlook with the attachment; otherwise the default mail app without it,
/// with the file shown in File Explorer to drag in. Phase 2: Microsoft Graph draft in the user's mailbox.
/// </summary>
public interface IEmailComposer
{
    Task<ComposeResult> ComposeAsync(EmailDraft draft, string? attachmentLocation, EmailRoute route = EmailRoute.Auto, CancellationToken ct = default);
}

/// <summary>
/// Auto: classic Outlook with the attachment when it's installed, otherwise the default mail app.
/// OutlookWeb: a new message in Outlook on the web (works with new Outlook accounts; no attachment).
/// </summary>
public enum EmailRoute { Auto, OutlookWeb }
