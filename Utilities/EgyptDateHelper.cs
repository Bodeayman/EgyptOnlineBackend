

public static class EgyptTimeHelper
{
    private static readonly TimeZoneInfo EgyptZone =
        TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");

    // ==================== DateTime Methods ====================

    /// <summary>
    /// Gets the current date and time in Egypt's timezone (EET - Eastern European Time).
    /// </summary>
    /// <returns>
    /// A DateTime object representing the current moment in Egypt, adjusted from UTC.
    /// The returned value is offset by +2 hours (or +3 during DST) from UTC.
    /// </returns>
    /// <remarks>
    /// Use this method when you need the current time in Egypt for operations like logging, 
    /// displaying UI timestamps, or making business logic decisions based on Egypt's local time.
    /// 
    /// Example:
    /// <code>
    /// var egyptNow = EgyptTimeHelper.NowInEgypt();
    /// Console.WriteLine($"Current time in Egypt: {egyptNow}");
    /// </code>
    /// </remarks>
    public static DateTime NowInEgypt()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EgyptZone);
    }

    /// <summary>
    /// Converts a UTC DateTime to Egypt's timezone.
    /// </summary>
    /// <param name="utcDateTime">
    /// A DateTime value that must be marked as UTC (Kind == DateTimeKind.Utc).
    /// If the DateTime is not UTC, an ArgumentException is thrown to prevent silent errors.
    /// </param>
    /// <returns>
    /// The equivalent DateTime in Egypt's timezone (EET/EEST).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the input DateTime is not marked as UTC. 
    /// This validation prevents accidental conversion of unaware or local times.
    /// </exception>
    /// <remarks>
    /// This is typically used when retrieving timestamps from the database (which stores UTC)
    /// and needing to display or use them in Egypt's local time.
    /// 
    /// Example:
    /// <code>
    /// var utcTime = DateTime.UtcNow;
    /// var egyptTime = EgyptTimeHelper.ToEgyptTime(utcTime);
    /// // egyptTime is now 2 hours ahead of utcTime
    /// </code>
    /// </remarks>
    public static DateTime ToEgyptTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be in UTC", nameof(utcDateTime));
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, EgyptZone);
    }

    /// <summary>
    /// Converts an Egypt local time DateTime to UTC for database storage.
    /// </summary>
    /// <param name="egyptDateTime">
    /// A DateTime value representing a time in Egypt's timezone.
    /// This should be a local time (Kind is typically Unspecified).
    /// </param>
    /// <returns>
    /// The equivalent UTC DateTime, suitable for storing in a database.
    /// The returned DateTime will have Kind == DateTimeKind.Utc.
    /// </returns>
    /// <remarks>
    /// Use this method before saving timestamps to the database. 
    /// The database should always store UTC times for consistency and to handle DST transitions correctly.
    /// 
    /// This is the inverse operation of ToEgyptTime().
    /// 
    /// Example:
    /// <code>
    /// var egyptLocalTime = new DateTime(2026, 1, 23, 15, 30, 0); // 3:30 PM Egypt time
    /// var utcTime = EgyptTimeHelper.ToUtc(egyptLocalTime);
    /// // utcTime is now 1:30 PM UTC (assuming EET is active)
    /// await _db.SaveAsync(record with timestamp = utcTime);
    /// </code>
    /// </remarks>
    public static DateTime ToUtc(DateTime egyptDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(egyptDateTime, EgyptZone);
    }

    // ==================== DateOnly Methods ====================

    /// <summary>
    /// Gets today's date in Egypt's timezone as a DateOnly value (without time component).
    /// </summary>
    /// <returns>
    /// A DateOnly object representing today's date in Egypt, regardless of the current time.
    /// The date is extracted from the current moment in Egypt's timezone.
    /// </returns>
    /// <remarks>
    /// Use this when you need just the date portion in Egypt's timezone, particularly for:
    /// - Creating subscription start/end dates
    /// - Comparing dates in Egypt's local context
    /// - Logging date-based events
    /// - Database operations that work with date-only fields
    /// 
    /// Example:
    /// <code>
    /// var today = EgyptTimeHelper.TodayInEgypt();
    /// var subscriptionEnd = today.AddMonths(1);
    /// // If it's 11 PM UTC on Jan 23, but 1 AM in Egypt on Jan 24, 
    /// // TodayInEgypt() returns Jan 24, not Jan 23
    /// </code>
    /// </remarks>
    public static DateOnly TodayInEgypt()
    {
        return DateOnly.FromDateTime(NowInEgypt());
    }

    /// <summary>
    /// Converts a UTC DateTime to Egypt's date (without time component).
    /// </summary>
    /// <param name="utcDateTime">
    /// A UTC DateTime value to convert.
    /// </param>
    /// <returns>
    /// A DateOnly representing the date in Egypt's timezone when that UTC moment occurs.
    /// </returns>
    /// <remarks>
    /// This method first converts UTC to Egypt time, then extracts just the date part.
    /// Useful when you have a UTC timestamp from the database and need the Egypt date for comparison.
    /// 
    /// Example:
    /// <code>
    /// var utcTime = new DateTime(2026, 1, 23, 22, 0, 0, DateTimeKind.Utc); // 10 PM UTC
    /// var egyptDate = EgyptTimeHelper.ToEgyptDate(utcTime);
    /// // Result: DateOnly(2026, 1, 24) - because it's 12 AM in Egypt
    /// </code>
    /// </remarks>
    public static DateOnly ToEgyptDate(DateTime utcDateTime)
    {
        var egyptTime = ToEgyptTime(utcDateTime);
        return DateOnly.FromDateTime(egyptTime);
    }

    /// <summary>
    /// Converts an Egypt date to UTC DateTime at the start of that day (midnight).
    /// </summary>
    /// <param name="egyptDate">
    /// A DateOnly representing a date in Egypt's timezone.
    /// </param>
    /// <returns>
    /// A UTC DateTime representing midnight (00:00:00) on that Egypt date, converted to UTC.
    /// This is suitable for database queries with "starting from this date" logic.
    /// </returns>
    /// <remarks>
    /// Use this when querying for all records on or after a specific date in Egypt.
    /// It sets the time to midnight (start of the day) in Egypt, then converts to UTC.
    /// This ensures you capture everything from the beginning of that day.
    /// 
    /// Example:
    /// <code>
    /// var egyptDate = new DateOnly(2026, 1, 23);
    /// var utcStart = EgyptTimeHelper.ToUtcStartOfDay(egyptDate);
    /// // Assuming EET: utcStart = 2026-01-22 22:00:00 UTC
    /// // (because midnight in Egypt is 10 PM previous day in UTC)
    /// 
    /// var records = await _db.Records
    ///     .Where(r => r.CreatedAt >= utcStart)
    ///     .ToListAsync();
    /// </code>
    /// </remarks>
    public static DateTime ToUtcStartOfDay(DateOnly egyptDate)
    {
        var egyptDateTime = egyptDate.ToDateTime(TimeOnly.MinValue);
        return ToUtc(egyptDateTime);
    }

    /// <summary>
    /// Converts an Egypt date to UTC DateTime at the end of that day (23:59:59).
    /// </summary>
    /// <param name="egyptDate">
    /// A DateOnly representing a date in Egypt's timezone.
    /// </param>
    /// <returns>
    /// A UTC DateTime representing the last second (23:59:59) of that Egypt date, converted to UTC.
    /// This is suitable for database queries with "until end of this date" logic.
    /// </returns>
    /// <remarks>
    /// Use this when querying for all records up to and including a specific date in Egypt.
    /// It sets the time to 23:59:59 (end of the day) in Egypt, then converts to UTC.
    /// This ensures you capture everything up to the very end of that day.
    /// 
    /// Example:
    /// <code>
    /// var egyptDate = new DateOnly(2026, 1, 23);
    /// var utcEnd = EgyptTimeHelper.ToUtcEndOfDay(egyptDate);
    /// // Assuming EET: utcEnd = 2026-01-23 21:59:59 UTC
    /// // (because 23:59:59 in Egypt is 9:59:59 PM UTC same day)
    /// 
    /// var records = await _db.Records
    ///     .Where(r => r.CreatedAt <= utcEnd)
    ///     .ToListAsync();
    /// 
    /// // Combined with ToUtcStartOfDay for a date range:
    /// var allRecordsOnDate = await _db.Records
    ///     .Where(r => r.CreatedAt >= ToUtcStartOfDay(egyptDate) 
    ///              && r.CreatedAt <= ToUtcEndOfDay(egyptDate))
    ///     .ToListAsync();
    /// </code>
    /// </remarks>
    public static DateTime ToUtcEndOfDay(DateOnly egyptDate)
    {
        var egyptDateTime = egyptDate.ToDateTime(new TimeOnly(23, 59, 59));
        return ToUtc(egyptDateTime);
    }
}
// Then use:
