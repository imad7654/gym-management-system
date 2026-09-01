using FluentAssertions;
using GymManagement.Application.Services;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// Proves the membership status queries survive being turned into MySQL.
///
/// The other tests in this folder run against EF's in-memory provider, which executes LINQ
/// as ordinary C# and so cannot tell a translatable query from an untranslatable one. That
/// matters here: the status rule became a chain of conditionals and date comparisons, and a
/// query EF cannot translate does not fail at build time - it throws the first time a real
/// user opens the page.
///
/// <c>ToQueryString</c> forces the full translation and throws if any part of it cannot be
/// expressed as SQL, without needing a database to connect to. That is the whole check; the
/// SQL text itself is not asserted on, because pinning generated SQL makes a brittle test
/// that breaks on every provider upgrade.
/// </summary>
public class ClientQueriesTranslateToSqlTests
{
    /// <summary>
    /// A context on the real MySQL provider. Nothing connects - building the query text is
    /// entirely offline - so the connection string only has to parse.
    /// </summary>
    private static ApplicationDbContext MySqlContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "server=localhost;database=gymdb;user=none;password=none",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static readonly DateOnly Today = new(2026, 8, 28);

    [Fact]
    public void AllowedIn_Translates()
    {
        using var context = MySqlContext();

        var sql = context.Clients.AllowedIn(Today).ToQueryString();

        sql.Should().Contain("SELECT", "the door's entitlement check has to run in the database");
    }

    [Fact]
    public void ExpiringWithin_Translates()
    {
        using var context = MySqlContext();

        var sql = context.Clients.ExpiringWithin(7, Today).ToQueryString();

        sql.Should().Contain("SELECT");
    }

    [Theory]
    [InlineData(MembershipStatus.Pending)]
    [InlineData(MembershipStatus.Active)]
    [InlineData(MembershipStatus.Expiring)]
    [InlineData(MembershipStatus.Expired)]
    [InlineData(MembershipStatus.Suspended)]
    public void WithStatus_TranslatesForEveryStatus(MembershipStatus status)
    {
        using var context = MySqlContext();

        var sql = context.Clients.WithStatus(status, Today).ToQueryString();

        sql.Should().Contain("SELECT", $"filtering the member list by {status} has to reach SQL");
    }

    /// <summary>
    /// The riskiest one. Sorting by status is a chain of conditionals that has to become a
    /// CASE expression; if EF gave up on it, the query would fall back to client evaluation
    /// or throw, and sorting a member list is not something to discover broken at the desk.
    /// </summary>
    [Fact]
    public void StatusRank_TranslatesToACaseExpression()
    {
        using var context = MySqlContext();

        var sql = context.Clients
            .OrderBy(ClientQueries.StatusRank(Today))
            .ToQueryString();

        sql.Should().Contain("CASE", "the status order must be computed by the database, not in memory");
        sql.Should().Contain("ORDER BY");
    }
}
