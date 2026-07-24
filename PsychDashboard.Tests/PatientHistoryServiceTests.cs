using Microsoft.Extensions.Configuration;
using PsychDashboard.Models;
using PsychDashboard.Services;
using Xunit;

namespace PsychDashboard.Tests;

public sealed class PatientHistoryServiceTests
{
    private readonly PatientHistoryService _service = new(
        null!,
        new ConfigurationBuilder().Build());

    [Fact]
    public async Task WeekGroupingUsesIsoWeekAcrossCalendarYear()
    {
        var records = new List<PatientHistoryService.BehaviorCsvRow>
        {
            Row(new DateTime(2024, 12, 30), 1),
            Row(new DateTime(2025, 1, 2), 2)
        };

        var result = await Aggregate(records, AggregationPeriod.Week);

        var point = Assert.Single(result);
        Assert.Equal(new DateTime(2024, 12, 30), point.Date);
        Assert.Equal(3, point.Count);
    }

    [Fact]
    public async Task RateIsEpisodesPerDistinctRecordedShift()
    {
        var records = new List<PatientHistoryService.BehaviorCsvRow>
        {
            Row(new DateTime(2025, 1, 1), 2, new TimeSpan(8, 0, 0)),
            Row(new DateTime(2025, 1, 1), 3, new TimeSpan(9, 0, 0)),
            Row(new DateTime(2025, 1, 1), 1, new TimeSpan(16, 0, 0))
        };

        var point = Assert.Single(await Aggregate(records, AggregationPeriod.Day));

        Assert.Equal(6, point.Count);
        Assert.Equal(3, point.Rate);
    }

    [Fact]
    public async Task AggregatesAllIntensityAndDurationBuckets()
    {
        var row = Row(new DateTime(2025, 2, 1), 21);
        row.Intensity_01_Count = 1;
        row.Intensity_02_Count = 2;
        row.Intensity_03_Count = 3;
        row.Intensity_04_Count = 4;
        row.Intensity_05_Count = 5;
        row.Duration_01_Count = 1;
        row.Duration_02_Count = 2;
        row.Duration_03_Count = 3;
        row.Duration_04_Count = 4;
        row.Duration_05_Count = 5;
        row.Duration_06_Count = 6;

        var point = Assert.Single(await Aggregate(new List<PatientHistoryService.BehaviorCsvRow> { row }, AggregationPeriod.Month));

        Assert.Equal(new double[] { 1, 2, 3, 4, 5 }, point.IntensityBuckets);
        Assert.Equal(new double[] { 1, 2, 3, 4, 5, 6 }, point.DurationBuckets);
        Assert.Equal(21, point.TotalDuration);
        Assert.Equal(55d / 15d, point.AverageIntensity, 10);
    }

    private async Task<List<PsychDashboard.ViewModels.DailyBehaviorCount>> Aggregate(
        List<PatientHistoryService.BehaviorCsvRow> rows,
        AggregationPeriod period)
    {
        var model = await _service.GetPatientHistoryFromRecordsAsync(
            rows,
            new List<PsychDashboard.Models.Medication>(),
            period);
        return model.DailyBehaviorCounts;
    }

    private static PatientHistoryService.BehaviorCsvRow Row(
        DateTime date,
        double count,
        TimeSpan? time = null) =>
        new()
        {
            Person_ID = "resident-1",
            Date = date,
            Time = time ?? new TimeSpan(8, 0, 0),
            Target = "Aggression",
            Episode_Count = count
        };
}
