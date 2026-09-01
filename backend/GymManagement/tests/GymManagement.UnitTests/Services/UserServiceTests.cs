using FluentAssertions;
using GymManagement.Application.DTOs.User;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// The rules that stop the gym being locked out of its own system.
///
/// Before this existed there was one administrator account, no way to make another, and no
/// password reset. A forgotten password meant deleting a database row to get a fresh one
/// printed to the console. These tests are the guard rails on the way out of that, so they
/// are written from the failure they prevent rather than from the method they call.
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserService _users;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"users-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        var unitOfWork = new UnitOfWork(_context);
        var audit = new Mock<IAuditService>();

        _users = new UserService(unitOfWork, new FakePasswordHasher(), audit.Object);
    }

    /// <summary>Reversible hashing stand-in. Real hashing is tested where it lives.</summary>
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string password, string hash) => hash == $"hashed:{password}";
    }

    private async Task<Role> SeedAdminRoleAsync()
    {
        var role = new Role { Name = Roles.Admin, Description = "Full system access" };
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();
        return role;
    }

    private async Task<User> SeedAdminAsync(Role role, string email, bool isActive = true)
    {
        var user = new User
        {
            FirstName = "Owner",
            LastName = email.Split('@')[0],
            Email = email,
            PasswordHash = "hashed:whatever",
            IsActive = isActive
        };

        user.UserRoles.Add(new UserRole { Role = role });
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task DeactivateUser_WhenTheyAreTheOnlyAdministrator_IsRefused()
    {
        var role = await SeedAdminRoleAsync();
        var onlyAdmin = await SeedAdminAsync(role, "owner@gym.local");
        var someoneElse = await SeedAdminAsync(role, "second@gym.local");

        // The second administrator is switched off first, leaving one.
        await _users.DeactivateUserAsync(someoneElse.Id, actingUserId: onlyAdmin.Id);

        // Acting id is a different account so the "not yourself" rule is not what refuses
        // this - the last-administrator rule has to be what does.
        var act = async () =>
            await _users.DeactivateUserAsync(onlyAdmin.Id, actingUserId: someoneElse.Id);

        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*only administrator*");

        (await _context.Users.FindAsync(onlyAdmin.Id))!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateUser_WhenAnotherAdministratorRemains_IsAllowed()
    {
        var role = await SeedAdminRoleAsync();
        var first = await SeedAdminAsync(role, "owner@gym.local");
        var second = await SeedAdminAsync(role, "second@gym.local");

        await _users.DeactivateUserAsync(second.Id, actingUserId: first.Id);

        (await _context.Users.FindAsync(second.Id))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateUser_WhenItIsYourOwnAccount_IsRefused()
    {
        var role = await SeedAdminRoleAsync();
        var me = await SeedAdminAsync(role, "owner@gym.local");
        await SeedAdminAsync(role, "second@gym.local");

        // Another administrator exists, so only the "not yourself" rule can refuse this.
        var act = async () => await _users.DeactivateUserAsync(me.Id, actingUserId: me.Id);

        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*your own account*");
    }

    [Fact]
    public async Task DeactivatedAdministrators_DoNotCountTowardsTheLastAdminRule()
    {
        var role = await SeedAdminRoleAsync();
        var active = await SeedAdminAsync(role, "owner@gym.local");

        // Switched off, so they cannot sign in and cannot rescue anyone from a lockout.
        await SeedAdminAsync(role, "old@gym.local", isActive: false);

        var act = async () => await _users.DeactivateUserAsync(active.Id, actingUserId: 999);

        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*only administrator*");
    }

    [Fact]
    public async Task ResetPassword_EndsEveryLiveSession()
    {
        var role = await SeedAdminRoleAsync();
        var user = await SeedAdminAsync(role, "owner@gym.local");

        _context.RefreshTokens.AddRange(
            new RefreshToken
            {
                UserId = user.Id,
                Token = "live-one",
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            },
            new RefreshToken
            {
                UserId = user.Id,
                Token = "live-two",
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
        await _context.SaveChangesAsync();

        await _users.ResetPasswordAsync(
            user.Id, new ResetUserPasswordRequest { NewPassword = "a-brand-new-password" });

        // Not revoking these was the real defect: the sign-in would use the new password
        // while anyone already signed in kept minting access tokens from the old session.
        var stillLive = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .CountAsync();

        stillLive.Should().Be(0);

        (await _context.Users.FindAsync(user.Id))!
            .PasswordHash.Should().Be("hashed:a-brand-new-password");
    }

    [Fact]
    public async Task DeactivateUser_EndsTheirLiveSessions()
    {
        var role = await SeedAdminRoleAsync();
        var first = await SeedAdminAsync(role, "owner@gym.local");
        var second = await SeedAdminAsync(role, "second@gym.local");

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = second.Id,
            Token = "second-session",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _context.SaveChangesAsync();

        await _users.DeactivateUserAsync(second.Id, actingUserId: first.Id);

        // Blocking the login is not enough on its own - a live refresh token would have
        // kept the switched-off account working until it expired.
        (await _context.RefreshTokens.SingleAsync(t => t.Token == "second-session"))
            .RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUser_WhenTheEmailBelongsToASwitchedOffAccount_IsRefused()
    {
        var role = await SeedAdminRoleAsync();
        var gone = await SeedAdminAsync(role, "taken@gym.local", isActive: false);

        var act = async () => await _users.CreateUserAsync(new CreateUserRequest
        {
            FirstName = "New",
            LastName = "Person",
            Email = "taken@gym.local",
            Password = "a-long-enough-password"
        });

        // The email is the login. Two rows sharing one would make restoring the old
        // account ambiguous.
        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*already uses*");

        gone.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetUsers_MarksWhoYouAreAndWhoCannotBeRemoved()
    {
        var role = await SeedAdminRoleAsync();
        var me = await SeedAdminAsync(role, "owner@gym.local");

        var list = await _users.GetUsersAsync(currentUserId: me.Id);

        var mine = list.Single(u => u.Id == me.Id);
        mine.IsYou.Should().BeTrue();
        mine.IsLastAdmin.Should().BeTrue();

        await _users.CreateUserAsync(new CreateUserRequest
        {
            FirstName = "Second",
            LastName = "Owner",
            Email = "second@gym.local",
            Password = "a-long-enough-password"
        });

        // Once a second administrator exists, neither is the last one.
        var after = await _users.GetUsersAsync(currentUserId: me.Id);
        after.Should().OnlyContain(u => !u.IsLastAdmin);
        after.Should().HaveCount(2);
    }

    public void Dispose() => _context.Dispose();
}
