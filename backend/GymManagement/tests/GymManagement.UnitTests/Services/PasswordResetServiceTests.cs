using FluentAssertions;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// "I forgot my password", for administrators and members alike.
///
/// A reset link is a complete takeover of an account, so these are written from the ways
/// that goes wrong: a link that still works next month, a link that works twice, a public
/// endpoint that answers differently for a real address, and a reset that leaves the old
/// sessions alive.
/// </summary>
public class PasswordResetServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordResetService _reset;
    private readonly CapturingEmailSender _email = new();

    public PasswordResetServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"password-reset-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:PublicUrl"] = "http://localhost:5173"
            })
            .Build();

        _reset = new PasswordResetService(
            new UnitOfWork(_context),
            new FakePasswordHasher(),
            _email,
            new Mock<IAuditService>().Object,
            configuration,
            NullLogger<PasswordResetService>.Instance);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string password, string hash) => hash == $"hashed:{password}";
    }

    /// <summary>Keeps the last email instead of sending it, so a test can read the link.</summary>
    private sealed class CapturingEmailSender : IEmailSender
    {
        public string? LastTo { get; private set; }
        public string? LastBody { get; private set; }
        public int SendCount { get; private set; }

        public bool IsConfigured => true;

        public Task SendAsync(
            string toEmail, string toName, string subject,
            string bodyHtml, string bodyText, CancellationToken cancellationToken = default)
        {
            LastTo = toEmail;
            LastBody = bodyText;
            SendCount++;
            return Task.CompletedTask;
        }
    }

    private async Task<User> SeedUserAsync(
        string email = "owner@gym.local", bool isActive = true)
    {
        var user = new User
        {
            FirstName = "Rita",
            LastName = "Khoury",
            Email = email,
            PasswordHash = "hashed:the-old-password",
            IsActive = isActive
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>Pulls the token out of the emailed link, the way a person clicking it would.</summary>
    private string TokenFromEmail()
    {
        var body = _email.LastBody ?? throw new InvalidOperationException("No email was sent.");
        var marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOfAny(new[] { '\n', '\r', ' ' }, start);

        return Uri.UnescapeDataString(end < 0 ? body[start..] : body[start..end]);
    }

    // ------------------------------------------------------------- asking

    [Fact]
    public async Task RequestReset_SendsALinkToTheAccountHolder()
    {
        var user = await SeedUserAsync();

        await _reset.RequestResetAsync(user.Email);

        _email.SendCount.Should().Be(1);
        _email.LastTo.Should().Be(user.Email);

        var stored = await _context.PasswordResetTokens.SingleAsync();
        stored.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task RequestReset_ForAnUnknownAddress_DoesNothingAndSaysNothing()
    {
        await SeedUserAsync("owner@gym.local");

        // Must not throw. This endpoint is public, so an error - or any answer that differed
        // from the real case - would turn it into a way of testing which emails the gym holds.
        await _reset.RequestResetAsync("nobody@example.com");

        _email.SendCount.Should().Be(0);
        _context.PasswordResetTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestReset_NeverStoresTheTokenItEmailed()
    {
        var user = await SeedUserAsync();

        await _reset.RequestResetAsync(user.Email);

        var emailed = TokenFromEmail();
        var stored = await _context.PasswordResetTokens.SingleAsync();

        // Anyone who got a copy of this table would otherwise hold a working reset link for
        // every account with a request open.
        stored.TokenHash.Should().NotBe(emailed);
        stored.TokenHash.Should().HaveLength(64, "it is a hex SHA-256");
    }

    [Fact]
    public async Task RequestReset_Twice_RetiresTheFirstLink()
    {
        var user = await SeedUserAsync();

        await _reset.RequestResetAsync(user.Email);
        var firstToken = TokenFromEmail();

        await _reset.RequestResetAsync(user.Email);
        var secondToken = TokenFromEmail();

        // Two live links would mean the older email - possibly the one that went astray -
        // still opens the account.
        (await _reset.ResetAsync(firstToken, "a-brand-new-password")).Should().BeFalse();
        (await _reset.ResetAsync(secondToken, "a-brand-new-password")).Should().BeTrue();
    }

    [Fact]
    public async Task RequestReset_ForASwitchedOffAccount_SendsNothing()
    {
        var user = await SeedUserAsync(isActive: false);

        await _reset.RequestResetAsync(user.Email);

        // They cannot sign in, so letting them set a new password would be misleading.
        _email.SendCount.Should().Be(0);
    }

    // ------------------------------------------------------------ resetting

    [Fact]
    public async Task Reset_WithAGoodToken_ChangesThePassword()
    {
        var user = await SeedUserAsync();
        await _reset.RequestResetAsync(user.Email);

        var done = await _reset.ResetAsync(TokenFromEmail(), "a-brand-new-password");

        done.Should().BeTrue();

        var updated = await _context.Users.FirstAsync(u => u.Id == user.Id);
        updated.PasswordHash.Should().Be("hashed:a-brand-new-password");
    }

    [Fact]
    public async Task Reset_WithTheSameTokenTwice_IsRefusedTheSecondTime()
    {
        var user = await SeedUserAsync();
        await _reset.RequestResetAsync(user.Email);
        var token = TokenFromEmail();

        (await _reset.ResetAsync(token, "a-brand-new-password")).Should().BeTrue();

        // The email sits in an inbox indefinitely. A link that still worked would be a
        // permanent back door into the account.
        (await _reset.ResetAsync(token, "another-new-password")).Should().BeFalse();

        var updated = await _context.Users.FirstAsync(u => u.Id == user.Id);
        updated.PasswordHash.Should().Be("hashed:a-brand-new-password",
            "the second attempt must not have taken effect");
    }

    [Fact]
    public async Task Reset_WithAnExpiredToken_IsRefused()
    {
        var user = await SeedUserAsync();
        await _reset.RequestResetAsync(user.Email);
        var token = TokenFromEmail();

        var stored = await _context.PasswordResetTokens.SingleAsync();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();

        (await _reset.ResetAsync(token, "a-brand-new-password")).Should().BeFalse();
    }

    [Fact]
    public async Task Reset_WithAnInventedToken_IsRefused()
    {
        await SeedUserAsync();

        (await _reset.ResetAsync("not-a-real-token", "a-brand-new-password"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Reset_EndsEverySessionThatAccountHolds()
    {
        var user = await SeedUserAsync();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = "a-live-session",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _context.SaveChangesAsync();
        await _reset.RequestResetAsync(user.Email);

        await _reset.ResetAsync(TokenFromEmail(), "a-brand-new-password");

        // Someone resetting has either forgotten the password or thinks another person has
        // it. Leaving the old session minting access tokens defeats the whole exercise.
        var stillLive = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .CountAsync();

        stillLive.Should().Be(0);
    }

    public void Dispose() => _context.Dispose();
}
