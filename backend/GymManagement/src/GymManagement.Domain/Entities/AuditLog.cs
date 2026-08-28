using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

/// <summary>
/// Who did what, and when.
///
/// Once more than one person can take money, "who marked this as paid" stops being a
/// question anybody can answer from memory. The trail is written from the first day even
/// though the gym starts with a single login, because an audit trail that begins the day
/// you need it is no use at all.
///
/// Separate from <see cref="PaymentHistory"/>, which carries the amounts a payment moved
/// between. This is the broader record - memberships, deletions, prices, the morning rate -
/// and is what the owner reads. A payment appears in both: the detail there, the fact here.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>What kind of thing was touched: Client, Payment, Package, ExchangeRate.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Which one. Null for an action that covers many rows, like an import.</summary>
    public int? EntityId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>One line the owner can read without knowing the data model.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>What actually changed, when that is worth spelling out.</summary>
    public string? Details { get; set; }

    public int? ActorUserId { get; set; }

    /// <summary>
    /// The actor's name, copied in at the time rather than looked up later.
    ///
    /// Deliberately denormalised: a trail that reads "user #3 deleted this member" after
    /// user 3 has gone answers nothing, and the whole point of the record is to still make
    /// sense long after the fact.
    /// </summary>
    public string? ActorName { get; set; }

    /// <summary>When it happened, in UTC. An instant, not a calendar date.</summary>
    public DateTime OccurredAt { get; set; }
}
