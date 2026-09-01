using GymManagement.Application.DTOs.Common;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GymManagement.Infrastructure.Data.Seeders;

/// <summary>
/// Fills an empty database with a gym that looks like it has been running for months.
///
/// This exists because every screen that proves this system is worth anything - the daily
/// takings, who owes money, the expiring list, the dashboard - shows nothing at all until
/// there is history behind it. A demo with two members and no payments reads as unfinished
/// software rather than as a finished product with an empty gym.
///
/// **The payments are taken through the real <see cref="PaymentService"/>, not written as
/// rows.** That is the whole design of this file. Hand-written payment rows would have to
/// reproduce the period stamping, the part-payment credit ledger and the settlement markers
/// by hand, and any small mistake would produce a database the application's own reports
/// disagree with - the demo would show numbers that cannot happen. Driving the real desk
/// flow through a clock that starts months ago means the history is generated exactly the
/// way a real gym would have generated it, so every report necessarily adds up.
///
/// Everything here is behind Seed:DemoData and only touches a database with no members in
/// it, so it can never appear underneath a real gym's records.
/// </summary>
internal static class DemoGymSeeder
{
    /// <summary>How far back the demo history reaches. Enough for the revenue chart to have a shape.</summary>
    private const int HistoryDays = 120;

    /// <summary>LBP per USD. Roughly the rate Lebanon settled at, so the figures read as plausible.</summary>
    private const decimal LbpRate = 89_000m;

    /// <summary>
    /// Fixed seed, so every clone of this repository demos with the identical gym. A demo
    /// that reshuffles itself on each rebuild makes screenshots and sales notes worthless.
    /// </summary>
    private const int RandomSeed = 20260901;

    private sealed record Member(
        string First,
        string Last,
        string Phone,
        Gender Gender,
        /// <summary>Which seeded package they buy, by index.</summary>
        int PackageIndex,
        /// <summary>How the story ends. Drives what the reports have to show.</summary>
        MemberStory Story);

    private enum MemberStory
    {
        /// <summary>Paid up with weeks left. The ordinary case, and most of the list.</summary>
        Active,
        /// <summary>Ends within the warning window, so the expiring list and dashboard have names on them.</summary>
        ExpiringSoon,
        /// <summary>Lapsed a while ago. The people a gym actually wants to chase.</summary>
        Expired,
        /// <summary>Part-paid and still short, so who-owes-money has rows.</summary>
        OwesMoney,
        /// <summary>Frozen by hand for travel or injury.</summary>
        Frozen,
        /// <summary>On the books, never paid. Shows what Pending looks like.</summary>
        NeverPaid,
        /// <summary>Paid, then the payment was reversed - so corrections appear in the takings.</summary>
        Refunded
    }

    /// <summary>
    /// A clock the seeder winds forward, so payments land on the dates their story needs
    /// rather than all at startup. <see cref="PaymentService"/> reads both the instant and
    /// the gym's today from here, which is what makes the generated history internally
    /// consistent.
    /// </summary>
    private sealed class SeedClock : IMembershipClock
    {
        private readonly TimeZoneInfo _zone = ResolveBeirut();
        public DateOnly Today { get; set; }

        /// <summary>Hour of the day the payment was taken. Varied so the takings do not all share a timestamp.</summary>
        public int Hour { get; set; } = 17;

        private static TimeZoneInfo ResolveBeirut()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut"); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }

        public DateTime UtcNow => ToUtc(Today.ToDateTime(new TimeOnly(Hour, 0)));

