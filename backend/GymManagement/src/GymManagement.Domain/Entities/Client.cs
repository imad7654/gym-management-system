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

    /// <summary>
    /// Paused by the owner for travel or injury. The only part of the membership status a
    /// person sets by hand, and therefore the only part worth storing - every other value
    /// is worked out from <see cref="MembershipEndDate"/> whenever it is asked for.
    /// </summary>
    public bool IsSuspended { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// The login this member uses to see their own membership, or null if they have never
    /// signed up. Optional on purpose: the owner creates the member record at the desk, and
    /// most members will never make an account at all.
    ///
    /// The link lives here rather than as a ClientId on User because a login is something a
    /// member may acquire, while an administrator has no member record at all - putting it
    /// on User would leave that column null for every account that runs the gym.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// When somebody last rang this member about renewing, or null if nobody has.
    ///
    /// A UTC instant rather than a flag, because the question is always "have we called
    /// them <em>today</em>" - a boolean would have to be reset by a nightly job, and the
    /// last job this system relied on was never written and left every membership reading
    /// Active forever.
    ///
    /// Deliberately not cleared when they renew. The chase list drops them once their dates
    /// move, so a stale value simply stops being asked about, and keeping it means the next
    /// time they lapse the desk can see they were called last time too.
    /// </summary>
    public DateTime? LastChasedAt { get; set; }

    // Soft Delete
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Package? CurrentPackage { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<PaymentHistory> PaymentHistories { get; set; } = new List<PaymentHistory>();

    /// <summary>Days remaining on the membership, or null if they have never paid.</summary>
    public int? DaysRemaining(DateOnly today) =>
        MembershipEndDate.HasValue
            ? DateOnly.FromDateTime(MembershipEndDate.Value).DayNumber - today.DayNumber
            : null;

    /// <summary>
    /// The member's status on a given day, worked out from the end date every time it is
    /// asked for.
    ///
    /// This used to be a stored column kept up to date by a nightly job. The job was never
    /// written, so an expired member went on reading Active indefinitely - the door would
    /// have let them in and every count of "active members" was wrong. Deriving it removes
    /// the possibility entirely: there is no copy left to go stale.
    ///
    /// Takes today as an argument rather than reading the clock, so the caller decides
    /// which calendar "today" means - the gym's, not the server's - and so this is
    /// testable without freezing time.
    /// </summary>
    public MembershipStatus MembershipStatusOn(DateOnly today) =>
        StatusFrom(IsSuspended, MembershipStartDate, MembershipEndDate, today);

    /// <summary>
    /// The single definition of the status rule, shared by the entity and by
    /// <c>ClientQueries</c>, which rewrites the same logic as SQL so the database can
    /// filter and sort on it. Any change here has to be mirrored there.
    /// </summary>
    public static MembershipStatus StatusFrom(
        bool isSuspended, DateTime? startDate, DateTime? endDate, DateOnly today)
    {
        // A freeze is the one thing a person sets deliberately, so it wins over the dates.
        if (isSuspended) return MembershipStatus.Suspended;

        if (!startDate.HasValue || !endDate.HasValue) return MembershipStatus.Pending;

        var start = DateOnly.FromDateTime(startDate.Value);
        var end = DateOnly.FromDateTime(endDate.Value);

        if (today < start) return MembershipStatus.Pending;
        if (today > end) return MembershipStatus.Expired;

        return end.DayNumber - today.DayNumber <= ExpiringWindowDays
            ? MembershipStatus.Expiring
            : MembershipStatus.Active;
    }

    /// <summary>Pauses the membership for travel or injury. The dates are left untouched.</summary>
    public void Suspend() => IsSuspended = true;

    /// <summary>
    /// Lifts a pause. The status goes straight back to whatever the dates say, with no
    /// recalculation step needed - which is the point of deriving it.
    /// </summary>
    public void Resume() => IsSuspended = false;

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

        // Both dates are inclusive, so the last day is start + duration - 1. Using
        // start + duration would give a 30-day package 31 days of access, and - worse -
        // would mean each payment adds duration + 1 days while WindBackMembership takes
        // only duration back, so every reversal would leave a free day behind.
        var end = start.AddDays(package.DurationDays - 1);

        CurrentPackageId = package.Id;
        MembershipStartDate ??= start.ToDateTime(TimeOnly.MinValue);
        MembershipEndDate = end.ToDateTime(TimeOnly.MinValue);
        PaymentStatus = PaymentStatus.Paid;

        // Paying lifts a freeze. Someone who paused for travel and has now renewed at the
        // desk is back, and leaving them frozen would turn them away at the door. This
        // matches what the old stored-status code did, which set Active before
        // recalculating and so cleared a suspension as a side effect.
        IsSuspended = false;

        return new MembershipPeriod(start, end);
    }

    /// <summary>
    /// Takes back the days a reversed payment had bought.
    ///
    /// Subtracts the duration rather than resetting to the reversed payment's start date,
    /// because the member may have renewed again since. Chopping back to that older date
    /// would silently delete the days a later, unrelated payment paid for.
    ///
    /// The join date is left alone: reversing a renewal does not unjoin anyone. So is the
    /// freeze - a reversal leaves a frozen member frozen.
    ///
    /// There is no status to put right afterwards any more. Moving the end date is the
    /// whole operation, and the status follows from it the next time anything asks.
    /// </summary>
    public void WindBackMembership(int days)
    {
        if (!MembershipEndDate.HasValue) return;

        MembershipEndDate = DateOnly.FromDateTime(MembershipEndDate.Value)
            .AddDays(-days)
            .ToDateTime(TimeOnly.MinValue);
    }

    public void SoftDelete()
    {
        IsActive = false;
        DeletedAt = DateTime.UtcNow;

        // Deliberately does not freeze the member. Removal and freezing are different
        // things, and conflating them used to leave a restored client stuck as Suspended.
    }

    public void Restore()
    {
        IsActive = true;
        DeletedAt = null;
    }
}

/// <summary>The span a single payment bought, both dates inclusive.</summary>
public readonly record struct MembershipPeriod(DateOnly Start, DateOnly End);
