namespace DevSync.Domain.Revision;

public sealed record RevisionSchedule(DateTimeOffset DueAt, int IntervalDays)
{
    // This is DevSync's simple deterministic schedule, not a standardized learning algorithm.
    // An Again grade resets the interval; the next successful review starts a new interval.
    public static RevisionSchedule Next(int previousIntervalDays, RevisionGrade grade, DateTimeOffset reviewedAt)
    {
        if (previousIntervalDays is < 0 or > 365)
            throw new ArgumentOutOfRangeException(nameof(previousIntervalDays));
        if (!Enum.IsDefined(grade)) throw new ArgumentOutOfRangeException(nameof(grade));
        var utc = reviewedAt.ToUniversalTime();
        if (grade == RevisionGrade.Again) return new(utc.AddMinutes(10), 0);
        var days = grade switch
        {
            RevisionGrade.Hard => previousIntervalDays == 0 ? 1 : Math.Max(1, (int)Math.Ceiling(previousIntervalDays * 1.2m)),
            RevisionGrade.Good => previousIntervalDays == 0 ? 3 : Math.Max(3, previousIntervalDays * 2),
            RevisionGrade.Easy => previousIntervalDays == 0 ? 7 : Math.Max(7, previousIntervalDays * 3),
            _ => throw new ArgumentOutOfRangeException(nameof(grade))
        };
        days = Math.Min(days, 365);
        return new(utc.AddDays(days), days);
    }
}
