using FluentAssertions;
using GymManagement.Application.DTOs.Client;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// The member page's data, and the search that reaches it.
///
/// The figure that matters most here is what a member still owes. It is quoted to the
/// member at the desk, chased by the who-owes-money report, and credited against by the
/// next payment - so all three have to be the same number. These tests take payments
/// through the real <see cref="PaymentService"/> rather than writing rows by hand, so a
/// change to how credit is settled shows up here rather than being quietly agreed with.
/// </summary>
public class MemberSummaryTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;
    private readonly ClientService _clients;
    private readonly PaymentService _payments;

    public MemberSummaryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"member-summary-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);

        var unitOfWork = new UnitOfWork(_context);
        var clock = new FixedClock(Today);
        var audit = new Mock<IAuditService>();

        _clients = new ClientService(unitOfWork, clock, audit.Object);
        _payments = new PaymentService(unitOfWork, clock, audit.Object);

        _context.Packages.Add(new Package
        {
            Id = 1, Name = "1 Month", DurationDays = 30, Price = 30m, IsActive = true
        });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private Client AddMember(string first, string last, string phone)
    {
        var client = new Client { FirstName = first, LastName = last, PhoneNumber = phone };
        _context.Clients.Add(client);
        _context.SaveChanges();
        return client;
    }

    private Task<PaymentDto> Pay(int clientId, decimal amount) =>
        _payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = clientId,
            PackageId = 1,
            AmountReceived = amount,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        });

    [Fact]
    public async Task PartPayment_ShowsWhatIsStillOwed()
    {
        var member = AddMember("Sara", "Khoury", "70123456");
        await Pay(member.Id, 20m);

        var summary = await _clients.GetMemberSummaryAsync(member.Id);

        summary!.TotalOwed.Should().Be(10m);
        summary.Outstanding.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                PackageName = "1 Month",
                PackagePrice = 30m,
                AmountPaid = 20m,
                AmountOwed = 10m
            }, options => options.ExcludingMissingMembers());

        summary.MembershipStatus.Should().Be(nameof(MembershipStatus.Pending),
            "a part payment records the money but must not extend the membership");
    }

    [Fact]
    public async Task PayingTheRest_ClearsTheDebtAndStartsTheMembership()
    {
        var member = AddMember("Sara", "Khoury", "70123456");
        await Pay(member.Id, 20m);
        await Pay(member.Id, 10m);

        var summary = await _clients.GetMemberSummaryAsync(member.Id);

        summary!.TotalOwed.Should().Be(0m, "the two payments together cover the price");
        summary.Outstanding.Should().BeEmpty();
        summary.MembershipStatus.Should().Be(nameof(MembershipStatus.Active));
        summary.DaysRemaining.Should().Be(29, "30 days inclusive of the day it started");
    }

    [Fact]
    public async Task History_ShowsReversalsAsCorrectionsRatherThanPayments()
    {
        var member = AddMember("Sara", "Khoury", "70123456");
        var payment = await Pay(member.Id, 30m);
        await _payments.ReversePaymentAsync(payment.Id, "took it twice by mistake");

        var summary = await _clients.GetMemberSummaryAsync(member.Id);

        summary!.Payments.Should().HaveCount(2, "the original row is never edited or deleted");
        summary.Payments.Count(p => p.IsReversal).Should().Be(1);
        summary.Payments.Sum(p => p.AmountUsd).Should().Be(0m, "the two cancel out");

        // Expired rather than Pending, and deliberately so. Winding the membership back
        // moves the end date but leaves the join date alone, because reversing a renewal
        // must not unjoin a member who has been coming for a year. When the reversed
        // payment was their only one, that leaves an end date a day before the start - an
        // empty membership, which reads as Expired. What matters at the door is the same
        // either way: they cannot come in.
        summary.MembershipStatus.Should().Be(nameof(MembershipStatus.Expired));
        MembershipStatuses.AllowsEntry(MembershipStatus.Expired).Should().BeFalse(
            "reversing the payment must take back the days it bought");
    }

    [Fact]
    public async Task Summary_LoadsForARemovedMember()
    {
        var member = AddMember("Sara", "Khoury", "70123456");
        await _clients.DeleteClientAsync(member.Id);

        var summary = await _clients.GetMemberSummaryAsync(member.Id);

        summary.Should().NotBeNull(
            "opening a removed member is how they get restored - refusing to load the page "
            + "would put the undelete out of reach again");
        summary!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Freezing_StopsEntryWithoutTakingDaysAway()
    {
        var member = AddMember("Sara", "Khoury", "70123456");
        await Pay(member.Id, 30m);
        var endBefore = (await _clients.GetMemberSummaryAsync(member.Id))!.MembershipEndDate;

        await _clients.SetSuspendedAsync(member.Id, suspended: true, "travelling");

        var frozen = await _clients.GetMemberSummaryAsync(member.Id);
        frozen!.MembershipStatus.Should().Be(nameof(MembershipStatus.Suspended));
        frozen.IsSuspended.Should().BeTrue();
        frozen.MembershipEndDate.Should().Be(endBefore,
            "a freeze stops them being let in; it does not hand days back");

        await _clients.SetSuspendedAsync(member.Id, suspended: false);

        var resumed = await _clients.GetMemberSummaryAsync(member.Id);
        resumed!.MembershipStatus.Should().Be(nameof(MembershipStatus.Active));
    }

    [Fact]
    public async Task PhoneDigits_AreReadyForCallAndWhatsAppLinks()
    {
        var member = AddMember("Sara", "Khoury", "+961 70 123 456");

        var summary = await _clients.GetMemberSummaryAsync(member.Id);

        summary!.PhoneNumber.Should().Be("+961 70 123 456", "the number is stored as written");
        summary.PhoneDigits.Should().Be("70123456", "the link needs digits only");
    }

    // ---- Search ----

    [Theory]
    [InlineData("70123456")]
    [InlineData("70 123 456")]
    [InlineData("70-123-456")]
    [InlineData("123456")]
    public async Task Search_FindsAMemberHoweverTheNumberIsTyped(string typed)
    {
        var member = AddMember("Sara", "Khoury", "70 123 456");

        var found = await _clients.GetClientsAsync(new ClientQueryParameters { Search = typed });

        found.Items.Should().ContainSingle().Which.Id.Should().Be(member.Id,
            $"typing '{typed}' at the desk has to find the member saved as '70 123 456'");
    }

    [Fact]
    public async Task Search_MatchesAFullName()
    {
        var member = AddMember("Sara", "Khoury", "70123456");
        AddMember("Rabih", "Jaafar", "03663336");

        var found = await _clients.GetClientsAsync(
            new ClientQueryParameters { Search = "sara khoury" });

        found.Items.Should().ContainSingle().Which.Id.Should().Be(member.Id,
            "reception types the whole name, not just one half of it");
    }

    [Fact]
    public async Task Search_DoesNotTreatAShortNameAsAPhoneNumber()
    {
        AddMember("Sara", "Khoury", "70123456");

        var found = await _clients.GetClientsAsync(new ClientQueryParameters { Search = "zz" });

        found.Items.Should().BeEmpty();
    }
}
