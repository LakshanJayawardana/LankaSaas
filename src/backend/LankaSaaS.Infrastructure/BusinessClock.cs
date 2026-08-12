namespace LankaSaaS.Infrastructure;

public static class BusinessClock
{
    // LankaSaaS is currently LKR-first and uses Sri Lanka's year-round UTC+05:30 business date.
    public static DateOnly Today => FromInstant(DateTimeOffset.UtcNow);
    public static DateOnly FromInstant(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime.AddHours(5.5));
}
