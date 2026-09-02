namespace GymManagement.Application.Interfaces;

/// <summary>
/// Sending an email, without saying how.
///
/// Declared here and implemented in Infrastructure, the same split the member import uses
/// for spreadsheets: SMTP, MailKit and the gym's Gmail credentials are IO concerns and have
/// no business in the Application layer. It also means the password-reset flow can be
/// tested without a mail server.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends one message. Throws if it could not be sent - the caller decides whether that
    /// should surface to the person or only to the log.
    /// </summary>
    Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string bodyHtml,
        string bodyText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a real mail server is configured. False means email is going to the log
    /// instead of to anybody, which callers must not present to a person as "email sent".
    /// </summary>
    bool IsConfigured { get; }
}
