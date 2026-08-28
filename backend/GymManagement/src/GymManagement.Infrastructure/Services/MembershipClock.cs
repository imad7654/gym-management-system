using GymManagement.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GymManagement.Infrastructure.Services;

/// <summary>
/// Resolves "today" in the gym's timezone rather than the server's, so a server rented in
/// another region cannot shift when memberships expire.
/// </summary>
public class MembershipClock : IMembershipClock
{
    /// <summary>
    /// IANA id. .NET resolves these on Windows as well as Linux, so the same configuration
    /// value works on a developer laptop and on the deployed server.
    /// </summary>
    private const string DefaultTimeZoneId = "Asia/Beirut";

    private readonly TimeZoneInfo _timeZone;

    public MembershipClock(IConfiguration configuration, ILogger<MembershipClock> logger)
    {
        var configured = configuration["Gym:TimeZone"];
        var timeZoneId = string.IsNullOrWhiteSpace(configured) ? DefaultTimeZoneId : configured;

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Falling back to UTC is wrong by up to a day, so say so loudly rather than
            // letting memberships quietly expire on the wrong date.
            logger.LogError(
                ex,
                "Gym:TimeZone '{TimeZoneId}' could not be resolved. Falling back to UTC, which "
                + "will make membership dates roll over at the wrong time of day. Set a valid "
                + "IANA timezone id such as {Default}.",
                timeZoneId, DefaultTimeZoneId);

            _timeZone = TimeZoneInfo.Utc;
        }
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone));

    public (DateTime StartUtc, DateTime EndUtc) DayBoundsUtc(DateOnly date) =>
        (ToUtc(date.ToDateTime(TimeOnly.MinValue)),
         ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue)));

    /// <summary>
    /// Converts a gym-local wall-clock time to the UTC instant it happened at.
    ///
    /// The awkward case is the spring clock change. Lebanon moves its clocks at midnight,
    /// so on that one night local midnight never happens - and it is exactly the boundary a
    /// day report asks for. ConvertTimeToUtc throws on a time that does not exist, which
    /// would take the takings report down twice a year, so the first instant that did
    /// happen is used instead.
    /// </summary>
    private DateTime ToUtc(DateTime local)
    {
        if (_timeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        // An ambiguous time - the autumn hour that happens twice - resolves to standard
        // time, which is the earlier of the two. That is the right edge for a day boundary:
        // it keeps the day whole rather than clipping an hour off its start.
        return TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
    }
}
