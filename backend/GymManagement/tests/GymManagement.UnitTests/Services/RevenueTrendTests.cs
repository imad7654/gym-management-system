using FluentAssertions;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// Revenue month by month, and one month opened up.
///
/// The rule this chart lives or dies by is that it counts the same money the rest of the
/// system counts. A chart that disagreed with the daily takings report about March would
/// undermine the one thing that makes the whole system worth having, so most of what is
/// pinned here is agreement rather than arithmetic.
/// </summary>
public class RevenueTrendTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 2);
    private const int BeirutOffsetHours = 3;

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly FixedClock _clock;
    private readonly ReportService _reports;

    public RevenueTrendTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"revenue-trend-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _clock = new FixedClock(Today, BeirutOffsetHours);
        _reports = new ReportService(_unitOfWork, _clock);
    }

    private async Task<Package> SeedPackageAsync(decimal price = 50m, int durationDays = 30)
    {
        var package = new Package { Name = "Monthly", Price = price, DurationDays = durationDays };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();
        return package;
    }

    /// <summary>
    /// A member with an explicit join date.
    ///
    /// Deliberately not derived from the end date: a member ending in December would then
    /// have "joined" in December, and every month before that would correctly show them as
    /// not yet a member - which looks like a bug in the count and is not one.
    /// </summary>
    private async Task<Client> SeedMemberAsync(
        string name = "Rita", DateOnly? endsOn = null, DateOnly? joinedOn = null)
    {
        var client = new Client
        {
            FirstName = name,
            LastName = "Member",
            PhoneNumber = "03 111 000",
            MembershipStartDate = endsOn is null
                ? null
                : (joinedOn ?? new DateOnly(2026, 1, 1)).ToDateTime(TimeOnly.MinValue),
            MembershipEndDate = endsOn?.ToDateTime(TimeOnly.MinValue)
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    /// <summary>A completed payment written straight in, at a chosen instant.</summary>
    private async Task SeedPaymentAsync(
        Client client,
        Package package,
        decimal amountUsd,
        DateTime takenAtUtc,
        PaymentMethod method = PaymentMethod.Cash,
        bool isReversalOf = false,
        int? reversesPaymentId = null)
    {
        _context.Payments.Add(new Payment
        {
            ClientId = client.Id,
            PackageId = package.Id,
            Amount = amountUsd,
            AmountReceived = amountUsd,
            Currency = Currency.Usd,
            PaymentMethod = method,
            Status = TransactionStatus.Completed,
            PaymentDate = takenAtUtc,
            ReversesPaymentId = reversesPaymentId,
            PeriodStartDate = isReversalOf || reversesPaymentId != null
                ? null
                : takenAtUtc
        });

        await _context.SaveChangesAsync();
    }

    /// <summary>An instant that is the given gym date and hour in Beirut.</summary>
    private static DateTime GymInstant(int year, int month, int day, int hour = 12) =>
        new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc)
            .AddHours(-BeirutOffsetHours);

    [Fact]
    public async Task Trend_ReturnsAMonthPerWindowEndingWithThisMonth()
    {
        var trend = await _reports.GetRevenueTrendAsync(months: 6);

        trend.Months.Should().HaveCount(6);

        // Oldest first, so it plots left to right without the chart having to sort it.
        trend.Months.First().Label.Should().Be("Apr 2026");
        trend.Months.Last().Label.Should().Be("Sep 2026");
    }

    [Fact]
    public async Task Trend_CountsAPaymentInTheMonthItWasTaken_WholeAndNotSpread()
    {
        var package = await SeedPackageAsync(price: 150m, durationDays: 90);
        var member = await SeedMemberAsync(endsOn: Today.AddDays(60));

        // Three months of membership, bought in July.
        await SeedPaymentAsync(member, package, 150m, GymInstant(2026, 7, 10));

        var trend = await _reports.GetRevenueTrendAsync(months: 4);

        var july = trend.Months.Single(m => m.Label == "Jul 2026");
        var august = trend.Months.Single(m => m.Label == "Aug 2026");

        // Cash in, deliberately. Spreading it would be reasonable accounting and would
        // leave this chart disagreeing with the drawer, the takings report and the bank.
        july.TotalUsd.Should().Be(150m);
        august.TotalUsd.Should().Be(0m);
    }

    [Fact]
    public async Task Trend_UsesTheGymsMonthBoundaryNotUtc()
    {
        var package = await SeedPackageAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(30));

        // 11pm on 31 August in Beirut. In UTC that is 20:00 on the 31st - same month here,
        // but the reverse case is the one that bites: half past midnight on 1 September in
        // Beirut is 21:30 on 31 August in UTC, and a naive grouping files it under August.
        await SeedPaymentAsync(member, package, 40m, GymInstant(2026, 8, 31, 23));
        await SeedPaymentAsync(member, package, 60m, GymInstant(2026, 9, 1, 0));

        var trend = await _reports.GetRevenueTrendAsync(months: 3);

        trend.Months.Single(m => m.Label == "Aug 2026").TotalUsd.Should().Be(40m);
        trend.Months.Single(m => m.Label == "Sep 2026").TotalUsd.Should().Be(60m,
            "half past midnight in Beirut is September, whatever UTC calls it");
    }

    [Fact]
    public async Task Trend_KeepsWhishOutOfTheDrawerFigureButInTheTotal()
    {
        var package = await SeedPackageAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(30));

        await SeedPaymentAsync(member, package, 30m, GymInstant(2026, 9, 1), PaymentMethod.Cash);
        await SeedPaymentAsync(member, package, 70m, GymInstant(2026, 9, 1), PaymentMethod.Whish);

        var september = (await _reports.GetRevenueTrendAsync(months: 2))
            .Months.Single(m => m.Label == "Sep 2026");

        september.DrawerUsd.Should().Be(30m, "Whish money never touched the till");
        september.WhishUsd.Should().Be(70m);
        september.TotalUsd.Should().Be(100m);
    }

    [Fact]
    public async Task Trend_NetsOffARefundInTheMonthItWasGivenBack()
    {
        var package = await SeedPackageAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(30));

        await SeedPaymentAsync(member, package, 50m, GymInstant(2026, 8, 10));
        var original = await _context.Payments.FirstAsync();

        // Handed back the following month, which is when the drawer actually lost it.
        await SeedPaymentAsync(
            member, package, -50m, GymInstant(2026, 9, 1),
            reversesPaymentId: original.Id);

        var trend = await _reports.GetRevenueTrendAsync(months: 3);

        trend.Months.Single(m => m.Label == "Aug 2026").TotalUsd.Should().Be(50m);

        var september = trend.Months.Single(m => m.Label == "Sep 2026");
        september.TotalUsd.Should().Be(-50m);
        september.ReversalCount.Should().Be(1);
        september.PaymentCount.Should().Be(0, "a reversal is a correction, not a sale");
    }

    [Fact]
    public async Task Trend_CountsMembersWhoseMembershipCoveredTheEndOfTheMonth()
    {
        // Ends mid-August, so covers the end of July but not the end of August.
        await SeedMemberAsync("Lapsing", endsOn: new DateOnly(2026, 8, 15));

        // Runs past both.
        await SeedMemberAsync("Staying", endsOn: new DateOnly(2026, 12, 31));

        var trend = await _reports.GetRevenueTrendAsync(months: 3);

        trend.Months.Single(m => m.Label == "Jul 2026").ActiveMembers.Should().Be(2);
        trend.Months.Single(m => m.Label == "Aug 2026").ActiveMembers.Should().Be(1,
            "a falling member count under flat revenue is the early warning worth showing");
    }

    [Fact]
    public async Task Trend_KeepsRemovedMembersInTheHistoricalCount()
    {
        var member = await SeedMemberAsync("Removed", endsOn: new DateOnly(2026, 12, 31));

        member.SoftDelete();
        await _context.SaveChangesAsync();

        var trend = await _reports.GetRevenueTrendAsync(months: 3);

        // They really were training in those months. Dropping them would rewrite the past
        // every time the owner tidies the member list.
        trend.Months.Single(m => m.Label == "Jul 2026").ActiveMembers.Should().Be(1);
    }

    [Fact]
    public async Task MonthDetail_AgreesWithTheMonthOnTheChart()
    {
        var package = await SeedPackageAsync();
        var member = await SeedMemberAsync(endsOn: Today.AddDays(30));

        await SeedPaymentAsync(member, package, 30m, GymInstant(2026, 8, 3), PaymentMethod.Cash);
        await SeedPaymentAsync(member, package, 70m, GymInstant(2026, 8, 20), PaymentMethod.Whish);

        var onChart = (await _reports.GetRevenueTrendAsync(months: 3))
            .Months.Single(m => m.Label == "Aug 2026");

        var opened = await _reports.GetRevenueMonthAsync(2026, 8);

        // The bar and the screen behind it have to be the same number, or neither is worth
        // reading.
        opened.TotalUsd.Should().Be(onChart.TotalUsd);
        opened.DrawerUsd.Should().Be(onChart.DrawerUsd);
        opened.WhishUsd.Should().Be(onChart.WhishUsd);
        opened.Payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task MonthDetail_CountsARenewalOnlyWhenTheMembershipMoved()
    {
        var package = await SeedPackageAsync(price: 50m);
        var member = await SeedMemberAsync(endsOn: Today.AddDays(30));

        // Bought a period.
        await SeedPaymentAsync(member, package, 50m, GymInstant(2026, 8, 5));

        // Real money, but short of the price, so nobody's dates moved.
        _context.Payments.Add(new Payment
        {
            ClientId = member.Id,
            PackageId = package.Id,
            Amount = 20m,
            AmountReceived = 20m,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash,
            Status = TransactionStatus.Completed,
            PaymentDate = GymInstant(2026, 8, 6),
            PeriodStartDate = null
        });

        await _context.SaveChangesAsync();

        var opened = await _reports.GetRevenueMonthAsync(2026, 8);

        opened.TotalUsd.Should().Be(70m, "both are money the gym took");
        opened.RenewalCount.Should().Be(1, "only one of them moved a membership");
    }

    [Fact]
    public async Task MonthDetail_RejectsAMonthThatIsNotAMonth()
    {
        var act = async () => await _reports.GetRevenueMonthAsync(2026, 13);

        await act.Should().ThrowAsync<Application.Exceptions.BusinessException>();
    }

    [Fact]
    public async Task Trend_CountsTheCurrentMonthsMembersAsOfToday_NotTheMonthsEnd()
    {
        // Runs out in the middle of this month. They are a member today, and today is what
        // the newest bar is reporting on.
        await SeedMemberAsync("StillHere", endsOn: new DateOnly(2026, 9, 20));

        var trend = await _reports.GetRevenueTrendAsync(months: 2);
        var thisMonth = trend.Months.Single(m => m.Label == "Sep 2026");

        // Measured at the 30th they would already be gone, and every month in progress
        // would show a collapse that had not happened.
        thisMonth.ActiveMembers.Should().Be(1);
        thisMonth.InProgress.Should().BeTrue();

        trend.Months.Single(m => m.Label == "Aug 2026").InProgress.Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}
