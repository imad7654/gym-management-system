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
/// What the owner's revenue figures do when a payment is reversed.
///
/// This is pinned down because it is easy to get wrong in a way nobody notices until the
/// numbers are already untrustworthy. The rule the code settled on: the original row is
/// never edited, and the reversal is a second row with a negative amount that is also
/// Completed - so summing Completed rows gives the true net take.
///
/// The failure this guards against is subtracting twice: flipping the original out of the
/// Completed sum *and* adding a negative row, which makes one refund cost the gym double
/// and can show a day's revenue as negative.
/// </summary>
public class RevenueAfterReversalTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly PaymentService _payments;
    private readonly DashboardService _dashboard;

    public RevenueAfterReversalTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"revenue-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Packages.Add(new Package { Id = 1, Name = "Monthly", DurationDays = 30, Price = 50m });
        _context.Clients.Add(new Client
        {
            Id = 1, FirstName = "Sara", LastName = "Khoury", PhoneNumber = "03123456"
        });
        _context.SaveChanges();

        var clock = new FixedClock(Today);
        _unitOfWork = new UnitOfWork(_context);
        _payments = new PaymentService(_unitOfWork, clock);
        _dashboard = new DashboardService(_unitOfWork, clock);
    }

    public void Dispose() => _unitOfWork.Dispose();

    [Fact]
    public async Task ReversingAPayment_LeavesNetRevenueAtZero_NotMinusTheAmount()
    {
        var payment = await TakePayment(50m);

        var beforeReversal = await _dashboard.GetStatsAsync();
        beforeReversal.RevenueSummary.TotalRevenue.Should().Be(50m);

        await _payments.ReversePaymentAsync(payment.Id, "Wrong member", userId: 1);

        var afterReversal = await _dashboard.GetStatsAsync();
        afterReversal.RevenueSummary.TotalRevenue.Should().Be(0m,
            "the 50 taken and the 50 given back cancel exactly - subtracting twice would show -50");
        afterReversal.RevenueSummary.TodayRevenue.Should().Be(0m);
    }

    [Fact]
    public async Task ReversingOnePaymentOfTwo_LeavesTheOtherIntact()
    {
        var first = await TakePayment(50m);
        await TakePayment(50m);

        await _payments.ReversePaymentAsync(first.Id, "Wrong member", userId: 1);

        var stats = await _dashboard.GetStatsAsync();
        stats.RevenueSummary.TotalRevenue.Should().Be(50m);
    }

    [Fact]
    public async Task ReversingAPayment_LeavesTheOriginalRowExactlyAsItWas()
    {
        var payment = await TakePayment(50m);

        await _payments.ReversePaymentAsync(payment.Id, "Wrong member", userId: 1);

        // Money rows are append-only. Editing the original is what makes a till
        // impossible to check afterwards, and it is also how the double-subtraction
        // above gets reintroduced.
        var original = _context.Payments.Single(p => p.Id == payment.Id);
        original.Amount.Should().Be(50m);
        original.Status.Should().Be(TransactionStatus.Completed);

        var reversal = _context.Payments.Single(p => p.ReversesPaymentId == payment.Id);
        reversal.Amount.Should().Be(-50m);
        reversal.Status.Should().Be(TransactionStatus.Completed,
            "a reversal is recognised by ReversesPaymentId, not by being excluded from the sums");
    }

    [Fact]
    public async Task ReversingAPayment_TakesBackTheDaysItBought()
    {
        var payment = await TakePayment(50m);

        var client = _context.Clients.Single(c => c.Id == 1);
        client.MembershipEndDate.Should().Be(Today.AddDays(29).ToDateTime(TimeOnly.MinValue));

        await _payments.ReversePaymentAsync(payment.Id, "Wrong member", userId: 1);

        _context.Entry(client).Reload();
        client.MembershipEndDate.Should().Be(Today.AddDays(-1).ToDateTime(TimeOnly.MinValue),
            "reversing a payment is the exact inverse of taking one");
    }

    private Task<PaymentDto> TakePayment(decimal amount) =>
        _payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = 1,
            PackageId = 1,
            AmountReceived = amount,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        }, userId: 1);

    private sealed class FixedClock : IMembershipClock
    {
        public FixedClock(DateOnly today) => Today = today;
        public DateTime UtcNow => Today.ToDateTime(TimeOnly.MinValue);
        public DateOnly Today { get; }
    }
}
