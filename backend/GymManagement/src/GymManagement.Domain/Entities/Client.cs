using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class Client : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// How close to the end date a membership starts warning. Drives both the
    /// <see cref="MembershipStatus.Expiring"/> status and the reminder the door shows.
    /// </summary>
    public const int ExpiringWindowDays = 7;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Notes { get; set; }

    // Membership Info
    public int? CurrentPackageId { get; set; }

    /// <summary>
    /// When this member first joined. Set once, on their first payment, and never moved
    /// again - otherwise the gym loses the record of when each member actually joined.
    /// </summary>
    public DateTime? MembershipStartDate { get; set; }

    /// <summary>
    /// Last day the membership is valid, inclusive. A calendar date in the gym's own
    /// timezone, not a UTC instant - see the date handling in MembershipClock.
    /// </summary>
    public DateTime? MembershipEndDate { get; set; }

    public MembershipStatus MembershipStatus { get; set; } = MembershipStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    // Soft Delete
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual Package? CurrentPackage { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<PaymentHistory> PaymentHistories { get; set; } = new List<PaymentHistory>();

    /// <summary>Days remaining on the membership, or null if they have never paid.</summary>
    public int? DaysRemaining(DateOnly today) =>
        MembershipEndDate.HasValue
            ? DateOnly.FromDateTime(MembershipEndDate.Value).DayNumber - today.DayNumber
            : null;

    /// <summary>
    /// Recalculates the status from the end date. Takes today as an argument rather than
    /// reading the clock itself, so the caller decides which calendar "today" means - the
    /// gym's, not the server's - and so this is testable without freezing time.
    /// </summary>
    public void UpdateMembershipStatus(DateOnly today)
    {
        // A freeze is the one status a person sets deliberately. The nightly recalculation
        // has to leave it alone, or a frozen member silently becomes Active or Expired.
        if (MembershipStatus == MembershipStatus.Suspended) return;

        if (!MembershipStartDate.HasValue || !MembershipEndDate.HasValue)
        {
            MembershipStatus = MembershipStatus.Pending;
            return;
        }

        var start = DateOnly.FromDateTime(MembershipStartDate.Value);
        var end = DateOnly.FromDateTime(MembershipEndDate.Value);

        if (today < start)
        {
            MembershipStatus = MembershipStatus.Pending;
        }
        else if (today > end)
        {
            MembershipStatus = MembershipStatus.Expired;
        }
        else
        {
            var daysLeft = end.DayNumber - today.DayNumber;
            MembershipStatus = daysLeft <= ExpiringWindowDays
                ? MembershipStatus.Expiring
                : MembershipStatus.Active;
        }
    }

    /// <summary>
    /// Moves the membership forward by one package duration and returns the period bought.
    /// Called by both the desk payment flow and the online renewal so the two can never
    /// drift apart.
    ///
    /// Renewing early does not cost the member the days they already paid for: the new
    /// period starts the day after the current end date rather than today.
    /// </summary>
    public MembershipPeriod ExtendMembership(Package package, DateOnly today)
    {
        var currentEnd = MembershipEndDate.HasValue
            ? DateOnly.FromDateTime(MembershipEndDate.Value)
            : (DateOnly?)null;

        var start = currentEnd.HasValue && currentEnd.Value >= today
            ? currentEnd.Value.AddDays(1)
            : today;

        // End date is inclusive, so a 30-day package bought today runs through today+30.
        var end = start.AddDays(package.DurationDays);

        CurrentPackageId = package.Id;
        MembershipStartDate ??= start.ToDateTime(TimeOnly.MinValue);
        MembershipEndDate = end.ToDateTime(TimeOnly.MinValue);
        PaymentStatus = PaymentStatus.Paid;
        MembershipStatus = MembershipStatus.Active;
        UpdateMembershipStatus(today);

        return new MembershipPeriod(start, end);
    }

    /// <summary>
    /// Takes back the days a reversed payment had bought.
    ///
    /// Subtracts the duration rather than resetting to the reversed payment's start date,
    /// because the member may have renewed again since. Chopping back to that older date
    /// would silently delete the days a later, unrelated payment paid for.
    ///
    /// The join date is left alone: reversing a renewal does not unjoin anyone.
    /// </summary>
    public void WindBackMembership(int days, DateOnly today)
    {
        if (!MembershipEndDate.HasValue) return;

        MembershipEndDate = DateOnly.FromDateTime(MembershipEndDate.Value)
            .AddDays(-days)
            .ToDateTime(TimeOnly.MinValue);

        // Suspended would otherwise block the recalculation, and a reversal should leave a
        // frozen member frozen.
        if (MembershipStatus != MembershipStatus.Suspended)
        {
            UpdateMembershipStatus(today);
        }
    }

    public void SoftDelete()
    {
        IsActive = false;
        DeletedAt = DateTime.UtcNow;

        // Deliberately does not touch MembershipStatus. Removal and freezing are different
        // things: marking a deleted client Suspended made Restore() unable to recalculate
        // their status, because Suspended is skipped by design.
    }

    public void Restore(DateOnly today)
    {
        IsActive = true;
        DeletedAt = null;
        UpdateMembershipStatus(today);
    }
}

/// <summary>The span a single payment bought, both dates inclusive.</summary>
public readonly record struct MembershipPeriod(DateOnly Start, DateOnly End);
