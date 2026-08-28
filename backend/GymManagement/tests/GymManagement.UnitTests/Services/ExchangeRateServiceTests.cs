using FluentAssertions;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ExchangeRateEntity = GymManagement.Domain.Entities.ExchangeRate;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// Today's LBP rate - the number the owner sets each morning and the payment form offers
/// all day.
///
/// It is only ever a default. A payment records the rate it was actually converted at, so
/// nothing here can restate money already taken; these tests are about the desk being
/// offered the right number, and being told when that number is old.
/// </summary>
public class ExchangeRateServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly ExchangeRateService _service;

    public ExchangeRateServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"rates-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _service = new ExchangeRateService(_unitOfWork, new FixedClock(Today));
    }

    public void Dispose() => _unitOfWork.Dispose();

    [Fact]
    public async Task GetCurrent_BeforeTheOwnerHasEverSetARate_IsNullRatherThanZero()
    {
        // Zero would sail into a division and turn a payment into nonsense. Null means
        // "ask reception to type one", which is the honest answer.
        (await _service.GetCurrentAsync()).Should().BeNull();
    }

    [Fact]
    public async Task SetTodaysRate_IsOfferedBackImmediatelyAndIsNotStale()
    {
        await _service.SetTodaysRateAsync(89500m, userId: 1);

        var current = await _service.GetCurrentAsync();

        current!.Rate.Should().Be(89500m);
        current.DaysOld.Should().Be(0);
        current.IsStale.Should().BeFalse();
        current.EffectiveDate.Should().Be(Today.ToDateTime(TimeOnly.MinValue));
    }

    [Fact]
    public async Task SetTodaysRate_Twice_CorrectsTheDayRatherThanAddingASecondRate()
    {
        await _service.SetTodaysRateAsync(8950m, userId: 1);
        await _service.SetTodaysRateAsync(89500m, userId: 1);

        // A day with two rates is a day whose takings cannot be checked against the drawer.
        _context.ExchangeRates.Should().HaveCount(1);
        (await _service.GetCurrentAsync())!.Rate.Should().Be(89500m);
    }

    [Fact]
    public async Task GetCurrent_WhenTheRateIsFromAnEarlierDay_IsStillOfferedButFlaggedStale()
    {
        _context.ExchangeRates.Add(new ExchangeRateEntity
        {
            EffectiveDate = Today.AddDays(-3).ToDateTime(TimeOnly.MinValue),
            Rate = 89000m
        });
        await _context.SaveChangesAsync();

        var current = await _service.GetCurrentAsync();

        // Withheld, reception invents a number under pressure. Offered with a warning,
        // they can see it is old and check.
        current!.Rate.Should().Be(89000m);
        current.DaysOld.Should().Be(3);
        current.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrent_PrefersTheMostRecentRate()
    {
        _context.ExchangeRates.AddRange(
            new ExchangeRateEntity
            {
                EffectiveDate = Today.AddDays(-5).ToDateTime(TimeOnly.MinValue), Rate = 88000m
            },
            new ExchangeRateEntity
            {
                EffectiveDate = Today.AddDays(-1).ToDateTime(TimeOnly.MinValue), Rate = 89500m
            });
        await _context.SaveChangesAsync();

        (await _service.GetCurrentAsync())!.Rate.Should().Be(89500m);
    }

    [Fact]
    public async Task GetCurrent_IgnoresARateDatedInTheFuture()
    {
        _context.ExchangeRates.AddRange(
            new ExchangeRateEntity
            {
                EffectiveDate = Today.ToDateTime(TimeOnly.MinValue), Rate = 89500m
            },
            new ExchangeRateEntity
            {
                EffectiveDate = Today.AddDays(2).ToDateTime(TimeOnly.MinValue), Rate = 95000m
            });
        await _context.SaveChangesAsync();

        // A future-dated row is a mistyped date. Using it would convert today's cash at a
        // number nobody agreed to.
        (await _service.GetCurrentAsync())!.Rate.Should().Be(89500m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SetTodaysRate_RejectsARateThatIsNotPositive(decimal rate)
    {
        var act = () => _service.SetTodaysRateAsync(rate, userId: 1);

        await act.Should().ThrowAsync<BusinessException>();
        _context.ExchangeRates.Should().BeEmpty();
    }

}
