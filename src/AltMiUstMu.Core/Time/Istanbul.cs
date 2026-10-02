namespace AltMiUstMu.Core.Time;

/// <summary>Display timezone helpers. Everything is stored in UTC and converted only for display/input.</summary>
public static class Istanbul
{
    public static readonly TimeZoneInfo Zone = Resolve();

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime ToUtc(DateTime istanbulLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(istanbulLocal, DateTimeKind.Unspecified), Zone);

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(FromUtc(utcNow));

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Türkiye has used permanent UTC+3 since 2016; safe fallback for images without tzdata.
        return TimeZoneInfo.CreateCustomTimeZone("Europe/Istanbul", TimeSpan.FromHours(3), "Istanbul", "TRT");
    }
}
