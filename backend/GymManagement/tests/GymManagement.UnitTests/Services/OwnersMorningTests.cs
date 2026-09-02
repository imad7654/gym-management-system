using FluentAssertions;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// The first screen of the day.
///
/// The old dashboard led with all-time revenue, a number nobody acts on, and its "today"
/// disagreed with the takings report about which day it was. These are written from what
/// this screen has to get right instead: the same figures the reports show, a call sheet
/// that includes the people who have already gone, and a "called" mark that answers the
/// gym's question rather than the server's.
/// </summary>
public class OwnersMorningTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 2);

    /// <summary>Beirut runs ahead of UTC; the offset is what makes the day-boundary cases bite.</summary>
    private const int BeirutOffsetHours = 3;

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly FixedClock _clock;
    private readonly DashboardService _dashboard;
    private readonly PaymentService _payments;

    public OwnersMorningTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"morning-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _clock = new FixedClock(Today, BeirutOffsetHours);

        _dashboard = new DashboardService(
            _unitOfWork, _clock, new ReportService(_unitOfWork, _clock));

        _payments = new PaymentService(
            _unitOfWork, _clock, new AuditService(_unitOfWork, _clock));
    }

    private async Task<Package> SeedPackageAsync()
    {
        var package = new Package { Name = "Monthly", Price = 50m, DurationDays = 30 };
        _context.Packages.Add(package);
        await _context.SaveChangesAsync();
        return package;
    }

    private async Task<Client> SeedMemberAsync(
        string firstName,
        DateOnly endsOn,
        bool isSuspended = false,
        DateTime? lastChasedAt = null)
    {
        var client = new Client
        {
            FirstName = firstName,
            LastName = "Member",
            PhoneNumber = "03 111 000",
            IsSuspended = isSuspended,
            MembershipStartDate = endsOn.AddDays(-30).ToDateTime(TimeOnly.MinValue),
            MembershipEndDate = endsOn.ToDateTime(TimeOnly.MinValue),
            LastChasedAt = lastChasedAt
        };

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    // -------------------------------------------------------------- the call sheet

    [Fact]
    public async Task NeedsChasing_IncludesTheOnesWhoHaveAlreadyLapsed()
    {
        await SeedMemberAsync("Lapsed", Today.AddDays(-10));
        await SeedMemberAsync("Expiring", Today.AddDays(3));
        await SeedMemberAsync("Comfortable", Today.AddDays(60));

        var chasing = await _dashboard.GetNeedsChasingAsync();

        // The expiring list on its own only ever showed people who had not left yet. The
        // ones already gone are exactly who a phone call wins back, and no screen had them.
        chasing.Select(c => c.ClientName)
            .Should().BeEquivalentTo(new[] { "Lapsed Member", "Expiring Member" });
    }

    [Fact]
    public async Task NeedsChasing_PutsTheLapsedFirst()
    {
        await SeedMemberAsync("Expiring", Today.AddDays(5));
        await SeedMemberAsync("Lapsed", Today.AddDays(-8));

        var chasing = await _dashboard.GetNeedsChasingAsync();

        chasing.First().ClientName.Should().Be("Lapsed Member",
            "the people already gone are the ones worth ringing first");
        chasing.First().DaysRemaining.Should().Be(-8);
    }

    [Fact]
    public async Task NeedsChasing_LeavesOutMembersWhoAreFrozen()
    {
        await SeedMemberAsync("Travelling", Today.AddDays(2), isSuspended: true);

        var chasing = await _dashboard.GetNeedsChasingAsync();

        // Somebody who told the gym they are away does not want a call asking why they
        // have not been in.
        chasing.Should().BeEmpty();
    }

    [Fact]
    public async Task NeedsChasing_LeavesOutMembersWhoLapsedLongAgo()
    {
        await SeedMemberAsync("LongGone", Today.AddDays(-120));

        var chasing = await _dashboard.GetNeedsChasingAsync();

        // Past a month it stops being a nudge and becomes a cold sell, and a list nobody
        // finishes is a list nobody opens.
        chasing.Should().BeEmpty();
    }

    // ------------------------------------------------------------ the called mark

    [Fact]
    public async Task MarkChased_ShowsAsCalledToday()
    {
        var member = await SeedMemberAsync("Lapsed", Today.AddDays(-3));

        await _dashboard.MarkChasedAsync(member.Id, called: true);

        var chasing = await _dashboard.GetNeedsChasingAsync();
        chasing.Single().CalledToday.Should().BeTrue();
    }

    [Fact]
    public async Task MarkChased_CanBeTakenBackOff()
    {
        var member = await SeedMemberAsync("Lapsed", Today.AddDays(-3));

        await _dashboard.MarkChasedAsync(member.Id, called: true);
        await _dashboard.MarkChasedAsync(member.Id, called: false);

        var chasing = await _dashboard.GetNeedsChasingAsync();
        chasing.Single().CalledToday.Should().BeFalse();
    }

    [Fact]
    public async Task CalledToday_IsTheGymsDayNotTheServers()
    {
        // 21:00 in Beirut on the gym's today, which is 18:00 UTC - still today either way.
        // The case that matters is the other side: an instant that is today in Beirut but
        // yesterday in UTC, which a naive comparison files under the wrong day and offers
        // the member up to be rung a second time.
        var justAfterMidnightInBeirut = Today.ToDateTime(new TimeOnly(0, 30))
            .AddHours(-BeirutOffsetHours);

        var member = await SeedMemberAsync(
            "Lapsed", Today.AddDays(-3), lastChasedAt: justAfterMidnightInBeirut);

        var chasing = await _dashboard.GetNeedsChasingAsync();

        chasing.Single().CalledToday.Should().BeTrue(
            "half past midnight in Beirut is today at the gym, whatever UTC calls it");
    }

    [Fact]
    public async Task CalledYesterday_DoesNotCountAsCalledToday()
    {
        var yesterdayEvening = Today.AddDays(-1).ToDateTime(new TimeOnly(20, 0))
            .AddHours(-BeirutOffsetHours);

        var member = await SeedMemberAsync(
            "Lapsed", Today.AddDays(-3), lastChasedAt: yesterdayEvening);

        var chasing = await _dashboard.GetNeedsChasingAsync();

        chasing.Single().CalledToday.Should().BeFalse(
            "the list resets each morning, or nobody would ever be rung twice");
    }

    // ------------------------------------------------------------------ the money

    [Fact]
    public async Task Today_ShowsTheSameDrawerFigureAsTheTakingsReport()
    {
        var package = await SeedPackageAsync();
        var member = await SeedMemberAsync("Payer", Today.AddDays(-2));

        await _payments.CreatePaymentAsync(new Application.DTOs.Payment.CreatePaymentRequest
        {
            ClientId = member.Id,
            PackageId = package.Id,
            AmountReceived = 50m,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        });

        var morning = await _dashboard.GetTodayAsync();
        var report = await new ReportService(_unitOfWork, _clock).GetDailyTakingsAsync(Today);

        // Two screens showing two numbers for the same day is what stopped either being
        // trusted last time. This one reads the report rather than counting again.
        morning.DrawerTotalUsd.Should().Be(report.DrawerTotalUsd);
        morning.TotalUsd.Should().Be(report.TotalUsd);
        morning.DrawerTotalUsd.Should().Be(50m);
    }

    [Fact]
    public async Task Today_CountsARenewalOnlyWhenTheMembershipActuallyMoved()
    {
        var package = await SeedPackageAsync();
        var renewer = await SeedMemberAsync("Renewer", Today.AddDays(-2));
        var partPayer = await SeedMemberAsync("PartPayer", Today.AddDays(-2));

        await _payments.CreatePaymentAsync(new Application.DTOs.Payment.CreatePaymentRequest
        {
            ClientId = renewer.Id,
            PackageId = package.Id,
            AmountReceived = 50m,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        });

        // Short of the price, so it buys no days and is not a renewal - even though it is
        // real money in the drawer.
        await _payments.CreatePaymentAsync(new Application.DTOs.Payment.CreatePaymentRequest
        {
            ClientId = partPayer.Id,
            PackageId = package.Id,
            AmountReceived = 20m,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        });

        var morning = await _dashboard.GetTodayAsync();

        morning.RenewalsToday.Should().Be(1,
            "calling a part payment a renewal would tell the owner the chasing worked when "
            + "nobody's dates moved");

        morning.PaymentCount.Should().Be(2, "both are money taken today");
    }

    [Fact]
    public async Task Today_KeepsWhishOutOfTheDrawerFigure()
    {
        var package = await SeedPackageAsync();
        var member = await SeedMemberAsync("Payer", Today.AddDays(-2));

        await _payments.CreatePaymentAsync(new Application.DTOs.Payment.CreatePaymentRequest
        {
            ClientId = member.Id,
            PackageId = package.Id,
            AmountReceived = 50m,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Whish
        });

        var morning = await _dashboard.GetTodayAsync();

        // Whish money never touched the till. Counting it in the drawer would make the
        // owner's count come up short every single day.
        morning.DrawerTotalUsd.Should().Be(0m);
        morning.WhishUsd.Should().Be(50m);
        morning.TotalUsd.Should().Be(50m);
    }

    [Fact]
    public async Task Today_CountsHowManyHaveAlreadyBeenCalled()
    {
        var first = await SeedMemberAsync("One", Today.AddDays(-3));
        await SeedMemberAsync("Two", Today.AddDays(-4));

        await _dashboard.MarkChasedAsync(first.Id, called: true);

        var morning = await _dashboard.GetTodayAsync();

        morning.NeedsChasing.Should().HaveCount(2);
        morning.CalledToday.Should().Be(1);
    }

    public void Dispose() => _context.Dispose();
}
