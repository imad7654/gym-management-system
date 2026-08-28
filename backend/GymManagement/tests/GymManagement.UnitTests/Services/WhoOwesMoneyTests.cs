using FluentAssertions;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// The who-owes-money list.
///
/// The owner works this list by phoning people, so a wrong name on it costs them a
/// conversation they should not have had. The two ways to get that wrong are billing
/// someone whose money is actually square, and quietly dropping someone who really does
/// owe - both are pinned down here.
/// </summary>
public class WhoOwesMoneyTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly PaymentService _payments;
    private readonly ReportService _reports;

    public WhoOwesMoneyTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"owes-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Packages.Add(new Package { Id = 1, Name = "Monthly", DurationDays = 30, Price = 50m });
        _context.Clients.AddRange(
            new Client { Id = 1, FirstName = "Sara", LastName = "Khoury", PhoneNumber = "03123456" },
            new Client { Id = 2, FirstName = "Ali", LastName = "Hassan", PhoneNumber = "03222333" });
        _context.SaveChanges();

        var clock = new FixedClock(Today);
        _unitOfWork = new UnitOfWork(_context);
        _payments = new PaymentService(_unitOfWork, clock, new AuditService(_unitOfWork, clock));
        _reports = new ReportService(_unitOfWork, clock);
    }

    public void Dispose() => _unitOfWork.Dispose();

    [Fact]
    public async Task NobodyHasPartPaid_TheListIsEmpty()
    {
        await Pay(1, 50m);

        var report = await _reports.GetWhoOwesMoneyAsync();

        report.MemberCount.Should().Be(0);
        report.TotalOwed.Should().Be(0m);
    }

    [Fact]
    public async Task APartPayment_ShowsWhatWasPaidAndWhatIsLeft()
    {
        await Pay(1, 30m);

        var report = await _reports.GetWhoOwesMoneyAsync();

        var row = report.Members.Should().ContainSingle().Subject;
        row.ClientName.Should().Be("Sara Khoury");
        row.PhoneNumber.Should().Be("03123456");
        row.PackageName.Should().Be("Monthly");
        row.PackagePrice.Should().Be(50m);
        row.AmountPaid.Should().Be(30m);
        row.AmountOwed.Should().Be(20m);
        report.TotalOwed.Should().Be(20m);
    }

    [Fact]
    public async Task TwoPartPaymentsFromOneMember_AreOneLineNotTwo()
    {
        await Pay(1, 20m);
        await Pay(1, 10m);

        var report = await _reports.GetWhoOwesMoneyAsync();

        // The owner is chasing a person for a number, not a list of instalments.
        var row = report.Members.Should().ContainSingle().Subject;
        row.AmountPaid.Should().Be(30m);
        row.AmountOwed.Should().Be(20m);
    }

    [Fact]
    public async Task OnceTheyPayTheRest_TheyComeOffTheList()
    {
        await Pay(1, 30m);
        await Pay(1, 20m);

        var report = await _reports.GetWhoOwesMoneyAsync();

        report.MemberCount.Should().Be(0, "they have paid in full and must not be chased");
    }

    [Fact]
    public async Task WhenAPartPaymentIsReversed_TheyComeOffTheList()
    {
        var partial = await Pay(1, 30m);
        await _payments.ReversePaymentAsync(partial.Id, "Given back", userId: 1);

        var report = await _reports.GetWhoOwesMoneyAsync();

        // The money went back to them, so nothing is owed. Billing them for the 20 they
        // never really owed is the worst thing this list could do.
        report.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task WhenACompletedMembershipIsReversed_TheEarlierPartPaymentIsOwedAgain()
    {
        await Pay(1, 30m);
        var completing = await Pay(1, 20m);
        await _payments.ReversePaymentAsync(completing.Id, "Wrong member", userId: 1);

        var report = await _reports.GetWhoOwesMoneyAsync();

        var row = report.Members.Should().ContainSingle().Subject;
        row.AmountPaid.Should().Be(30m, "the 30 they handed over is still theirs");
        row.AmountOwed.Should().Be(20m);
    }

    [Fact]
    public async Task TheLongestOutstandingDebtIsListedFirst()
    {
        var old = await Pay(1, 30m);
        _context.Payments.Single(p => p.Id == old.Id).PaymentDate = Today.AddDays(-40).ToDateTime(TimeOnly.MinValue);
        await _context.SaveChangesAsync();

        await Pay(2, 25m);

        var report = await _reports.GetWhoOwesMoneyAsync();

        report.Members[0].ClientName.Should().Be("Sara Khoury");
        report.Members[0].DaysOutstanding.Should().Be(40);
        report.Members[1].ClientName.Should().Be("Ali Hassan");
        report.MemberCount.Should().Be(2);
        report.TotalOwed.Should().Be(45m);
    }

    [Fact]
    public async Task ARemovedMemberIsNotChasedForMoney()
    {
        await Pay(1, 30m);

        var client = _context.Clients.Single(c => c.Id == 1);
        client.SoftDelete();
        await _context.SaveChangesAsync();

        var report = await _reports.GetWhoOwesMoneyAsync();

        report.MemberCount.Should().Be(0);
    }

    private Task<PaymentDto> Pay(int clientId, decimal amount) =>
        _payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = clientId,
            PackageId = 1,
            AmountReceived = amount,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        }, userId: 1);

}
