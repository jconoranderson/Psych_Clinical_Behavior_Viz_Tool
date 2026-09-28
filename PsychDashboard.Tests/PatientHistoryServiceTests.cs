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
    public async Task WeekGroupingUsesSevenDaySegmentsFromFirstAvailableDate()
    {
        var records = new List<PatientHistoryService.BehaviorCsvRow>
        {
            Row(new DateTime(2024, 12, 31), 1),
            Row(new DateTime(2025, 1, 6), 2),
            Row(new DateTime(2025, 1, 7), 3),
            Row(new DateTime(2025, 1, 9), 4)
        };

        var result = await Aggregate(records, AggregationPeriod.Week);

        Assert.Collection(result,
            first =>
            {
                Assert.Equal(new DateTime(2024, 12, 31), first.Date);
                Assert.Equal(new DateTime(2025, 1, 6), first.PeriodEnd);
                Assert.Equal(3, first.Count);
            },
            last =>
            {
                Assert.Equal(new DateTime(2025, 1, 7), last.Date);
                Assert.Equal(new DateTime(2025, 1, 9), last.PeriodEnd);
                Assert.Equal(7, last.Count);
            });
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

    [Fact]
    public async Task ExplicitZeroIsRetainedAsAValidDataPoint()
    {
        var point = Assert.Single(await Aggregate(
            new List<PatientHistoryService.BehaviorCsvRow>
            {
                Row(new DateTime(2025, 3, 1), 0)
            },
            AggregationPeriod.Month));

        Assert.Equal(0, point.Count);
        Assert.True(point.HasData);
    }

    [Fact]
    public async Task NoDataAndLoaRowsDoNotExtendBehaviorSeries()
    {
        var recorded = Row(new DateTime(2025, 3, 1), 0);
        var noData = Row(new DateTime(2025, 4, 1), 0);
        noData.Behavior_No_Data_Recorded = true;
        var loa = Row(new DateTime(2025, 5, 1), 0);
        loa.Behavior_LOA = true;

        var points = await Aggregate(
            new List<PatientHistoryService.BehaviorCsvRow> { recorded, noData, loa },
            AggregationPeriod.Month);

        var point = Assert.Single(points);
        Assert.Equal(new DateTime(2025, 3, 1), point.Date);
    }

    [Fact]
    public async Task MedicationDataEndsAtLastBehaviorObservation()
    {
        var lastBehaviorDate = new DateTime(2025, 3, 15);
        var model = await _service.GetPatientHistoryFromRecordsAsync(
            new List<PatientHistoryService.BehaviorCsvRow>
            {
                Row(lastBehaviorDate, 1)
            },
            new List<Medication>
            {
                new()
                {
                    Name = "Test Medication",
                    Dose = "10",
                    Unit = "mg",
                    StartDate = new DateTime(2025, 3, 1),
                    EndDate = new DateTime(2025, 12, 31)
                }
            },
            AggregationPeriod.Month);

        Assert.NotEmpty(model.UnreducedMedications);
        Assert.Equal(lastBehaviorDate, model.UnreducedMedications.Max(item => item.Date));
    }

    [Fact]
    public async Task EmptyShiftSelectionReturnsNoObservationsButNullIncludesAll()
    {
        var rows = new List<PatientHistoryService.BehaviorCsvRow> { Row(new DateTime(2025, 1, 1), 2) };
        var empty = await _service.GetPatientHistoryFromRecordsAsync(rows, [], AggregationPeriod.Day, new HashSet<string>());
        var all = await _service.GetPatientHistoryFromRecordsAsync(rows, [], AggregationPeriod.Day);
        Assert.Empty(empty.DailyBehaviorCounts);
        Assert.Single(all.DailyBehaviorCounts);
        Assert.NotEmpty(empty.AvailableTargets);
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
