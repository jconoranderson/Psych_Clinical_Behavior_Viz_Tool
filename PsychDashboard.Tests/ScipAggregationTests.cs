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
    public void MergeDeduplicatesScipWithoutCombiningDistinctShifts()
    {
        var row = new ScipRecord(new DateTime(2025, 1, 1), TimeSpan.FromHours(7), "A", 2, "Test");
        var result = WorkbookMergeService.Merge(new ParsedWorkbookResult { Scip = [row] },
            [new ParsedWorkbookResult { Scip = [row, row with { Time = TimeSpan.FromHours(15) }] }]);
        Assert.Equal(2, result.Scip.Count);
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
