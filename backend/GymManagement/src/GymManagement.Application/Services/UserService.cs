using GymManagement.Application.DTOs.User;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IUserService
{
    Task<List<UserListDto>> GetUsersAsync(int currentUserId, CancellationToken cancellationToken = default);
    Task<UserListDto> CreateUserAsync(CreateUserRequest request, int? actingUserId = null, CancellationToken cancellationToken = default);
    Task<UserListDto?> UpdateUserAsync(int id, UpdateUserRequest request, int? actingUserId = null, CancellationToken cancellationToken = default);
    Task<bool> DeactivateUserAsync(int id, int actingUserId, CancellationToken cancellationToken = default);
    Task<bool> RestoreUserAsync(int id, int? actingUserId = null, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(int id, ResetUserPasswordRequest request, int? actingUserId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// The people who can sign in and run the gym.
///
/// This exists for one reason above the others: until now there was a single administrator
/// account and no way to make another. A forgotten password meant deleting a row from the
/// database to get a fresh one printed to the console - not something the owner could do,
/// and not something to discover on a Saturday morning.
///
/// So every rule here is about not being locked out. The last administrator who can still
/// sign in cannot be switched off, and nobody can switch off their own account.
///
/// Accounts made here are administrators. A reception role with narrower access is separate
/// work: every endpoint in this system is currently AdminOnly, so a "staff" account would
/// sign in successfully and then be refused by every screen it tried to open.
/// </summary>
public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _audit;

    public UserService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, IAuditService audit)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _audit = audit;
    }

    public async Task<List<UserListDto>> GetUsersAsync(
        int currentUserId, CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.QueryIncludingDeleted()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var adminIds = ActiveAdminIds(users);

        return users.Select(u => new UserListDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            Roles = u.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList(),
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            IsYou = u.Id == currentUserId,
            IsLastAdmin = adminIds.Count == 1 && adminIds.Contains(u.Id)
        }).ToList();
    }

    /// <summary>
    /// Administrators who could still sign in. Deactivated accounts do not count - they
    /// cannot log in, so leaving one behind would not save anyone from a lockout.
    /// </summary>
    private static HashSet<int> ActiveAdminIds(IEnumerable<User> users) =>
        users
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == Roles.Admin))
            .Select(u => u.Id)
            .ToHashSet();

    public async Task<UserListDto> CreateUserAsync(
        CreateUserRequest request, int? actingUserId = null, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();

        // Checked against deleted accounts too. The email is the login, and a deactivated
        // user still holds theirs - reusing it would give two rows the same credential and
        // make restoring the old one ambiguous.
        var taken = await _unitOfWork.Users.QueryIncludingDeleted()
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (taken)
        {
            throw new BusinessException($"An account already uses {email}.");
        }

        var adminRole = await _unitOfWork.Roles.Query()
            .FirstOrDefaultAsync(r => r.Name == Roles.Admin, cancellationToken)
            ?? throw new BusinessException("The Admin role is missing from this database.");

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password)
        };

        user.UserRoles.Add(new UserRole { Role = adminRole });

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, because the row has no id before it and a trail entry that cannot
        // say who it refers to is not worth keeping.
        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Created,
            $"Added administrator {user.FullName}",
            $"Signs in as {user.Email}. The password was set by hand and is not recorded here.",
            actingUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserListDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Roles = new List<string> { Roles.Admin },
            IsActive = true,
            CreatedAt = user.CreatedAt,
            IsYou = user.Id == actingUserId,
            IsLastAdmin = false
        };
    }

    public async Task<UserListDto?> UpdateUserAsync(
        int id, UpdateUserRequest request, int? actingUserId = null, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null) return null;

        var email = request.Email.Trim();

        var taken = await _unitOfWork.Users.QueryIncludingDeleted()
            .AnyAsync(u => u.Email == email && u.Id != id, cancellationToken);

        if (taken)
        {
            throw new BusinessException($"An account already uses {email}.");
        }

        var emailChanged = !string.Equals(user.Email, email, StringComparison.Ordinal);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Updated,
            $"Edited {user.FullName}'s account",
            emailChanged ? $"Sign-in email is now {email}." : null,
            actingUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserListDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Roles = user.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            IsYou = user.Id == actingUserId,
            IsLastAdmin = false
        };
    }

    public async Task<bool> DeactivateUserAsync(
        int id, int actingUserId, CancellationToken cancellationToken = default)
    {
        // Switching off your own account signs you out with no way back in if you are also
        // the only administrator. Refused before anything else is checked.
        if (id == actingUserId)
        {
            throw new BusinessException("You cannot switch off your own account.");
        }

        var users = await _unitOfWork.Users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync(cancellationToken);

        var user = users.FirstOrDefault(u => u.Id == id);
        if (user == null) return false;

        var adminIds = ActiveAdminIds(users);

        if (adminIds.Count == 1 && adminIds.Contains(id))
        {
            throw new BusinessException(
                "This is the only administrator who can still sign in. Add another before "
                + "switching this one off, or nobody will be able to get in.");
        }

        user.IsActive = false;
        user.DeletedAt = DateTime.UtcNow;

        // A deactivated account keeps working until its refresh tokens expire otherwise -
        // the sign-in is blocked but a live session just keeps minting access tokens. This
        // was already found the hard way once, when deleting an old admin left thirty-three
        // usable tokens behind.
        var revoked = await RevokeTokensAsync(id, cancellationToken);

        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Deleted,
            $"Switched off {user.FullName}'s account",
            $"They can no longer sign in. {revoked} active session(s) were ended. "
            + "The account can be switched back on.",
            actingUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreUserAsync(
        int id, int? actingUserId = null, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.QueryIncludingDeleted()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null) return false;

        user.IsActive = true;
        user.DeletedAt = null;

        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Restored,
            $"Switched {user.FullName}'s account back on",
            $"They can sign in again as {user.Email}. Their old password still works.",
            actingUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(
        int id, ResetUserPasswordRequest request, int? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null) return false;

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);

        // Anyone signed in as this person is signed out. A password reset is usually either
        // a forgotten password or a suspicion that someone else has it - in both cases
        // leaving the existing sessions alive defeats the point.
        var revoked = await RevokeTokensAsync(id, cancellationToken);

        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Updated,
            $"Reset {user.FullName}'s password",
            $"Set by an administrator, not by {user.FullName}. {revoked} active session(s) "
            + "were ended, so they must sign in again.",
            actingUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Revokes every refresh token this user still holds, and returns how many. Queued onto
    /// the caller's unit of work so it commits with the change that caused it.
    /// </summary>
    private async Task<int> RevokeTokensAsync(int userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var live = await _unitOfWork.RefreshTokens.Query()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in live)
        {
            token.RevokedAt = now;
        }

        return live.Count;
    }
}
