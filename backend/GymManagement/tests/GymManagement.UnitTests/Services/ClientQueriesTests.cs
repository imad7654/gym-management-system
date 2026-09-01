using FluentAssertions;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// Membership status is now worked out in two places: <see cref="Client.StatusFrom"/> in
/// memory, and <see cref="ClientQueries"/> as a database query. Two copies of a rule is a
/// risk, and this file is the reason it is an acceptable one - every case is driven through
/// both and they have to give the same answer.
///
/// If they ever disagreed, the member list would call someone Expired while the door called
/// them Active, which is precisely the confusion that deriving the status was meant to end.
/// </summary>
public class ClientQueriesTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;

    public ClientQueriesTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"client-queries-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private Client Add(string name, int? startOffset, int? endOffset, bool suspended = false)
    {
        var client = new Client
        {
            FirstName = name,
            LastName = "Member",
            PhoneNumber = "03000000",
            IsSuspended = suspended,
            MembershipStartDate = startOffset.HasValue
                ? Today.AddDays(startOffset.Value).ToDateTime(TimeOnly.MinValue)
                : null,
            MembershipEndDate = endOffset.HasValue
                ? Today.AddDays(endOffset.Value).ToDateTime(TimeOnly.MinValue)
                : null
        };

        _context.Clients.Add(client);
        _context.SaveChanges();
        return client;
    }

    /// <summary>
    /// One member per situation, each asked both ways. The offsets are days either side of
    /// today, and the expiring window is seven days.
    /// </summary>
    [Theory]
    [InlineData("never paid", null, null, false, MembershipStatus.Pending)]
    [InlineData("starts next week", 5, 35, false, MembershipStatus.Pending)]
    [InlineData("mid term", -20, 30, false, MembershipStatus.Active)]
    [InlineData("just outside the window", -20, 8, false, MembershipStatus.Active)]
    [InlineData("on the window edge", -20, 7, false, MembershipStatus.Expiring)]
    [InlineData("last day", -29, 0, false, MembershipStatus.Expiring)]
    [InlineData("ran out yesterday", -30, -1, false, MembershipStatus.Expired)]
    [InlineData("ran out long ago", -400, -370, false, MembershipStatus.Expired)]
    [InlineData("frozen mid term", -20, 30, true, MembershipStatus.Suspended)]
    [InlineData("frozen and lapsed", -400, -370, true, MembershipStatus.Suspended)]
    public void TheDatabaseAndTheEntityAgree(
        string name, int? startOffset, int? endOffset, bool suspended, MembershipStatus expected)
    {
        var client = Add(name, startOffset, endOffset, suspended);

        client.MembershipStatusOn(Today).Should().Be(expected,
            "the in-memory rule decides what the desk and the door see");

        _context.Clients.WithStatus(expected, Today).Select(c => c.Id).ToList()
            .Should().Contain(client.Id,
                "the database has to put the member in the same bucket the entity does");

        foreach (var other in Enum.GetValues<MembershipStatus>().Where(s => s != expected))
        {
            _context.Clients.WithStatus(other, Today).Select(c => c.Id).ToList()
                .Should().NotContain(client.Id,
                    $"a member cannot be both {expected} and {other}");
        }
    }

    [Fact]
    public void EveryMemberLandsInExactlyOneStatus()
    {
        Add("never paid", null, null);
        Add("future start", 5, 35);
        Add("mid term", -20, 30);
        Add("expiring", -20, 3);
        Add("expired", -30, -1);
        Add("frozen", -20, 30, suspended: true);

        var counted = Enum.GetValues<MembershipStatus>()
            .Sum(status => _context.Clients.WithStatus(status, Today).Count());

        counted.Should().Be(_context.Clients.Count(),
            "the buckets must partition the members - no one counted twice, no one missed");
    }

    [Fact]
    public void AllowedIn_TakesExpiringMembersToo()
    {
        var active = Add("mid term", -20, 30);
        var expiring = Add("last week", -25, 3);
        var lastDay = Add("last day", -29, 0);
        Add("expired", -30, -1);
        Add("frozen", -20, 30, suspended: true);
        Add("never paid", null, null);

        var allowed = _context.Clients.AllowedIn(Today).Select(c => c.Id).ToList();

        allowed.Should().BeEquivalentTo(new[] { active.Id, expiring.Id, lastDay.Id },
            "a member in their last week is entitled to train, and the end date is inclusive");
    }

    [Fact]
    public void ExpiringWithin_IsTheCallSheetAndExcludesThoseAlreadyGone()
    {
        var soon = Add("three days left", -27, 3);
        var today = Add("last day", -29, 0);
        Add("plenty of time", -20, 40);
        Add("already expired", -30, -1);
        Add("frozen but expiring", -25, 3, suspended: true);

        var expiring = _context.Clients.ExpiringWithin(7, Today).Select(c => c.Id).ToList();

        expiring.Should().BeEquivalentTo(new[] { soon.Id, today.Id },
            "an expired member is not a renewal to chase, and a frozen one is not due yet");
    }

    [Fact]
    public void StatusRank_SortsInTheOrderOfTheEnum()
    {
        Add("expired", -30, -1);
        Add("frozen", -20, 30, suspended: true);
        Add("mid term", -20, 30);
        Add("never paid", null, null);
        Add("expiring", -20, 3);

        var order = _context.Clients
            .OrderBy(ClientQueries.StatusRank(Today))
            .Select(c => c.FirstName)
            .ToList();

        order.Should().Equal(
            new[] { "never paid", "mid term", "expiring", "expired", "frozen" },
            "sorting by status should follow Pending, Active, Expiring, Expired, Suspended - "
            + "the old stored column was text and sorted alphabetically instead");
    }
}
