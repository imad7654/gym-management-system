using GymManagement.Application.DTOs.ExchangeRate;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IExchangeRateService
{
    /// <summary>
    /// The rate the payment form should offer, or null if the owner has never set one.
    /// </summary>
    Task<ExchangeRateDto?> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets - or corrects - today's rate.</summary>
    Task<ExchangeRateDto> SetTodaysRateAsync(decimal rate, int? userId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Today's LBP rate, which the owner sets each morning and the desk uses all day.
///
/// This only ever produces a *default* for the payment form. A payment stores the rate it
/// was actually converted at, and nothing here recalculates it - so correcting the rate at
/// noon cannot restate the money taken in the morning.
/// </summary>
public class ExchangeRateService : IExchangeRateService
{
    /// <summary>
    /// An older rate is still offered rather than withheld: in Lebanon a rate a few days old
    /// is usually closer to right than whatever reception invents under pressure. Past this,
    /// the desk is warned it is stale.
    /// </summary>
    private const int StaleAfterDays = 1;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipClock _clock;
    private readonly IAuditService _audit;

    public ExchangeRateService(IUnitOfWork unitOfWork, IMembershipClock clock, IAuditService audit)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _audit = audit;
    }

    public async Task<ExchangeRateDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var todayAsDate = today.ToDateTime(TimeOnly.MinValue);

        // The most recent rate not in the future. A rate dated ahead of today would be a
        // mistake, and offering it would convert today's cash at tomorrow's number.
        var latest = await _unitOfWork.ExchangeRates.Query()
            .Where(r => r.EffectiveDate <= todayAsDate)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest == null) return null;

        var daysOld = today.DayNumber - DateOnly.FromDateTime(latest.EffectiveDate).DayNumber;

        return new ExchangeRateDto
        {
            Rate = latest.Rate,
            EffectiveDate = latest.EffectiveDate,
            DaysOld = daysOld,
            IsStale = daysOld >= StaleAfterDays
        };
    }

    public async Task<ExchangeRateDto> SetTodaysRateAsync(
        decimal rate, int? userId = null, CancellationToken cancellationToken = default)
    {
        if (rate <= 0)
        {
            throw new BusinessException("The exchange rate has to be more than zero.");
        }

        var today = _clock.Today;
        var todayAsDate = today.ToDateTime(TimeOnly.MinValue);

        var existing = await _unitOfWork.ExchangeRates.Query()
            .FirstOrDefaultAsync(r => r.EffectiveDate == todayAsDate, cancellationToken);

        // Read before the overwrite below, so the trail can say what the rate was changed
        // from rather than repeating what it was changed to.
        var previousRate = existing?.Rate;

        if (existing != null)
        {
            // Correcting a typo made this morning, not adding a second rate for the same
            // day - the unique index on EffectiveDate would refuse that anyway, and a day
            // with two rates is a day whose takings cannot be checked.
            existing.Rate = rate;
            existing.SetBy = userId;
        }
        else
        {
            await _unitOfWork.ExchangeRates.AddAsync(new Domain.Entities.ExchangeRate
            {
                EffectiveDate = todayAsDate,
                Rate = rate,
                SetBy = userId
            }, cancellationToken);
        }

        // Audited because the rate decides what every LBP payment that day is worth in the
        // books. A rate typed wrong and corrected an hour later leaves payments behind it.
        await _audit.RecordAsync(
            "ExchangeRate", null,
            previousRate.HasValue ? AuditAction.Updated : AuditAction.Created,
            $"Set today's rate to {rate:#,##0} LBP per USD",
            previousRate.HasValue ? $"Corrected from {previousRate.Value:#,##0}." : null,
            userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ExchangeRateDto
        {
            Rate = rate,
            EffectiveDate = todayAsDate,
            DaysOld = 0,
            IsStale = false
        };
    }
}
