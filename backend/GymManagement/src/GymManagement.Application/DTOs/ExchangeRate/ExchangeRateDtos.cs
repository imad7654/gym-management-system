using System.ComponentModel.DataAnnotations;

namespace GymManagement.Application.DTOs.ExchangeRate;

/// <summary>
/// The rate the payment form should offer, plus enough context for the desk to notice when
/// it is out of date.
/// </summary>
public class ExchangeRateDto
{
    public decimal Rate { get; set; }

    /// <summary>The day this rate was set for, in the gym's timezone.</summary>
    public DateTime EffectiveDate { get; set; }

    /// <summary>
    /// How many days old the rate is. Zero means it was set today.
    ///
    /// Surfaced rather than left for the browser to work out, because the browser's idea of
    /// today is the machine's timezone, and the whole point of the gym clock is that those
    /// two disagree for part of every day.
    /// </summary>
    public int DaysOld { get; set; }

    /// <summary>True when the rate was not set today. The desk should be warned, not blocked.</summary>
    public bool IsStale { get; set; }
}

public class SetExchangeRateRequest
{
    /// <summary>How many LBP to one USD.</summary>
    [Required]
    [Range(0.01, 100_000_000)]
    public decimal Rate { get; set; }
}
