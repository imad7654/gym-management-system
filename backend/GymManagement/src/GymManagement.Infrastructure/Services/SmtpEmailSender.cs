using System.Diagnostics.CodeAnalysis;
using GymManagement.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;

namespace GymManagement.Infrastructure.Services;

/// <summary>
/// Sends mail through an ordinary SMTP server, which for this gym means Gmail.
///
/// Gmail will not accept an account's normal password over SMTP. The gym has to turn on
/// 2-Step Verification and generate a 16-character <b>App Password</b>, and that is what
/// goes in <c>Email:Password</c>. Using the everyday password fails with a confusing
/// "username and password not accepted", which is worth knowing before spending an hour on
/// it.
///
/// The credentials never live in a tracked file. They belong in user-secrets beside the JWT
/// key and the connection string:
///
/// <code>
/// dotnet user-secrets set "Email:Password" "your-16-char-app-password"
/// </code>
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;

    private readonly string? _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly bool _useStartTls;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;

        _host = configuration["Email:Host"];
        _port = configuration.GetValue("Email:Port", 587);
        _username = configuration["Email:Username"];
        _password = configuration["Email:Password"];
        _fromAddress = configuration["Email:FromAddress"] ?? _username ?? string.Empty;
        // Falls back to the gym's own name rather than a hardcoded one, so a clone that sets
        // only Gym:Name still sends mail signed as itself.
        _fromName = configuration["Email:FromName"]
            ?? configuration["Gym:Name"]
            ?? "The gym";

        // 587 with STARTTLS is what Gmail wants. 465 is implicit SSL from the start.
        _useStartTls = _port != 465;
    }

    /// <summary>
    /// Whether there is a mail server to send through.
    ///
    /// The annotation tells the compiler what the guard in <see cref="SendAsync"/> already
    /// guarantees: past that check these four are not null. Without it every use below is a
    /// nullable warning, and the tempting fix is to silence each one with `!` — which would
    /// keep working after somebody removed the guard.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_host), nameof(_username), nameof(_password), nameof(_fromAddress))]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_host)
        && !string.IsNullOrWhiteSpace(_username)
        && !string.IsNullOrWhiteSpace(_password)
        && !string.IsNullOrWhiteSpace(_fromAddress);

    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string bodyHtml,
        string bodyText,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "No mail server is configured. Set Email:Host, Email:Username, "
                + "Email:Password and Email:FromAddress before sending.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;

        // Both parts, so a client that refuses HTML still shows something readable - which
        // matters here because the alternative is a member seeing an empty reset email.
        var body = new BodyBuilder
        {
            HtmlBody = bodyHtml,
            TextBody = bodyText
        };

        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _host,
            _port,
            _useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect,
            cancellationToken);

        await client.AuthenticateAsync(_username, _password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        // The address is deliberately not logged. A log of who asked for a password reset,
        // and when, is worth something to somebody.
        _logger.LogInformation("Sent a {Subject} email.", subject);
    }
}

/// <summary>
/// Writes the email to the log instead of sending it, for development where there is no
/// mail account.
///
/// This exists so the reset flow can be built and demonstrated before the gym has a Gmail
/// address, not as a fallback for production - <see cref="IsConfigured"/> is false, and the
/// startup checks refuse to boot outside development if this is what is registered.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured => false;

    public Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string bodyHtml,
        string bodyText,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "No mail server configured, so this email was NOT sent. It would have gone to "
            + "{ToEmail}:\n--- {Subject} ---\n{Body}\n---",
            toEmail, subject, bodyText);

        return Task.CompletedTask;
    }
}
