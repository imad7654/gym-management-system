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
/// Accounts made here are administrators or reception. Reception runs the desk - find a
/// member, take a payment, add somebody - and is refused everything that reveals what the
/// gym has earned or could take money back out of the record.
///
/// Members are not managed here even though they are also User rows. Theirs is claimed by
/// matching a phone number and is looked after from their own member page.
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

        // Members are User rows too, since they started being able to sign in, and without
        // this they appeared on a screen whose own subtitle says it is not for them. Worse
        // than untidy: it offered the owner a "switch off" next to a member, which is not
        // what removing a member means and would not have taken them off the member list.
        //
        // A member's login is managed from their own member page, where the rest of what
        // the gym knows about them already is.
        users = users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name is Roles.Admin or Roles.Staff))
            .ToList();

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
    /// Refuses to touch a member's login from the staff accounts screen.
    ///
    /// Members became <see cref="User"/> rows when they started being able to sign in, so
    /// their ids are valid here and every one of these methods would happily act on them.
    /// A member's account belongs to their member page: switching one off from here would
    /// look like removing a member without removing them from the member list, which is a
    /// confusing half-state to leave the gym in.
    /// </summary>
    private static void RefuseIfMember(User user)
    {
        var isStaffAccount = user.UserRoles
            .Any(ur => ur.Role.Name is Roles.Admin or Roles.Staff);

        if (!isStaffAccount)
        {
            throw new BusinessException(
                $"{user.FullName} is a member, not a member of staff. Their account is "
                + "managed from their own member page.");
        }
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

        var roleName = NormaliseRole(request.Role);

        var role = await _unitOfWork.Roles.Query()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
            ?? throw new BusinessException($"The {roleName} role is missing from this database.");

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password)
        };

        user.UserRoles.Add(new UserRole { Role = role });

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, because the row has no id before it and a trail entry that cannot
        // say who it refers to is not worth keeping.
        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Created,
            roleName == Roles.Admin
                ? $"Added administrator {user.FullName}"
                : $"Added reception account for {user.FullName}",
            $"Signs in as {user.Email}. The password was set by hand and is not recorded here.",
            actingUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserListDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Roles = new List<string> { roleName },
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

        RefuseIfMember(user);

        var email = request.Email.Trim();

        var taken = await _unitOfWork.Users.QueryIncludingDeleted()
            .AnyAsync(u => u.Email == email && u.Id != id, cancellationToken);

        if (taken)
        {
            throw new BusinessException($"An account already uses {email}.");
        }

        var emailChanged = !string.Equals(user.Email, email, StringComparison.Ordinal);

        var roleName = NormaliseRole(request.Role);
        var currentRole = user.UserRoles.FirstOrDefault()?.Role.Name;
        var roleChanged = !string.Equals(currentRole, roleName, StringComparison.Ordinal);

        if (roleChanged)
        {
            await ChangeRoleAsync(user, roleName, cancellationToken);
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        var details = new List<string>();
        if (emailChanged) details.Add($"Sign-in email is now {email}.");

        if (roleChanged)
        {
            details.Add(roleName == Roles.Admin
                ? "Promoted to administrator - they can now see revenue, reverse payments "
                  + "and manage accounts."
                : "Changed to reception - they can no longer reverse payments, see revenue "
                  + "history, read the audit trail or change prices.");
        }

        await _audit.RecordAsync(
            "User", user.Id, AuditAction.Updated,
            $"Edited {user.FullName}'s account",
            details.Count > 0 ? string.Join(" ", details) : null,
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

        RefuseIfMember(user);

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
        // Roles are included because the member guard below reads them; without the Include
        // the collection comes back empty and every account looks like a member.
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null) return false;

        RefuseIfMember(user);

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
    /// The two roles this screen can hand out, and nothing else.
    ///
    /// Trainer and Client also exist in the database. Neither belongs here: a member gets
    /// their account by matching their own phone number, and handing one out from the
    /// accounts screen would put a login on a person with no member record behind it.
    /// </summary>
    private static string NormaliseRole(string? role) =>
        string.Equals(role?.Trim(), Roles.Staff, StringComparison.OrdinalIgnoreCase)
            ? Roles.Staff
            : Roles.Admin;

    /// <summary>
    /// Moves an account between administrator and reception.
    ///
    /// Demoting the last administrator is refused for exactly the reason switching them off
    /// is: reception cannot reach the accounts screen, so there would be nobody left able to
    /// put it back. That is the same lockout by a different door, and it is easy to walk
    /// into while tidying up who has what.
    /// </summary>
    private async Task ChangeRoleAsync(
        User user, string roleName, CancellationToken cancellationToken)
    {
        if (roleName != Roles.Admin)
        {
            var everyone = await _unitOfWork.Users.Query()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ToListAsync(cancellationToken);

            var adminIds = ActiveAdminIds(everyone);

            if (adminIds.Count == 1 && adminIds.Contains(user.Id))
            {
                throw new BusinessException(
                    "This is the only administrator who can still sign in. Making them "
                    + "reception would leave nobody able to change it back. Add another "
                    + "administrator first.");
            }
        }

        var role = await _unitOfWork.Roles.Query()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
            ?? throw new BusinessException($"The {roleName} role is missing from this database.");

        user.UserRoles.Clear();
        user.UserRoles.Add(new UserRole { Role = role });

        // A live session carries the old role in its access token until it expires, so a
        // demoted administrator would keep administrator screens for up to fifteen minutes.
        // Ending the sessions makes the change take effect at once.
        await RevokeTokensAsync(user.Id, cancellationToken);
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
