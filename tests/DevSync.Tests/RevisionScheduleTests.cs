using DevSync.Domain.Revision;

namespace DevSync.Tests;

public sealed class RevisionScheduleTests
{
    [Theory]
    [InlineData(0, RevisionGrade.Hard, 1)]
    [InlineData(0, RevisionGrade.Good, 3)]
    [InlineData(0, RevisionGrade.Easy, 7)]
    [InlineData(1, RevisionGrade.Hard, 2)]
    [InlineData(1, RevisionGrade.Good, 3)]
    [InlineData(1, RevisionGrade.Easy, 7)]
    [InlineData(3, RevisionGrade.Hard, 4)]
    [InlineData(3, RevisionGrade.Good, 6)]
    [InlineData(3, RevisionGrade.Easy, 9)]
    [InlineData(200, RevisionGrade.Hard, 240)]
    [InlineData(200, RevisionGrade.Good, 365)]
    [InlineData(200, RevisionGrade.Easy, 365)]
    [InlineData(365, RevisionGrade.Hard, 365)]
    [InlineData(365, RevisionGrade.Good, 365)]
    [InlineData(365, RevisionGrade.Easy, 365)]
    public void Successful_grades_round_up_apply_minimums_and_cap_at_one_year(int previousDays, RevisionGrade grade, int expectedDays)
    {
        var now = new DateTimeOffset(2026, 10, 7, 12, 30, 0, TimeSpan.Zero);
        var schedule = RevisionSchedule.Next(previousDays, grade, now);
        Assert.Equal(expectedDays, schedule.IntervalDays);
        Assert.Equal(now.AddDays(expectedDays), schedule.DueAt);
    }

    [Fact]
    public void Again_resets_interval_and_schedules_ten_minutes_from_the_review_in_UTC()
    {
        var local = new DateTimeOffset(2026, 10, 7, 0, 5, 0, TimeSpan.FromHours(5.5));
        var schedule = RevisionSchedule.Next(200, RevisionGrade.Again, local);
        Assert.Equal(0, schedule.IntervalDays);
        Assert.Equal(TimeSpan.Zero, schedule.DueAt.Offset);
        Assert.Equal(local.ToUniversalTime().AddMinutes(10), schedule.DueAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 18, 45, 0, TimeSpan.Zero), schedule.DueAt);
        Assert.Equal(3, RevisionSchedule.Next(schedule.IntervalDays, RevisionGrade.Good, schedule.DueAt).IntervalDays);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Invalid_intervals_are_rejected(int days) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RevisionSchedule.Next(days, RevisionGrade.Good, DateTimeOffset.UtcNow));

    [Fact]
    public void Invalid_grade_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RevisionSchedule.Next(0, (RevisionGrade)99, DateTimeOffset.UtcNow));
}
