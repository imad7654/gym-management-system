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
/// The daily takings report - the one the owner uses every day to count the drawer.
///
/// Its whole value is that the number matches the cash in hand. Two things break that: money
/// that never touched the drawer being counted as if it had, and money moving between days
/// because the server's idea of "today" is not the gym's.
/// </summary>
public class DailyTakingsTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    /// <summary>Beirut in summer. Chosen so the day boundary is genuinely not UTC midnight.</summary>
    private const int GymOffsetHours = 3;

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly PaymentService _payments;
    private readonly ReportService _reports;

    public DailyTakingsTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"takings-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Packages.Add(new Package { Id = 1, Name = "Monthly", DurationDays = 30, Price = 50m });
        _context.Clients.Add(new Client
        {
            Id = 1, FirstName = "Sara", LastName = "Khoury", PhoneNumber = "03123456"
        });
        _context.SaveChanges();

        var clock = new FixedClock(Today, GymOffsetHours);
        _unitOfWork = new UnitOfWork(_context);
        _payments = new PaymentService(_unitOfWork, clock, new AuditService(_unitOfWork, clock));
        _reports = new ReportService(_unitOfWork, clock);
    }

    public void Dispose() => _unitOfWork.Dispose();

    [Fact]
    public async Task ANoMoneyDay_ReportsZerosRatherThanFailing()
    {
        var takings = await _reports.GetDailyTakingsAsync();

        takings.TotalUsd.Should().Be(0m);
        takings.DrawerTotalUsd.Should().Be(0m);
        takings.PaymentCount.Should().Be(0);
        takings.Payments.Should().BeEmpty();
    }

    [Fact]
    public async Task UsdCash_CountsTowardTheDrawer()
    {
        await Pay(50m, PaymentMethod.Cash);

        var takings = await _reports.GetDailyTakingsAsync();

        takings.CashUsd.Should().Be(50m);
        takings.DrawerTotalUsd.Should().Be(50m);
        takings.TotalUsd.Should().Be(50m);
        takings.PaymentCount.Should().Be(1);
    }

    [Fact]
    public async Task WhishMoney_CountsAsIncomeButNotAsCashInTheDrawer()
    {
        await Pay(50m, PaymentMethod.Whish);

        var takings = await _reports.GetDailyTakingsAsync();

        // The single most important line in this report. If a transfer inflates the drawer
        // figure, the owner counts the till, finds it short, and stops trusting the report.
        takings.WhishUsd.Should().Be(50m);
        takings.DrawerTotalUsd.Should().Be(0m);
        takings.TotalUsd.Should().Be(50m);
    }

    [Fact]
    public async Task LbpCash_IsShownAsBothTheNotesAndTheirUsdValue()
    {
        await PayLbp(received: 4_475_000m, rate: 89_500m);

        var takings = await _reports.GetDailyTakingsAsync();

        // The notes are what gets counted; the USD figure is what the totals add up.
        takings.CashLbpReceived.Should().Be(4_475_000m);
        takings.CashLbpInUsd.Should().Be(50m);
        takings.DrawerTotalUsd.Should().Be(50m);
    }

    [Fact]
    public async Task CashAndWhishOnTheSameDay_AreSplitApart()
    {
        await Pay(50m, PaymentMethod.Cash);
        await Pay(50m, PaymentMethod.Whish);
        await PayLbp(received: 1_790_000m, rate: 89_500m);

        var takings = await _reports.GetDailyTakingsAsync();

        takings.CashUsd.Should().Be(50m);
        takings.CashLbpInUsd.Should().Be(20m);
        takings.DrawerTotalUsd.Should().Be(70m, "only the cash is in the drawer");
        takings.WhishUsd.Should().Be(50m);
        takings.TotalUsd.Should().Be(120m);
    }

    [Fact]
    public async Task AReversalTakenTheSameDay_ReducesTheDrawer()
    {
        var payment = await Pay(50m, PaymentMethod.Cash);
        await _payments.ReversePaymentAsync(payment.Id, "Wrong member", userId: 1);

        var takings = await _reports.GetDailyTakingsAsync();

        // The cash really did leave the drawer. Leaving refunds out would send the owner
        // hunting for a shortfall they created themselves.
        takings.DrawerTotalUsd.Should().Be(0m);
        takings.ReversalsUsd.Should().Be(-50m);
        takings.ReversalCount.Should().Be(1);
        takings.PaymentCount.Should().Be(1, "the original payment still happened");
    }

    [Fact]
    public async Task APaymentTakenLateInTheGymsEvening_BelongsToThatDayNotTheNext()
    {
        // 23:30 in Beirut on the 28th is 20:30 UTC on the 28th - same date either way.
        await PayAt(Today, hourUtc: 20, minute: 30);

        // 00:30 in Beirut on the 29th is 21:30 UTC on the *28th*. Filtering on the UTC date
        // would file this under the 28th, and the owner's count for the 28th would include
        // money taken after they had already cashed up.
        await PayAt(Today, hourUtc: 21, minute: 30);

        var twentyEighth = await _reports.GetDailyTakingsAsync(Today);
        var twentyNinth = await _reports.GetDailyTakingsAsync(Today.AddDays(1));

        twentyEighth.PaymentCount.Should().Be(1);
        twentyNinth.PaymentCount.Should().Be(1);
    }

    [Fact]
    public async Task AskingForAnEarlierDay_ShowsThatDayAndNotToday()
    {
        var yesterdays = await Pay(50m, PaymentMethod.Cash);
        _context.Payments.Single(p => p.Id == yesterdays.Id).PaymentDate =
            Today.AddDays(-1).ToDateTime(new TimeOnly(12, 0)).AddHours(-GymOffsetHours);
        await _context.SaveChangesAsync();

        (await _reports.GetDailyTakingsAsync(Today)).TotalUsd.Should().Be(0m);
        (await _reports.GetDailyTakingsAsync(Today.AddDays(-1))).TotalUsd.Should().Be(50m);
    }

    [Fact]
    public async Task EveryMovementIsListed_SoAFigureCanBeChecked()
    {
        await Pay(50m, PaymentMethod.Cash);
        await Pay(30m, PaymentMethod.Whish);

        var takings = await _reports.GetDailyTakingsAsync();

        takings.Payments.Should().HaveCount(2);
        takings.Payments.Should().Contain(p => p.ClientName == "Sara Khoury" && p.AmountUsd == 50m);
        takings.Payments.Should().OnlyContain(p => p.PackageName == "Monthly");
    }

    private Task<PaymentDto> Pay(decimal amount, PaymentMethod method) =>
        _payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = 1,
            PackageId = 1,
            AmountReceived = amount,
            Currency = Currency.Usd,
            PaymentMethod = method
        }, userId: 1);

    private Task<PaymentDto> PayLbp(decimal received, decimal rate) =>
        _payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = 1,
            PackageId = 1,
            AmountReceived = received,
            Currency = Currency.Lbp,
            ExchangeRate = rate,
            PaymentMethod = PaymentMethod.Cash
        }, userId: 1);

    /// <summary>Records a payment and forces it to a specific UTC instant.</summary>
    private async Task PayAt(DateOnly gymDate, int hourUtc, int minute)
    {
        var payment = await Pay(50m, PaymentMethod.Cash);
        _context.Payments.Single(p => p.Id == payment.Id).PaymentDate =
            gymDate.ToDateTime(new TimeOnly(hourUtc, minute));
        await _context.SaveChangesAsync();
    }
}
