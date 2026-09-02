using GymManagement.Application.DTOs.Payment;

namespace GymManagement.Application.DTOs.Member;

/// <summary>
/// What someone types to claim the membership the gym already created for them.
///
/// There is no free sign-up: the phone number and surname are matched against a member
/// record the owner made at the desk, so a stranger cannot put themselves in the member
/// list. The surname is asked for as well as the number because the number alone would let
/// anyone discover who is a member by trying numbers until one was accepted.
/// </summary>
public class RegisterMemberRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>The email they will sign in with. Not required to match the gym's record.</summary>
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// A member's own view of their membership. Deliberately not the admin's
/// <c>MemberSummaryDto</c>: this one carries no notes, no audit fields and nothing about
/// anyone else.
/// </summary>
public class MyMembershipDto
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>Worked out from the end date on every request, never stored.</summary>
    public string MembershipStatus { get; set; } = string.Empty;

    public bool IsSuspended { get; set; }

    /// <summary>
    /// Negative once the membership has run out, which is what lets the page say "expired
    /// 12 days ago" rather than just "expired".
    /// </summary>
    public int? DaysRemaining { get; set; }

    public DateTime? MembershipStartDate { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public string? CurrentPackageName { get; set; }

    /// <summary>Whether today's status lets them train. Expiring members still can.</summary>
    public bool CanTrainToday { get; set; }

    /// <summary>Money put down that has not yet bought a period, from the shared definition.</summary>
    public decimal OutstandingCredit { get; set; }
}

/// <summary>Whether a member has a login, for the owner's view of them.</summary>
public class MemberAccountDto
{
    public bool HasAccount { get; set; }
    public int? UserId { get; set; }
    public string? Email { get; set; }
    public DateTime? CreatedAt { get; set; }

    /// <summary>False when the owner has switched the login off from the accounts screen.</summary>
    public bool IsActive { get; set; }
}

/// <summary>An administrator setting a member's password for them.</summary>
public class ResetMemberPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>A member's own payment history, shown to them.</summary>
public class MyPaymentsDto
{
    public List<PaymentDto> Payments { get; set; } = new();
}