        public (DateTime StartUtc, DateTime EndUtc) DayBoundsUtc(DateOnly date) =>
            (ToUtc(date.ToDateTime(TimeOnly.MinValue)), ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue)));

        private DateTime ToUtc(DateTime local)
        {
            if (_zone.IsInvalidTime(local)) local = local.AddHours(1);
            return TimeZoneInfo.ConvertTimeToUtc(local, _zone);
        }
    }

    /// <summary>
    /// Swallows the audit entries the seeded payments would otherwise generate.
    ///
    /// The trail is meant to answer "who did this". Nobody did these - they are scaffolding
    /// - and filling the history screen with hundreds of entries attributed to no one would
    /// make the real entries impossible to find, in the one screen whose whole value is
    /// being trustworthy.
    /// </summary>
    private sealed class SeedAudit : IAuditService
    {
        public Task RecordAsync(
            string entityType, int? entityId, AuditAction action, string summary,
            string? details = null, int? actorUserId = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PaginatedResult<AuditEntryDto>> GetEntriesAsync(
            AuditQueryParameters parameters, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The seeder never reads the audit trail.");
    }

    public static async Task SeedAsync(
        ApplicationDbContext context, IMembershipClock realClock, ILogger logger)
    {
        // Only ever fills an empty gym. A database with members in it is somebody's real
        // data, demo flag or not.
        if (await context.Clients.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var packages = await context.Packages.IgnoreQueryFilters()
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
            .ToListAsync();

        if (packages.Count == 0)
        {
            logger.LogWarning("Demo members were not seeded: there are no packages to sell them.");
            return;
        }

        var today = realClock.Today;
        var clock = new SeedClock { Today = today };
        var unitOfWork = new UnitOfWork(context);
        var payments = new PaymentService(unitOfWork, clock, new SeedAudit());
        var random = new Random(RandomSeed);

        await SeedExchangeRatesAsync(context, today);

        var created = 0;
        var taken = 0;

        foreach (var member in Roster())
        {
            var package = packages[Math.Min(member.PackageIndex, packages.Count - 1)];

            var client = new Client
            {
                FirstName = member.First,
                LastName = member.Last,
                PhoneNumber = member.Phone,
                Email = $"{member.First.ToLowerInvariant()}.{member.Last.ToLowerInvariant()}@example.com",
                Gender = member.Gender,
                DateOfBirth = new DateTime(1985 + random.Next(0, 20), random.Next(1, 13), random.Next(1, 28)),
                // Joined some time before their first payment, the way a real record would.
                CreatedAt = today.AddDays(-random.Next(30, HistoryDays)).ToDateTime(TimeOnly.MinValue)
            };

            context.Clients.Add(client);
            await context.SaveChangesAsync();
            created++;

            taken += await PlayOutStoryAsync(
                member, client, package, payments, context, clock, random, today);
        }

        logger.LogInformation(
            "Seeded a demo gym: {Members} members and {Payments} payments across the last {Days} days",
            created, taken, HistoryDays);
    }

    /// <summary>
    /// Takes the payments this member's story needs, on the dates it needs them, through the
    /// real desk flow.
    /// </summary>
    private static async Task<int> PlayOutStoryAsync(
        Member member, Client client, Package package, PaymentService payments,
        ApplicationDbContext context, SeedClock clock, Random random, DateOnly today)
    {
        // Where their current period should end, worked back from the story. Renewals are
        // then walked forwards from there so the dates line up on their own.
        //
        // Days remaining are capped at the package length, because a member cannot have
        // more time left than the thing they bought lasts. Without the cap, a 30-day
        // package with 50 days still to run put the first payment in the future, the loop
        // below refused to date anything ahead of today, and the member ended up with no
        // payments at all - reading as "never paid" while the roster called them active.
        var mostDaysLeft = Math.Max(Client.ExpiringWindowDays + 2, package.DurationDays);

        int daysUntilEnd = member.Story switch
        {
            MemberStory.Active => random.Next(Client.ExpiringWindowDays + 1, mostDaysLeft),
            MemberStory.ExpiringSoon => random.Next(1, Client.ExpiringWindowDays),
            MemberStory.Expired => -random.Next(5, 70),
            MemberStory.Frozen => random.Next(Client.ExpiringWindowDays + 1, mostDaysLeft),
            MemberStory.Refunded => random.Next(Client.ExpiringWindowDays + 1, mostDaysLeft),
            _ => 0
        };

        var count = 0;

        if (member.Story == MemberStory.NeverPaid)
        {
            // No payment at all. Their status derives to Pending from having no dates.
            return count;
        }

        if (member.Story == MemberStory.OwesMoney)
        {
            // Short of the price, so the membership does not move and they land on the
            // who-owes-money report. A second part payment for some of them, because a
            // member paying twice and still owing is the case most worth demonstrating.
            var firstDay = today.AddDays(-random.Next(6, 40));
            var portion = Math.Round(package.Price * (decimal)(0.25 + random.NextDouble() * 0.3), 2);

            count += await TakeAsync(payments, clock, client, package, firstDay, portion, random);

            if (random.Next(2) == 0)
            {
                var secondDay = firstDay.AddDays(random.Next(2, 10));
                if (secondDay <= today)
                {
                    var second = Math.Round(package.Price * (decimal)(0.15 + random.NextDouble() * 0.2), 2);
                    count += await TakeAsync(payments, clock, client, package, secondDay, second, random);
                }
            }

            return count;
        }

        // Everyone else: a run of full renewals ending where the story says, so their
        // history reads as a member who has been coming for a while.
        var renewals = random.Next(1, 4);
        var periodEnd = today.AddDays(daysUntilEnd);
        var firstPeriodStart = periodEnd.AddDays(-(package.DurationDays * renewals) + 1);

        for (var i = 0; i < renewals; i++)
        {
            var payDay = firstPeriodStart.AddDays(package.DurationDays * i);

            // Nothing may be dated in the future - a payment the gym has not taken yet
            // would show up in reports as money it does not have.
            if (payDay > today) break;

            count += await TakeAsync(payments, clock, client, package, payDay, package.Price, random);
        }

        // A member whose story says they have paid must end up with a payment. The date
        // arithmetic above is the kind that goes quietly wrong, and a "frozen" member who
        // never paid for anything is a nonsense the demo should not be able to show.
        if (count == 0)
        {
            count += await TakeAsync(payments, clock, client, package, today, package.Price, random);
        }

        if (member.Story == MemberStory.Frozen)
        {
            // Set directly: freezing is a person's decision, not something a payment causes.
            client.IsSuspended = true;
            await context.SaveChangesAsync();
        }

        if (member.Story == MemberStory.Refunded)
        {
            var last = await context.Payments
                .Where(p => p.ClientId == client.Id && p.ReversesPaymentId == null)
                .OrderByDescending(p => p.PaymentDate)
                .FirstOrDefaultAsync();

            if (last != null)
            {
                clock.Today = today.AddDays(-random.Next(0, 4));
                await payments.ReversePaymentAsync(
                    last.Id, "Paid for the wrong package at the desk.");
                count++;
            }
        }

        return count;
    }

    private static async Task<int> TakeAsync(
        PaymentService payments, SeedClock clock, Client client, Package package,
        DateOnly day, decimal amountUsd, Random random)
    {
        clock.Today = day;
        clock.Hour = random.Next(9, 21);

        // A minority pay by Whish, and a few of those in LBP, so the takings report has its
        // cash / Whish split and its currency conversion to show.
        var roll = random.Next(100);
        var method = roll < 70 ? PaymentMethod.Cash : roll < 95 ? PaymentMethod.Whish : PaymentMethod.Other;
        var payInLbp = method == PaymentMethod.Cash && random.Next(100) < 20;

        await payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = client.Id,
            PackageId = package.Id,
            AmountReceived = payInLbp ? Math.Round(amountUsd * LbpRate, 0) : amountUsd,
            Currency = payInLbp ? Currency.Lbp : Currency.Usd,
            ExchangeRate = payInLbp ? LbpRate : null,
            PaymentMethod = method,
            TransactionReference = method == PaymentMethod.Whish
                ? $"WM{random.Next(100000, 999999)}"
                : null
        });

        return 1;
    }

    /// <summary>
    /// A fortnight of daily rates, so the settings screen and the payment form have a
    /// history behind them rather than one lonely row.
    /// </summary>
    private static async Task SeedExchangeRatesAsync(ApplicationDbContext context, DateOnly today)
    {
        if (await context.ExchangeRates.AnyAsync()) return;

        for (var back = 14; back >= 0; back--)
        {
            context.ExchangeRates.Add(new ExchangeRate
            {
                EffectiveDate = today.AddDays(-back).ToDateTime(TimeOnly.MinValue),
                Rate = LbpRate
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Lebanese names, because the demo is shown to Lebanese gym owners and placeholder
    /// names read as a template rather than as a working business.
    /// </summary>
    private static IEnumerable<Member> Roster() =>
    [
        new("Rami", "Khoury", "03 111 201", Gender.Male, 1, MemberStory.Active),
        new("Nour", "Haddad", "70 111 202", Gender.Female, 1, MemberStory.Active),
        new("Karim", "Nassar", "03 111 203", Gender.Male, 2, MemberStory.Active),
        new("Layal", "Aoun", "71 111 204", Gender.Female, 0, MemberStory.Active),
        new("Elie", "Gerges", "03 111 205", Gender.Male, 1, MemberStory.Active),
        new("Maya", "Sfeir", "76 111 206", Gender.Female, 1, MemberStory.Active),
        new("Tarek", "Chahine", "03 111 207", Gender.Male, 0, MemberStory.Active),
        new("Rita", "Karam", "70 111 208", Gender.Female, 2, MemberStory.Active),
        new("Joseph", "Rizk", "03 111 209", Gender.Male, 1, MemberStory.Active),
        new("Dana", "Mansour", "71 111 210", Gender.Female, 1, MemberStory.Active),
        new("Ziad", "Abou Jaoude", "03 111 211", Gender.Male, 1, MemberStory.Active),
        new("Hala", "Semaan", "76 111 212", Gender.Female, 0, MemberStory.Active),

        new("Georges", "Matar", "03 111 213", Gender.Male, 1, MemberStory.ExpiringSoon),
        new("Yara", "Fares", "70 111 214", Gender.Female, 1, MemberStory.ExpiringSoon),
        new("Marwan", "Daher", "03 111 215", Gender.Male, 2, MemberStory.ExpiringSoon),
        new("Sara", "Bou Khalil", "71 111 216", Gender.Female, 0, MemberStory.ExpiringSoon),

        new("Fadi", "Younes", "03 111 217", Gender.Male, 1, MemberStory.Expired),
        new("Lea", "Zeidan", "70 111 218", Gender.Female, 1, MemberStory.Expired),
        new("Bilal", "Hamdan", "03 111 219", Gender.Male, 0, MemberStory.Expired),
        new("Christelle", "Abi Nader", "76 111 220", Gender.Female, 1, MemberStory.Expired),
        new("Omar", "Sleiman", "03 111 221", Gender.Male, 2, MemberStory.Expired),

        new("Nadine", "Tannous", "70 111 222", Gender.Female, 1, MemberStory.OwesMoney),
        new("Charbel", "Saad", "03 111 223", Gender.Male, 1, MemberStory.OwesMoney),
        new("Reem", "Ghanem", "71 111 224", Gender.Female, 2, MemberStory.OwesMoney),
        new("Hadi", "Barakat", "03 111 225", Gender.Male, 0, MemberStory.OwesMoney),

        new("Jad", "Moukarzel", "03 111 226", Gender.Male, 1, MemberStory.Frozen),
        new("Carine", "Estephan", "70 111 227", Gender.Female, 1, MemberStory.Frozen),

        new("Wissam", "Kassem", "03 111 228", Gender.Male, 1, MemberStory.NeverPaid),
        new("Tala", "Harb", "71 111 229", Gender.Female, 0, MemberStory.NeverPaid),

        new("Antoine", "Chidiac", "03 111 230", Gender.Male, 1, MemberStory.Refunded),
    ];
}
