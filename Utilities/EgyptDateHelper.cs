

public static class EgyptTimeHelper
{
    private static readonly TimeZoneInfo EgyptZone =
        TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

    // ==================== DateTime Methods ====================

    /// <summary>
    /// Gets current DateTime in Egypt timezone
    /// </summary>
    public static DateTime NowInEgypt()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EgyptZone);
    }

    /// <summary>
    /// Converts UTC DateTime to Egypt timezone
    /// </summary>
    public static DateTime ToEgyptTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be in UTC", nameof(utcDateTime));
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, EgyptZone);
    }

    /// <summary>
    /// Converts Egypt DateTime to UTC (for saving to database)
    /// </summary>
    public static DateTime ToUtc(DateTime egyptDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(egyptDateTime, EgyptZone);
    }

    // ==================== DateOnly Methods ====================

    /// <summary>
    /// Gets today's date in Egypt timezone
    /// </summary>
    public static DateOnly TodayInEgypt()
    {
        return DateOnly.FromDateTime(NowInEgypt());
    }

    /// <summary>
    /// Converts UTC DateTime to Egypt DateOnly
    /// </summary>
    public static DateOnly ToEgyptDate(DateTime utcDateTime)
    {
        var egyptTime = ToEgyptTime(utcDateTime);
        return DateOnly.FromDateTime(egyptTime);
    }

    /// <summary>
    /// Converts DateOnly (assumed to be Egypt date) to UTC DateTime at start of day
    /// </summary>
    public static DateTime ToUtcStartOfDay(DateOnly egyptDate)
    {
        var egyptDateTime = egyptDate.ToDateTime(TimeOnly.MinValue);
        return ToUtc(egyptDateTime);
    }

    /// <summary>
    /// Converts DateOnly (assumed to be Egypt date) to UTC DateTime at end of day
    /// </summary>
    public static DateTime ToUtcEndOfDay(DateOnly egyptDate)
    {
        var egyptDateTime = egyptDate.ToDateTime(new TimeOnly(23, 59, 59));
        return ToUtc(egyptDateTime);
    }
}
// Then use:
