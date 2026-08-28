using GymManagement.Domain.Common;

namespace GymManagement.Domain.Entities;

/// <summary>
/// The LBP-per-USD rate the gym is using on a given day, set by the owner each morning.
///
/// Kept as one row per day rather than a single number that gets overwritten, because the
/// Lebanese rate moves and the owner needs to be able to answer "what were we charging on
/// the 12th" when a member argues about what they handed over. Correcting today's rate
/// leaves yesterday's alone.
///
/// This is only ever a *default* for the payment form. What a payment was actually
/// converted at is stored on the payment itself and never recalculated from here - if the
/// rate moved after the fact, changing this row must not quietly restate money already
/// taken.
/// </summary>
public class ExchangeRate : BaseEntity
{
    /// <summary>
    /// The calendar date this rate applies to, in the gym's own timezone - a date on the
    /// wall calendar, not a UTC instant. See <c>IMembershipClock</c>.
    /// </summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>How many LBP to one USD.</summary>
    public decimal Rate { get; set; }

    /// <summary>Who set it, so a wrong rate can be traced to whoever typed it.</summary>
    public int? SetBy { get; set; }
}
