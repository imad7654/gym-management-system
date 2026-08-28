using FluentAssertions;
using GymManagement.Application.DTOs.Client;
using GymManagement.Application.DTOs.Payment;
using GymManagement.Application.DTOs.Reports;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// The audit trail.
///
/// Its only job is to still make sense long after the fact, so what is checked here is that
/// the things worth asking about later actually get written - money taken, money handed
/// back, membership dates moved, members removed - and that each entry says who did it in
/// words rather than in ids.
/// </summary>
public class AuditTrailTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly AuditService _audit;
    private readonly ClientService _clients;
    private readonly PaymentService _payments;

    public AuditTrailTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Packages.Add(new Package { Id = 1, Name = "Monthly", DurationDays = 30, Price = 50m });
        _context.Users.Add(new User
        {
            Id = 7, Email = "owner@gym.local", FirstName = "Jennifer", LastName = "Choukeir",
            PasswordHash = "x"
        });
        _context.SaveChanges();

        var clock = new FixedClock(Today);
        _unitOfWork = new UnitOfWork(_context);
        _audit = new AuditService(_unitOfWork, clock);
        _clients = new ClientService(_unitOfWork, clock, _audit);
        _payments = new PaymentService(_unitOfWork, clock, _audit);
    }

    public void Dispose() => _unitOfWork.Dispose();

    [Fact]
    public async Task AddingAMember_IsRecordedAgainstThePersonWhoDidIt()
    {
        await AddClient();

        var entry = (await Entries()).Single();
        entry.EntityType.Should().Be("Client");
        entry.Action.Should().Be(nameof(AuditAction.Created));
        entry.Summary.Should().Contain("Sara Khoury");

        // The name, not the id. "User #7 deleted this member" answers nothing once user 7
        // is gone, which is exactly when the trail gets read.
        entry.ActorName.Should().Be("Jennifer Choukeir");
    }

    [Fact]
    public async Task MovingAMembershipEndDate_RecordsWhatItWasAndWhatItBecame()
    {
        var client = await AddClient();

        await _clients.UpdateClientAsync(client.Id, new UpdateClientRequest
        {
            FirstName = "Sara",
            LastName = "Khoury",
            PhoneNumber = "03123456",
            PackageId = 1,
            MembershipStartDate = new DateTime(2026, 8, 1),
            MembershipEndDate = new DateTime(2026, 12, 31)
        }, userId: 7);

        // A changed end date is a member let in or turned away. It is the single thing most
        // worth being able to trace back to a person.
        var entry = (await Entries()).First();
        entry.Action.Should().Be(nameof(AuditAction.Updated));
        entry.Details.Should().Contain("2026-12-31");
    }

    [Fact]
    public async Task RemovingAndRestoringAMember_AreBothRecorded()
    {
        var client = await AddClient();

        await _clients.DeleteClientAsync(client.Id, userId: 7);
        await _clients.RestoreClientAsync(client.Id, userId: 7);

        var actions = (await Entries()).Select(e => e.Action).ToList();
        actions.Should().Contain(nameof(AuditAction.Deleted));
        actions.Should().Contain(nameof(AuditAction.Restored));
    }

    [Fact]
    public async Task TakingAndReversingAPayment_AreBothRecordedWithTheAmount()
    {
        var client = await AddClient();
        var payment = await _payments.CreatePaymentAsync(new CreatePaymentRequest
        {
            ClientId = client.Id,
            PackageId = 1,
            AmountReceived = 50m,
            Currency = Currency.Usd,
            PaymentMethod = PaymentMethod.Cash
        }, userId: 7);

        await _payments.ReversePaymentAsync(payment.Id, "Wrong member", userId: 7);

        var payments = (await Entries("Payment")).ToList();
        payments.Should().HaveCount(2);
        payments.Should().Contain(e => e.Action == nameof(AuditAction.Created) && e.Summary.Contains("50.00"));

        var reversal = payments.Single(e => e.Action == nameof(AuditAction.Reversed));
        reversal.Summary.Should().Contain("Gave back");
        reversal.Details.Should().Contain("Wrong member");
    }

    [Fact]
    public async Task TheTrailReadsNewestFirst()
    {
        var client = await AddClient();
        await _clients.DeleteClientAsync(client.Id, userId: 7);

        var entries = await Entries();

        entries.First().Action.Should().Be(nameof(AuditAction.Deleted));
        entries.Last().Action.Should().Be(nameof(AuditAction.Created));
    }

    [Fact]
    public async Task TheTrailCanBeNarrowedToOneMember()
    {
        var sara = await AddClient();
        await AddClient("Ali", "Hassan", "03222333");

        var entries = await _audit.GetEntriesAsync(new AuditQueryParameters
        {
            EntityType = "Client",
            EntityId = sara.Id
        });

        entries.Items.Should().OnlyContain(e => e.Summary.Contains("Sara"));
    }

    [Fact]
    public async Task AnActionWithNoSignedInUser_StillGetsAnEntry()
    {
        await _audit.RecordAsync("Client", 1, AuditAction.Updated, "Nightly status recalculation");
        await _unitOfWork.SaveChangesAsync();

        var entry = (await Entries()).Single();
        entry.ActorName.Should().BeNull("nobody did it - the system did");
        entry.Summary.Should().Be("Nightly status recalculation");
    }

    private Task<ClientDto> AddClient(
        string first = "Sara", string last = "Khoury", string phone = "03123456") =>
        _clients.CreateClientAsync(new CreateClientRequest
        {
            FirstName = first,
            LastName = last,
            PhoneNumber = phone
        }, userId: 7);

    private async Task<List<AuditEntryDto>> Entries(string? entityType = null)
    {
        var result = await _audit.GetEntriesAsync(new AuditQueryParameters { EntityType = entityType });
        return result.Items.ToList();
    }
}
