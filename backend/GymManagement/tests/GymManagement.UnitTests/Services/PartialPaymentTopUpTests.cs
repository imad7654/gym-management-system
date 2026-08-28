using FluentAssertions;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Exceptions;
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
/// What happens when a member who underpaid comes back with the rest.
///
/// This is the case the who-owes-money list creates: the owner reads the list, chases the
/// member for the difference, and the member pays it at the desk. If the second payment
/// does not complete the first, the list sends the owner to collect money the system then
/// cannot account for.
/// </summary>
public class PartialPaymentTopUpTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly PaymentService _payments;

    public PartialPaymentTopUpTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"topup-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Packages.Add(new Package { Id = 1, Name = "Monthly", DurationDays = 30, Price = 50m });
        _context.Clients.Add(new Client
        {
            Id = 1, FirstName = "Sara", LastName = "Khoury", PhoneNumber = "03123456"
        });
        _context.SaveChanges();

        _unitOfWork = new UnitOfWork(_context);
        _payments = new PaymentService(_unitOfWork, new FixedClock(Today), new AuditService(_unitOfWork, new FixedClock(Today)));
    }

    public void Dispose() => _unitOfWork.Dispose();

    [Fact]
    public async Task PayingHalf_RecordsTheMoneyButDoesNotMoveTheMembership()
    {
        await Pay(30m);

        var client = _context.Clients.Single();
        client.PaymentStatus.Should().Be(PaymentStatus.Partial);
        client.MembershipEndDate.Should().BeNull("a half payment must not unlock a full month");
        _context.Payments.Single().Amount.Should().Be(30m);
    }

    [Fact]
    public async Task PayingTheRestLater_CompletesTheMembership()
    {
        await Pay(30m);
        await Pay(20m);

        // 30 + 20 is the full 50. The member has paid for their month and should have it.
        var client = _context.Clients.Single();
        client.PaymentStatus.Should().Be(PaymentStatus.Paid);
        client.MembershipEndDate.Should().Be(Today.AddDays(29).ToDateTime(TimeOnly.MinValue));
        client.MembershipStatus.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task PayingPartOfTheRest_StillLeavesThemOwing()
    {
        await Pay(30m);
        await Pay(10m);

        var client = _context.Clients.Single();
        client.PaymentStatus.Should().Be(PaymentStatus.Partial);
        client.MembershipEndDate.Should().BeNull();
    }

    [Fact]
    public async Task PayingOffAPartial_DoesNotDiscountTheNextMonth()
    {
        await Pay(30m);
        await Pay(20m);

        // The 30 has been spent. If it were still counted as money on account, the next
        // month would be handed over for 20 - the member would pay for it once and get it
        // twice.
        await Pay(20m);

        var client = _context.Clients.Single();
        client.PaymentStatus.Should().Be(PaymentStatus.Partial);
        client.MembershipEndDate.Should().Be(Today.AddDays(29).ToDateTime(TimeOnly.MinValue),
            "the second month was not paid for, so the dates must not move again");
    }

    [Fact]
    public async Task ReversingTheCompletingPayment_TakesBackTheDaysAndLeavesThemPartPaidAgain()
    {
        await Pay(30m);
        var completing = await Pay(20m);

        await _payments.ReversePaymentAsync(completing.Id, "Wrong member", userId: 1);

        var client = _context.Clients.Single();
        client.MembershipEndDate.Should().Be(Today.AddDays(-1).ToDateTime(TimeOnly.MinValue));

        // The 30 they really did hand over is theirs again, not swallowed by the reversal.
        client.PaymentStatus.Should().Be(PaymentStatus.Partial);
        _context.Payments.Single(p => p.Amount == 30m).SettledByPaymentId.Should().BeNull();
    }

    [Fact]
    public async Task AfterReversingTheCompletingPayment_PayingTheRestAgainWorks()
    {
        await Pay(30m);
        var completing = await Pay(20m);
        await _payments.ReversePaymentAsync(completing.Id, "Wrong amount", userId: 1);

        await Pay(20m);

        var client = _context.Clients.Single();
        client.PaymentStatus.Should().Be(PaymentStatus.Paid);
        client.MembershipEndDate.Should().Be(Today.AddDays(29).ToDateTime(TimeOnly.MinValue));
    }

    [Fact]
    public async Task ReversingAPartPaymentThatWasAlreadyCompleted_IsRefusedWithAWayForward()
    {
        var first = await Pay(30m);
        var completing = await Pay(20m);

        var act = () => _payments.ReversePaymentAsync(first.Id, "Mistake", userId: 1);

        // Undoing the 30 on its own would leave a month paid for by money that is no longer
        // there. The message says which payment to undo first rather than just refusing.
        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage($"*#{completing.Id}*");
    }

    [Fact]
    public async Task ReversingAnUncompletedPartPayment_JustGivesTheMoneyBack()
    {
        var only = await Pay(30m);

        await _payments.ReversePaymentAsync(only.Id, "Member changed their mind", userId: 1);

        // The +30 and the -30 cancel, so nothing is outstanding and the dates never moved.
        var client = _context.Clients.Single();
        client.MembershipEndDate.Should().BeNull();
        _context.Payments.OutstandingCredit().Sum(p => p.Amount).Should().Be(0m);
    }

    [Fact]
    public async Task ThreeSmallPaymentsAddingUpToThePrice_CompleteTheMembership()
    {
        await Pay(20m);
        await Pay(20m);
        await Pay(10m);

        var client = _context.Clients.Single();
        client.PaymentStatus.Should().Be(PaymentStatus.Paid);
        client.MembershipEndDate.Should().Be(Today.AddDays(29).ToDateTime(TimeOnly.MinValue));
    }

    private Task<PaymentDto> Pay(decimal amount) =>
        _payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = 1,
            PackageId = 1,
            AmountReceived = amount,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        }, userId: 1);

}
