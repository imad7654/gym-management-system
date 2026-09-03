using System.Security.Cryptography;
using System.Text;
using GymManagement.Application.DTOs.Auth;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GymManagement.Application.Services;

public interface IPasswordResetService
{
    Task RequestResetAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ResetAsync(string token, string newPassword, CancellationToken cancellationToken = default);
}

/// <summary>
/// "I forgot my password", for administrators and members alike - both are
/// <see cref="User"/> rows, so one flow covers the desk and the member area.
///
/// Before this, a forgotten password was recovered by deleting a row from the database so
/// the seeder would print a new one to the console. That was never something the gym's
/// owner could do, and with only one administrator account it was a genuine lockout.
///
/// Three rules do most of the security work here:
///
/// 1. <b>Asking never reveals whether an account exists.</b> This endpoint is public, so an
///    answer that differed for a real address would turn it into a way of testing which
///    emails the gym holds.
/// 2. <b>Only a hash of the token is stored.</b> A reset link is a complete takeover of an
///    account, so the database must not contain working ones.
/// 3. <b>A token is single use and short lived.</b> The email sits in an inbox forever;
///    the link inside it must not.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    /// <summary>
    /// How long a link works for. Long enough to walk to a computer, short enough that an
    /// old email in an inbox is not a way in.
    /// </summary>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _email;
    private readonly IAuditService _audit;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IEmailSender email,
        IAuditService audit,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _email = email;
        _audit = audit;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Starts a reset. Completes quietly whether or not the address belongs to anyone -
    /// the caller must say the same thing either way.
    /// </summary>
    public async Task RequestResetAsync(
        string email, CancellationToken cancellationToken = default)
    {
        var address = email.Trim();

        // Query(), not QueryIncludingDeleted(): a switched-off account cannot sign in, so
        // letting it set a new password would be pointless and misleading.
        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Email == address, cancellationToken);

        if (user == null)
        {
            // Logged, not returned. Somebody mistyping their own address is the usual cause
            // and the log is where that gets diagnosed - the response stays identical.
            _logger.LogInformation(
                "A password reset was requested for an address with no account.");
            return;
        }

        // Any link already sent is retired. Otherwise asking twice would leave two working
        // links, and the older email - possibly the one that went astray - would still open
        // the account.
        var outstanding = await _unitOfWork.PasswordResetTokens.Query()
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var old in outstanding)
        {
            old.UsedAt = now;
        }

        var rawToken = GenerateToken();

        await _unitOfWork.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            ExpiresAt = now.Add(TokenLifetime)
        }, cancellationToken);

        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Updated,
            $"{user.FullName} asked to reset their password",
            "A reset link was sent to their email address. The password has not changed yet.",
            user.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await SendResetEmailAsync(user, rawToken, cancellationToken);
    }

    /// <summary>
    /// Spends a token and sets the new password. Returns false when the token is unknown,
    /// already used or expired - the caller says the same thing for all three, since
    /// distinguishing them tells an attacker which guesses were close.
    /// </summary>
    public async Task<bool> ResetAsync(
        string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var hash = Hash(token.Trim());

        var reset = await _unitOfWork.PasswordResetTokens.Query()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        var now = DateTime.UtcNow;

        if (reset == null || !reset.IsUsable(now)) return false;

        // The Include goes through the User query filter, so a switched-off account yields
        // null here rather than a row - which is the behaviour wanted, but only reads
        // correctly if it is checked.
        var user = reset.User;
        if (user == null) return false;

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        reset.UsedAt = now;

        // Every session ends. Someone resetting a password has either forgotten it or
        // believes another person has it, and in both cases leaving the old sessions alive
        // to keep minting access tokens defeats the whole exercise.
        var live = await _unitOfWork.RefreshTokens.Query()
            .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var session in live)
        {
            session.RevokedAt = now;
        }

        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Updated,
            $"{user.FullName} reset their own password by email",
            $"Set using a link sent to their address. {live.Count} active session(s) were "
            + "ended, so every device signs in again.",
            user.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task SendResetEmailAsync(
        User user, string rawToken, CancellationToken cancellationToken)
    {
        var baseUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:5173")
            .TrimEnd('/');

        var link = $"{baseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var minutes = (int)TokenLifetime.TotalMinutes;

        // Named rather than hardcoded, so a clone for another gym does not email its members
        // about their "Fit Bear Gym account".
        var gymName = _configuration["Gym:Name"] ?? "the gym";

        var text =
            $"Hello {user.FirstName},\n\n"
            + $"Someone asked to reset the password for your {gymName} account.\n\n"
            + $"Open this link to choose a new one:\n{link}\n\n"
            + $"The link works once and stops working after {minutes} minutes.\n\n"
            + "If this was not you, you can ignore this email - nothing has changed.\n";

        var html =
            $"<p>Hello {user.FirstName},</p>"
            + $"<p>Someone asked to reset the password for your {gymName} account.</p>"
            + $"<p><a href=\"{link}\">Choose a new password</a></p>"
            + $"<p>The link works once and stops working after {minutes} minutes.</p>"
            + "<p>If this was not you, you can ignore this email - nothing has changed.</p>";

        try
        {
            await _email.SendAsync(
                user.Email, user.FullName,
                $"Reset your {gymName} password",
                html, text, cancellationToken);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose. The token is already saved, so surfacing a mail failure
            // here would both leak that the address exists and leave the person with an
            // error they cannot act on. It goes to the log, which is where an unreachable
            // mail server gets noticed.
            _logger.LogError(ex, "Could not send a password reset email.");
        }
    }

    /// <summary>
    /// 32 cryptographically random bytes, URL-safe. Long enough that guessing is not a
    /// route in, and shaped to survive being pasted out of an email client.
    /// </summary>
    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// SHA-256, hex. Not a password hash: this input is 256 bits of randomness rather than
    /// something a person chose, so there is nothing for bcrypt's work factor to defend
    /// against and a fast hash is the right tool.
    /// </summary>
    private static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
