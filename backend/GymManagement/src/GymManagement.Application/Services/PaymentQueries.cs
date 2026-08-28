using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;

namespace GymManagement.Application.Services;

/// <summary>
/// The one definition of "money a member has put down that has not bought them anything
/// yet", shared by the payment desk and the who-owes-money list.
///
/// Written as a queryable filter rather than a property on <see cref="Payment"/> so the
/// database does the work, and kept in one place because the desk credits a new payment
/// against exactly the rows the report bills for. If those two ever disagreed, the report
/// would chase members for money the desk had already taken.
/// </summary>
public static class PaymentQueries
{
    /// <summary>
    /// Payments still waiting to buy something.
    ///
    /// Three things disqualify a row. It bought a period already (<c>PeriodStartDate</c>).
    /// A later payment finished paying for its package and spent it
    /// (<c>SettledByPaymentId</c>). Or it is not a completed movement at all.
    ///
    /// The fourth condition is the subtle one. A reversal row carries a negative amount and
    /// has no period of its own, so it looks like outstanding credit - but if it reverses a
    /// payment that *did* buy a period, the days were already taken back by winding the
    /// membership down. Counting it here as well would subtract the same money twice and
    /// leave the member owing for a month they had paid off. A reversal only reduces credit
    /// when the payment it cancels was itself outstanding credit.
    /// </summary>
    public static IQueryable<Payment> OutstandingCredit(this IQueryable<Payment> payments) =>
        payments.Where(p =>
            p.Status == TransactionStatus.Completed
            && p.PeriodStartDate == null
            && p.SettledByPaymentId == null
            && (p.ReversesPayment == null || p.ReversesPayment.PeriodStartDate == null));
}
