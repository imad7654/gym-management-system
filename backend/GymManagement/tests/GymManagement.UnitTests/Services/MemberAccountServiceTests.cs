using FluentAssertions;
using GymManagement.Application.DTOs.Member;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// Member sign-up and the member's own view of their membership.
///
/// The rule underneath all of these is that <b>signing up claims a membership, it never
/// creates one</b>. Every test here is written from the thing that goes wrong if that slips:
/// strangers in the member list, one member reading another's payments, or a lapsed member
/// locked out of the only screen that would have brought them back.
/// </summary>
public class MemberAccountServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 2);

    private readonly ApplicationDbContext _context;
    private readonly MemberAccountService _accounts;

    public MemberAccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"member-accounts-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        var unitOfWork = new UnitOfWork(_context);
        var audit = new Mock<IAuditService>();

        _accounts = new MemberAccountService(
            unitOfWork,
            new FakePasswordHasher(),
            new FakeTokenService(),
            new FixedClock(Today),
            audit.Object);
    }

    /// <summary>Reversible hashing stand-in. Real hashing is tested where it lives.</summary>
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string password, string hash) => hash == $"hashed:{password}";
    }

    /// <summary>Issues predictable tokens, so a test can assert who was signed in.</summary>
    private sealed class FakeTokenService : ITokenService
    {
        public string GenerateAccessToken(User user, IEnumerable<string> roles) =>
            $"access:{user.Id}:{string.Join(",", roles)}";

        public RefreshToken GenerateRefreshToken() => new()
        {
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        public int? ValidateAccessToken(string token) => null;
    }

    private async Task SeedMemberRoleAsync()
    {
        _context.Roles.Add(new Role { Name = Roles.Client, Description = "Member access" });
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// A member as reception would have created them. <paramref name="endsOn"/> null means
    /// they have never paid.
    /// </summary>
    private async Task<Client> SeedMemberAsync(
        string firstName = "Rita",
        string lastName = "Khoury",
        string phone = "03 123 456",
        DateOnly? endsOn = null,
        bool isActive = true)
    {
        var client = new Client
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phone,
            IsActive = isActive,
            MembershipStartDate = endsOn.HasValue
                ? Today.AddDays(-30).ToDateTime(TimeOnly.MinValue)
                : null,
            MembershipEndDate = endsOn?.ToDateTime(TimeOnly.MinValue)
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    private static RegisterMemberRequest SignUp(
        string phone = "03 123 456",
        string lastName = "Khoury",
        string email = "rita@example.com") => new()
        {
            PhoneNumber = phone,
            LastName = lastName,
            Email = email,
            Password = "a-long-enough-password",
            ConfirmPassword = "a-long-enough-password"
        };

    // ---------------------------------------------------------------- signing up

    [Fact]
    public async Task Register_WhenThePhoneAndSurnameMatchAMember_ClaimsThatMembership()
    {
        await SeedMemberRoleAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(20));

        var result = await _accounts.RegisterAsync(SignUp());

        result.User.Roles.Should().ContainSingle().Which.Should().Be(Roles.Client);

        var linked = await _context.Clients.FirstAsync(c => c.Id == member.Id);
        linked.UserId.Should().Be(result.User.Id,
            "the account has to point at the membership it claimed, or the member's own "
            + "page has nothing to show them");
    }

    [Fact]
    public async Task Register_WhenNoMemberHasThatPhone_IsRefused()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync(phone: "03 123 456");

        var act = async () => await _accounts.RegisterAsync(SignUp(phone: "03 999 888"));

        // The whole point of matching: a stranger cannot put themselves in the member list,
        // because the member list is what every money report is built on.
        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*could not match*");

        _context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WhenTheSurnameDoesNotMatchTheNumber_IsRefused()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync(lastName: "Khoury", phone: "03 123 456");

        var act = async () =>
            await _accounts.RegisterAsync(SignUp(lastName: "Haddad"));

        // The surname is what stops this endpoint being a way to ask the gym who its
        // members are by trying phone numbers until one is accepted.
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Register_WhenTheNumberIsWrittenDifferently_StillMatches()
    {
        await SeedMemberRoleAsync();

        // Reception wrote it the local way; the member types the international form.
        var member = await SeedMemberAsync(phone: "03 123 456");

        var result = await _accounts.RegisterAsync(SignUp(phone: "+961 3 123 456"));

        var linked = await _context.Clients.FirstAsync(c => c.Id == member.Id);
        linked.UserId.Should().Be(result.User.Id,
            "PhoneNumberKey is the rule for 'same person' everywhere else, and sign-up "
            + "would be unusable if it compared the raw text instead");
    }

    [Fact]
    public async Task Register_WhenTheMembershipAlreadyHasAnAccount_IsRefused()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync();

        await _accounts.RegisterAsync(SignUp());

        var act = async () =>
            await _accounts.RegisterAsync(SignUp(email: "someone-else@example.com"));

        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*already has an account*");
    }

    [Fact]
    public async Task Register_WhenTheEmailIsAlreadyUsed_IsRefused()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync(lastName: "Khoury", phone: "03 123 456");
        await SeedMemberAsync(lastName: "Haddad", phone: "03 777 111");

        await _accounts.RegisterAsync(SignUp(lastName: "Khoury", phone: "03 123 456"));

        var act = async () => await _accounts.RegisterAsync(
            SignUp(lastName: "Haddad", phone: "03 777 111", email: "rita@example.com"));

        // The email is the login. Two rows holding it would make signing in ambiguous.
        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*already uses*");
    }

    [Fact]
    public async Task Register_WhenTheMemberWasRemoved_IsRefused()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync(isActive: false);

        var act = async () => await _accounts.RegisterAsync(SignUp());

        // Somebody the owner took off the list must not be able to put themselves back on
        // it by signing up.
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Register_WhenTwoMembersShareANumberAndSurname_IsRefusedRatherThanGuessed()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync(firstName: "Rita", lastName: "Khoury", phone: "03 123 456");
        await SeedMemberAsync(firstName: "Maya", lastName: "Khoury", phone: "03 123 456");

        var act = async () => await _accounts.RegisterAsync(SignUp());

        // A couple on one phone. Picking either would attach the account to the wrong
        // person's payment history, which is worse than sending them to the desk.
        await act.Should().ThrowAsync<BusinessException>();
    }

    // ------------------------------------------------- the expired member decision

    [Fact]
    public async Task Register_WhenTheMembershipHasExpired_IsStillAllowed()
    {
        await SeedMemberRoleAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(-12));

        var result = await _accounts.RegisterAsync(SignUp());

        var linked = await _context.Clients.FirstAsync(c => c.Id == member.Id);
        linked.UserId.Should().Be(result.User.Id,
            "an expired member is exactly who the gym wants back, and the renew button is "
            + "on the page they would be locked out of");
    }

    [Fact]
    public async Task GetMyMembership_WhenExpired_SaysHowLongAgoRatherThanRefusing()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync(endsOn: Today.AddDays(-12));

        var signUp = await _accounts.RegisterAsync(SignUp());
        var membership = await _accounts.GetMyMembershipAsync(signUp.User.Id);

        membership.Should().NotBeNull();
        membership!.MembershipStatus.Should().Be(nameof(MembershipStatus.Expired));

        // Negative days is what lets the page say "ended 12 days ago" instead of just
        // "expired" - the difference between a nudge and a dead end.
        membership.DaysRemaining.Should().Be(-12);
        membership.CanTrainToday.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyMembership_WhenInTheLastWeek_StillSaysTheyCanTrain()
    {
        await SeedMemberRoleAsync();
        await SeedMemberAsync(endsOn: Today.AddDays(3));

        var signUp = await _accounts.RegisterAsync(SignUp());
        var membership = await _accounts.GetMyMembershipAsync(signUp.User.Id);

        membership!.MembershipStatus.Should().Be(nameof(MembershipStatus.Expiring));

        // Expiring is not Expired. Comparing against Active alone here would tell a member
        // who has paid for three more days that they cannot come in.
        membership.CanTrainToday.Should().BeTrue();
    }

    // ------------------------------------------------------ seeing only your own

    [Fact]
    public async Task GetMyMembership_WhenTheMemberWasRemovedAfterSigningUp_ReturnsNothing()
    {
        await SeedMemberRoleAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(20));

        var signUp = await _accounts.RegisterAsync(SignUp());

        member.SoftDelete();
        await _context.SaveChangesAsync();

        var membership = await _accounts.GetMyMembershipAsync(signUp.User.Id);

        // The login still works - it is not gated on membership - but there is no longer a
        // membership behind it, and the page says so rather than showing a stale one.
        membership.Should().BeNull();
    }

    [Fact]
    public async Task GetMyPayments_ReturnsOnlyTheirOwn()
    {
        await SeedMemberRoleAsync();

        var mine = await SeedMemberAsync(lastName: "Khoury", phone: "03 123 456", endsOn: Today.AddDays(20));
        var someoneElse = await SeedMemberAsync(lastName: "Haddad", phone: "03 777 111", endsOn: Today.AddDays(20));

        var package = new Package { Name = "Monthly", Price = 30m, DurationDays = 30 };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        _context.Payments.AddRange(
            new Payment
            {
                ClientId = mine.Id, PackageId = package.Id, Amount = 30m, AmountReceived = 30m,
                Currency = Currency.Usd, PaymentDate = DateTime.UtcNow,
                PaymentMethod = PaymentMethod.Cash, Status = TransactionStatus.Completed
            },
            new Payment
            {
                ClientId = someoneElse.Id, PackageId = package.Id, Amount = 55m, AmountReceived = 55m,
                Currency = Currency.Usd, PaymentDate = DateTime.UtcNow,
                PaymentMethod = PaymentMethod.Cash, Status = TransactionStatus.Completed
            });

        await _context.SaveChangesAsync();

        var signUp = await _accounts.RegisterAsync(SignUp(lastName: "Khoury", phone: "03 123 456"));
        var payments = await _accounts.GetMyPaymentsAsync(signUp.User.Id);

        // Resolved from the signed-in user, never from an id in the URL. The other member's
        // $55 must not be reachable from here at all.
        payments.Should().ContainSingle().Which.Amount.Should().Be(30m);
    }

    // ------------------------------------------------- the owner resetting it

    [Fact]
    public async Task ResetMemberPassword_EndsEveryLiveSession()
    {
        await SeedMemberRoleAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(20));

        var signUp = await _accounts.RegisterAsync(SignUp());

        var reset = await _accounts.ResetMemberPasswordAsync(
            member.Id,
            new ResetMemberPasswordRequest
            {
                NewPassword = "a-brand-new-password",
                ConfirmPassword = "a-brand-new-password"
            },
            actingUserId: 1);

        reset.Should().BeTrue();

        var user = await _context.Users.FirstAsync(u => u.Id == signUp.User.Id);
        user.PasswordHash.Should().Be("hashed:a-brand-new-password");

        // Blocking the sign-in alone would leave the old session minting access tokens
        // until it expired - which is exactly the hole found when an old admin was deleted.
        var live = await _context.RefreshTokens
            .Where(t => t.UserId == signUp.User.Id && t.RevokedAt == null)
            .CountAsync();

        live.Should().Be(0);
    }

    [Fact]
    public async Task ResetMemberPassword_WhenTheMemberHasNoAccount_SaysSo()
    {
        await SeedMemberRoleAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(20));

        var act = async () => await _accounts.ResetMemberPasswordAsync(
            member.Id,
            new ResetMemberPasswordRequest
            {
                NewPassword = "a-brand-new-password",
                ConfirmPassword = "a-brand-new-password"
            });

        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*no account yet*");
    }

    [Fact]
    public async Task GetAccountForClient_TellsTheOwnerWhetherThereIsALogin()
    {
        await SeedMemberRoleAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(20));

        var before = await _accounts.GetAccountForClientAsync(member.Id);
        before.HasAccount.Should().BeFalse();

        await _accounts.RegisterAsync(SignUp());

        var after = await _accounts.GetAccountForClientAsync(member.Id);
        after.HasAccount.Should().BeTrue();
        after.Email.Should().Be("rita@example.com");
        after.IsActive.Should().BeTrue();
    }

    public void Dispose() => _context.Dispose();
}
