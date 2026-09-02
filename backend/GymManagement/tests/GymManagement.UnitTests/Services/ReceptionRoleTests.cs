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
/// Reception accounts, and the lockout rules that now have a second door.
///
/// Reception cannot reach the accounts screen. That is the whole reason the rules here
/// matter: every way of ending up with no administrator who can sign in is a way of ending
/// up with a gym nobody can administer, and moving the last one down to reception is just
/// as final as switching them off.
/// </summary>
public class ReceptionRoleTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserService _users;

    public ReceptionRoleTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"reception-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        _users = new UserService(
            new UnitOfWork(_context),
            new FakePasswordHasher(),
            new Mock<IAuditService>().Object);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";
        public bool VerifyPassword(string password, string hash) => hash == $"hashed:{password}";
    }

    private async Task SeedRolesAsync()
    {
        _context.Roles.AddRange(
            new Role { Name = Roles.Admin, Description = "Full system access" },
            new Role { Name = Roles.Staff, Description = "Reception" });

        await _context.SaveChangesAsync();
    }

    private static CreateUserRequest NewAccount(
        string email, string role = Roles.Admin) => new()
        {
            FirstName = "Rita",
            LastName = "Khoury",
            Email = email,
            Password = "a-long-enough-password",
            Role = role
        };

    private static UpdateUserRequest Edit(string email, string role) => new()
    {
        FirstName = "Rita",
        LastName = "Khoury",
        Email = email,
        Role = role
    };

    [Fact]
    public async Task CreateUser_CanMakeAReceptionAccount()
    {
        await SeedRolesAsync();

        var created = await _users.CreateUserAsync(
            NewAccount("desk@gym.local", Roles.Staff));

        created.Roles.Should().ContainSingle().Which.Should().Be(Roles.Staff);
    }

    [Fact]
    public async Task CreateUser_DefaultsToAdministrator()
    {
        await SeedRolesAsync();

        // Every account made before the role existed was an administrator, so a request
        // that does not mention a role has to keep meaning that.
        var created = await _users.CreateUserAsync(new CreateUserRequest
        {
            FirstName = "Rita",
            LastName = "Khoury",
            Email = "owner2@gym.local",
            Password = "a-long-enough-password"
        });

        created.Roles.Should().ContainSingle().Which.Should().Be(Roles.Admin);
    }

    [Fact]
    public async Task UpdateUser_CanMoveSomeoneBetweenAdminAndReception()
    {
        await SeedRolesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));
        var second = await _users.CreateUserAsync(NewAccount("second@gym.local"));

        var demoted = await _users.UpdateUserAsync(
            second.Id, Edit("second@gym.local", Roles.Staff), actingUserId: owner.Id);

        demoted!.Roles.Should().ContainSingle().Which.Should().Be(Roles.Staff);

        var promoted = await _users.UpdateUserAsync(
            second.Id, Edit("second@gym.local", Roles.Admin), actingUserId: owner.Id);

        promoted!.Roles.Should().ContainSingle().Which.Should().Be(Roles.Admin);
    }

    [Fact]
    public async Task UpdateUser_DemotingTheOnlyAdministrator_IsRefused()
    {
        await SeedRolesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));
        await _users.CreateUserAsync(NewAccount("desk@gym.local", Roles.Staff));

        var act = async () => await _users.UpdateUserAsync(
            owner.Id, Edit("owner@gym.local", Roles.Staff), actingUserId: owner.Id);

        // Reception cannot open the accounts screen, so this would leave nobody able to
        // put it back - the same lockout as switching the last administrator off, reached
        // by a different door. A reception account existing does not rescue it.
        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*only administrator*");
    }

    [Fact]
    public async Task UpdateUser_ChangingRole_EndsTheirSessions()
    {
        await SeedRolesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));
        var second = await _users.CreateUserAsync(NewAccount("second@gym.local"));

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = second.Id,
            Token = "a-live-session",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _context.SaveChangesAsync();

        await _users.UpdateUserAsync(
            second.Id, Edit("second@gym.local", Roles.Staff), actingUserId: owner.Id);

        // The access token carries the old role until it expires, so a demoted
        // administrator would keep administrator screens for up to fifteen minutes.
        var live = await _context.RefreshTokens
            .Where(t => t.UserId == second.Id && t.RevokedAt == null)
            .CountAsync();

        live.Should().Be(0);
    }

    [Fact]
    public async Task UpdateUser_EditingWithoutChangingRole_LeavesSessionsAlone()
    {
        await SeedRolesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));
        var second = await _users.CreateUserAsync(NewAccount("second@gym.local"));

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = second.Id,
            Token = "a-live-session",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        await _context.SaveChangesAsync();

        // Correcting a typo in somebody's surname should not sign them out.
        await _users.UpdateUserAsync(
            second.Id,
            new UpdateUserRequest
            {
                FirstName = "Rita",
                LastName = "Khoury-Haddad",
                Email = "second@gym.local",
                Role = Roles.Admin
            },
            actingUserId: owner.Id);

        var live = await _context.RefreshTokens
            .Where(t => t.UserId == second.Id && t.RevokedAt == null)
            .CountAsync();

        live.Should().Be(1);
    }

    [Fact]
    public async Task DeactivateUser_AReceptionAccountIsNeverTheLastAdministrator()
    {
        await SeedRolesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));
        var desk = await _users.CreateUserAsync(NewAccount("desk@gym.local", Roles.Staff));

        // Switching off reception must stay possible even though only one administrator
        // exists - the last-administrator rule counts administrators, not accounts.
        var done = await _users.DeactivateUserAsync(desk.Id, actingUserId: owner.Id);

        done.Should().BeTrue();
    }

    [Fact]
    public async Task GetUsers_DoesNotCallAReceptionAccountTheLastAdmin()
    {
        await SeedRolesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));
        await _users.CreateUserAsync(NewAccount("desk@gym.local", Roles.Staff));

        var list = await _users.GetUsersAsync(currentUserId: owner.Id);

        list.Single(u => u.Email == "owner@gym.local").IsLastAdmin.Should().BeTrue();
        list.Single(u => u.Email == "desk@gym.local").IsLastAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task GetUsers_DoesNotListMembers()
    {
        await SeedRolesAsync();
        _context.Roles.Add(new Role { Name = Roles.Client, Description = "Member access" });
        await _context.SaveChangesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));
        await _users.CreateUserAsync(NewAccount("desk@gym.local", Roles.Staff));

        var memberRole = await _context.Roles.FirstAsync(r => r.Name == Roles.Client);
        var member = new User
        {
            FirstName = "Bilal",
            LastName = "Hamdan",
            Email = "bilal@example.com",
            PasswordHash = "hashed:whatever"
        };

        member.UserRoles.Add(new UserRole { Role = memberRole });
        _context.Users.Add(member);
        await _context.SaveChangesAsync();

        var list = await _users.GetUsersAsync(currentUserId: owner.Id);

        // Members became User rows once they could sign in, and this screen showed them
        // under a heading that says it is not for members - offering a "switch off" that
        // would not have taken them off the member list at all.
        list.Should().NotContain(u => u.Email == "bilal@example.com");
        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeactivateUser_WhenTheTargetIsAMember_IsRefused()
    {
        await SeedRolesAsync();
        _context.Roles.Add(new Role { Name = Roles.Client, Description = "Member access" });
        await _context.SaveChangesAsync();

        var owner = await _users.CreateUserAsync(NewAccount("owner@gym.local"));

        var memberRole = await _context.Roles.FirstAsync(r => r.Name == Roles.Client);
        var member = new User
        {
            FirstName = "Bilal",
            LastName = "Hamdan",
            Email = "bilal@example.com",
            PasswordHash = "hashed:whatever"
        };

        member.UserRoles.Add(new UserRole { Role = memberRole });
        _context.Users.Add(member);
        await _context.SaveChangesAsync();

        var act = async () =>
            await _users.DeactivateUserAsync(member.Id, actingUserId: owner.Id);

        // Hiding them from the list is not enough - the id is still valid, and this
        // endpoint would have acted on it.
        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*is a member*");
    }

    public void Dispose() => _context.Dispose();
}
