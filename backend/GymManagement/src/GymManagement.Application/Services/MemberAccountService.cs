using GymManagement.Application.DTOs.Auth;
using GymManagement.Application.DTOs.Member;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Common;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IMemberAccountService
{
    Task<LoginResponse> RegisterAsync(RegisterMemberRequest request, CancellationToken cancellationToken = default);
    Task<MyMembershipDto?> GetMyMembershipAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<PaymentDto>?> GetMyPaymentsAsync(int userId, CancellationToken cancellationToken = default);
    Task<MemberAccountDto> GetAccountForClientAsync(int clientId, CancellationToken cancellationToken = default);
    Task<bool> ResetMemberPasswordAsync(int clientId, ResetMemberPasswordRequest request, int? actingUserId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Logins for members, as opposed to the people who run the gym.
///
/// The rule that shapes everything here is that <b>a member cannot create a membership</b>.
/// The owner adds people at the desk; signing up only ever claims a record that already
/// exists. Free self-signup would fill the member list with strangers, and the member list
/// is what every one of the gym's money reports is built on.
///
/// A member and an administrator are both <see cref="User"/> rows. What separates them is
/// the role and the link: a member's <see cref="Client"/> row points at their user, and an
/// administrator has no client row at all.
/// </summary>
public class MemberAccountService : IMemberAccountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMembershipClock _clock;
    private readonly IAuditService _audit;

    /// <summary>
    /// What a failed match says, whichever half was wrong.
    ///
    /// Saying "no member has that number" would turn this endpoint into a way of asking the
    /// gym who its members are, one number at a time. Naming neither half leaves a wrong
    /// surname and an unknown number indistinguishable from outside.
    /// </summary>
    private const string NoMatchMessage =
        "We could not match that phone number and surname to a membership. Check both, or "
        + "ask the gym to add you - accounts can only be made for members the gym already has.";

    public MemberAccountService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMembershipClock clock,
        IAuditService audit)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _clock = clock;
        _audit = audit;
    }

    public async Task<LoginResponse> RegisterAsync(
        RegisterMemberRequest request, CancellationToken cancellationToken = default)
    {
        var client = await FindMemberAsync(request.PhoneNumber, request.LastName, cancellationToken)
            ?? throw new BusinessException(NoMatchMessage);

        if (client.UserId.HasValue)
        {
            // Safe to be specific: whoever is asking has already produced a matching phone
            // number and surname, so this tells them nothing they did not supply.
            throw new BusinessException(
                "This membership already has an account. Sign in instead, or ask the gym to "
                + "reset the password.");
        }

        var email = request.Email.Trim();

        // Checked against switched-off accounts too. The email is the login, so reusing one
        // would give two rows the same credential.
        var emailTaken = await _unitOfWork.Users.QueryIncludingDeleted()
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailTaken)
        {
            throw new BusinessException(
                $"An account already uses {email}. Sign in, or sign up with another address.");
        }

        var memberRole = await _unitOfWork.Roles.Query()
            .FirstOrDefaultAsync(r => r.Name == Roles.Client, cancellationToken)
            ?? throw new BusinessException("The Client role is missing from this database.");

        var user = new User
        {
            // Taken from the gym's record rather than from the form. The member list is what
            // reception searches, so letting the sign-up form supply a different name would
            // let the name at the desk and the name on the account drift apart.
            FirstName = client.FirstName,
            LastName = client.LastName,
            Email = email,
            PhoneNumber = client.PhoneNumber,
            PasswordHash = _passwordHasher.HashPassword(request.Password)
        };

        user.UserRoles.Add(new UserRole { Role = memberRole });

        await _unitOfWork.Users.AddAsync(user, cancellationToken);

        // Saved before the link is set, because the user row has no id until it is written.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        client.UserId = user.Id;

        await _audit.RecordAsync(
            "Client", client.Id, AuditAction.Created,
            $"{client.FullName} created a member account",
            $"They signed up themselves and now sign in as {email}. Matched to this "
            + "membership by phone number and surname.",
            user.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, new[] { Roles.Client }, cancellationToken);
    }

    /// <summary>
    /// Finds the one member a phone number and surname identify, or null.
    ///
    /// Narrowed by surname in SQL first, then matched on the phone in memory, because
    /// <see cref="PhoneNumberKey"/> is C# and cannot run in the database. This way round
    /// only same-surname rows are ever loaded, while the number is still compared by the
    /// same rule the import and the desk search use - so a member who writes
    /// "+961 3 123 456" where reception wrote "03 123 456" is still recognised.
    ///
    /// Removed members are not matched. Somebody the owner took off the list should not be
    /// able to let themselves back on by signing up.
    /// </summary>
    private async Task<Client?> FindMemberAsync(
        string? phoneNumber, string? lastName, CancellationToken cancellationToken)
    {
        var phoneKey = PhoneNumberKey.Normalize(phoneNumber);
        if (phoneKey == null || string.IsNullOrWhiteSpace(lastName)) return null;

        var surname = lastName.Trim().ToLower();

        var candidates = await _unitOfWork.Clients.Query()
            .Where(c => c.LastName.ToLower() == surname)
            .ToListAsync(cancellationToken);

        var matches = candidates
            .Where(c => PhoneNumberKey.Normalize(c.PhoneNumber) == phoneKey)
            .ToList();

        // Two members sharing a number and a surname - a couple, or a parent who gave their
        // own phone for a child - cannot be told apart from this form. Guessing would
        // attach the account to the wrong person's payment history, so neither is matched
        // and the desk sorts it out.
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<LoginResponse> IssueTokensAsync(
        User user, IEnumerable<string> roles, CancellationToken cancellationToken)
    {
        var roleList = roles.ToList();
        var accessToken = _tokenService.GenerateAccessToken(user, roleList);
        var refreshToken = _tokenService.GenerateRefreshToken();

        refreshToken.UserId = user.Id;
        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            AccessTokenExpiration = DateTime.UtcNow.AddMinutes(15),
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Roles = roleList
            }
        };
    }

    /// <summary>
    /// The member's own membership.
    ///
    /// Returns null when the signed-in user has no member record, which happens once the
    /// owner has removed them - the global filter hides inactive clients. The account still
    /// signs in; there is simply nothing left to show, and the page says so.
    ///
    /// An expired membership is <b>not</b> that case. It comes back normally, carrying a
    /// negative <see cref="MyMembershipDto.DaysRemaining"/>, so the page can say how long
    /// ago it ran out and offer to renew. Locking expired members out would hide that from
    /// exactly the people the gym most wants back.
    /// </summary>
    public async Task<MyMembershipDto?> GetMyMembershipAsync(
        int userId, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.Query()
            .Include(c => c.CurrentPackage)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (client == null) return null;

        var today = _clock.Today;
        var status = client.MembershipStatusOn(today);

        var outstandingCredit = await _unitOfWork.Payments.Query()
            .Where(p => p.ClientId == client.Id)
            .OutstandingCredit()
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        return new MyMembershipDto
        {
            FullName = client.FullName,
            PhoneNumber = client.PhoneNumber,
            Email = client.Email,
            MembershipStatus = status.ToString(),
            IsSuspended = client.IsSuspended,
            DaysRemaining = client.DaysRemaining(today),
            MembershipStartDate = client.MembershipStartDate,
            MembershipEndDate = client.MembershipEndDate,
            CurrentPackageName = client.CurrentPackage?.Name,

            // Expiring counts as entitled to train. Comparing against Active alone would
            // tell a member in their last paid week that they cannot come in.
            CanTrainToday = MembershipStatuses.AllowsEntry(status),
            OutstandingCredit = outstandingCredit
        };
    }

    public async Task<List<PaymentDto>?> GetMyPaymentsAsync(
        int userId, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.Query()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (client == null) return null;

        var payments = await _unitOfWork.Payments.Query()
            .Include(p => p.Package)
            .Where(p => p.ClientId == client.Id)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        return payments.Select(p => new PaymentDto
        {
            Id = p.Id,
            ClientId = p.ClientId,
            ClientName = client.FullName,
            PackageId = p.PackageId,

            // A package the owner has since removed comes back as null from the Include,
            // because the global filter hides it - so this cannot be p.Package.Name.
            PackageName = p.Package?.Name ?? "(removed package)",
            Amount = p.Amount,
            AmountReceived = p.AmountReceived,
            Currency = p.Currency.ToString(),
            ExchangeRate = p.ExchangeRate,
            PaymentDate = p.PaymentDate,
            PaymentMethod = p.PaymentMethod.ToString(),
            Status = p.Status.ToString(),
            PeriodStartDate = p.PeriodStartDate,
            PeriodEndDate = p.PeriodEndDate,
            ReversesPaymentId = p.ReversesPaymentId,
            TransactionReference = p.TransactionReference,
            Notes = p.Notes,
            CreatedAt = p.CreatedAt
        }).ToList();
    }

    /// <summary>Whether this member has a login, for the owner's view of them.</summary>
    public async Task<MemberAccountDto> GetAccountForClientAsync(
        int clientId, CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.QueryIncludingDeleted()
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken)
            ?? throw new NotFoundException("Member", clientId);

        if (!client.UserId.HasValue)
        {
            return new MemberAccountDto { HasAccount = false };
        }

        // Switched-off accounts are included, so the owner sees that a login exists but is
        // currently off rather than being told there is none.
        var user = await _unitOfWork.Users.QueryIncludingDeleted()
            .FirstOrDefaultAsync(u => u.Id == client.UserId.Value, cancellationToken);

        if (user == null)
        {
            return new MemberAccountDto { HasAccount = false };
        }

        return new MemberAccountDto
        {
            HasAccount = true,
            UserId = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive
        };
    }

    /// <summary>
    /// The owner setting a member's password for them - the answer to a member who has
    /// forgotten theirs, since this system has no email to send a reset link with.
    /// </summary>
    public async Task<bool> ResetMemberPasswordAsync(
        int clientId, ResetMemberPasswordRequest request, int? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var client = await _unitOfWork.Clients.Query()
            .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken)
            ?? throw new NotFoundException("Member", clientId);

        if (!client.UserId.HasValue)
        {
            throw new BusinessException(
                $"{client.FullName} has no account yet, so there is no password to reset. "
                + "Members sign up themselves with their phone number and surname.");
        }

        var user = await _unitOfWork.Users.QueryIncludingDeleted()
            .FirstOrDefaultAsync(u => u.Id == client.UserId.Value, cancellationToken);

        if (user == null) return false;

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);

        // Every session this member holds is ended. A reset is either a forgotten password
        // or a suspicion that someone else has it, and in both cases leaving the old
        // sessions minting access tokens defeats the point.
        var now = DateTime.UtcNow;

        var live = await _unitOfWork.RefreshTokens.Query()
            .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in live)
        {
            token.RevokedAt = now;
        }

        await _audit.RecordAsync(
            "Client", client.Id, AuditAction.Updated,
            $"Reset {client.FullName}'s member password",
            $"Set by an administrator, not by {client.FullName}. {live.Count} active "
            + "session(s) were ended, so they must sign in again.",
            actingUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
