namespace GymManagement.Domain.Enums;

/// <summary>
/// How the money actually reached the gym.
///
/// Only the ways The Fit Bear can really be paid. The gym is not a registered business and
/// has no merchant account, so card and bank transfer were removed rather than left sitting
/// in the dropdown - an option reception can pick but the gym cannot honour is a mis-click
/// that puts a payment in the books under a method that never happened.
///
/// Stored as a string, so this list can grow without a migration.
/// </summary>
public enum PaymentMethod
{
    /// <summary>Handed over at the desk. This is what should be in the drawer.</summary>
    Cash,

    /// <summary>
    /// Sent from the member's phone via Whish Money. Real income, but never in the drawer -
    /// the takings report has to keep it separate or the count will never reconcile.
    /// </summary>
    Whish,

    /// <summary>An escape hatch for the desk. Anything here needs a note saying what it was.</summary>
    Other
}
