using PsychDashboard.Models;
using PsychDashboard.Services;
using PsychDashboard.ViewModels;
using Xunit;

namespace PsychDashboard.Tests;

public class ScipAggregationTests
{
    [Theory]
    [InlineData(AggregationPeriod.Day, 2)]
    [InlineData(AggregationPeriod.Week, 5)]
    [InlineData(AggregationPeriod.Month, 9)]
    [InlineData(AggregationPeriod.Average, 9)]
    public void TotalsUsePeriodBoundariesAndSelectedShifts(AggregationPeriod period, int expected)
    {
        var start = new DateTime(2025, 1, 1);
        var records = new List<ScipRecord> {
            new(start, TimeSpan.FromHours(7), "A", 2, "Test"),
            new(start.AddDays(5), TimeSpan.FromHours(7), "A", 3, "Test"),
            new(start.AddDays(30), TimeSpan.FromHours(7), "A", 4, "Test"),
            new(start, TimeSpan.FromHours(15), "A", 99, "Test"),
            new(start.AddDays(31), TimeSpan.FromHours(7), "A", 88, "Test")
        };
        var point = Assert.Single(ScipAggregationService.Aggregate(records, "A", period,
            [new DailyBehaviorCount { Date = start, PeriodEnd = start.AddDays(6) }],
            ["7-3"], start, start.AddDays(30)));
        Assert.Equal(expected, point.Count);
        Assert.True(point.HasData);
    }

    [Fact]
    public void ScipWeekdaysMatchBehaviorFiltersAndPdfTotals()
    {
        var monday = new DateTime(2024, 7, 1);
        ScipRecord[] rows = [new(monday, TimeSpan.FromHours(7), "A", 2, "Test", 106),
            new(monday.AddDays(1), TimeSpan.FromHours(7), "A", 9, "Test", 106)];
        var points = ScipAggregationService.Aggregate(rows, "A", AggregationPeriod.Month,
            [new() { Date = monday }], ["7-3"], selectedDays: [DayOfWeek.Monday]);
        Assert.Equal(2, Assert.Single(points).Count);
        Assert.Equal(2, ScipAggregationService.InRange(rows, monday, monday.AddDays(30), ["7-3"], [DayOfWeek.Monday]).Sum(row => row.Count));
        Assert.False(ScipAggregationService.HasPositiveCount(rows, "A", monday, monday.AddDays(30), ["7-3"], [DayOfWeek.Wednesday]));
    }

    [Fact]
    public void DefaultVisibilityRequiresPositiveCountWithinDatesAndShifts()
    {
        var date = new DateTime(2025, 1, 1);
        ScipRecord[] rows = [new(date, TimeSpan.FromHours(7), "A", 0, "Test", 106),
            new(date.AddMonths(1), TimeSpan.FromHours(15), "A", 2, "Test", 106)];
        Assert.False(ScipAggregationService.HasPositiveCount(rows, "A", date, date.AddMonths(1).AddDays(-1), null));
        Assert.False(ScipAggregationService.HasPositiveCount(rows, "A", date, date.AddMonths(2), ["7-3"]));
        Assert.True(ScipAggregationService.HasPositiveCount(rows, "A", date, date.AddMonths(2), ["3-11"]));
        Assert.False(ScipAggregationService.HasPositiveCount(rows, "B", date, date.AddMonths(2), null));
    }

    [Fact]
    public void MergeDeduplicatesScipWithoutCombiningDistinctShifts()
    {
        var row = new ScipRecord(new DateTime(2025, 1, 1), TimeSpan.FromHours(7), "A", 2, "Test");
        var result = WorkbookMergeService.Merge(new ParsedWorkbookResult { Scip = [row] },
            [new ParsedWorkbookResult { Scip = [row, row with { SourceColumn = 106 }, row with { Time = TimeSpan.FromHours(15) }] }]);
        Assert.Equal(2, result.Scip.Count);
        Assert.Equal(106, result.Scip.Single(record => record.Time == TimeSpan.FromHours(7)).SourceColumn);
    }

    [Fact]
    public void RecordedZeroIsDifferentFromMissingPeriod()
    {
        var start = new DateTime(2025, 1, 1);
        var points = ScipAggregationService.Aggregate(
            [new ScipRecord(start, TimeSpan.FromHours(7), "A", 0, "Test")], "A", AggregationPeriod.Day,
            [new() { Date = start }, new() { Date = start.AddDays(1) }], ["7-3"]);
        Assert.True(points[0].HasData);
        Assert.Equal(0, points[0].Count);
        Assert.False(points[1].HasData);
    }
}
