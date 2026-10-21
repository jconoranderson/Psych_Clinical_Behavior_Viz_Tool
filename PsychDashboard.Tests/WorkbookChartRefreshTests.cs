using System.Reflection;
using Microsoft.Extensions.Configuration;
using PsychDashboard.Components.Pages;
using PsychDashboard.Components.Shared;
using PsychDashboard.Models;
using PsychDashboard.Services;
using PsychDashboard.ViewModels;
using Xunit;

namespace PsychDashboard.Tests;

public sealed class WorkbookChartRefreshTests
{
    [Theory]
    [InlineData(2023, AggregationPeriod.Month)]
    [InlineData(2025, AggregationPeriod.Month)]
    [InlineData(2023, AggregationPeriod.Week)]
    [InlineData(2025, AggregationPeriod.Week)]
    public async Task AdditionalWorkbookIsFullyPlottedBeforeSelectorRerenders(
        int addedYear, AggregationPeriod period)
    {
        using var page = new Home();
        typeof(Home).GetProperty("PatientHistoryService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new PatientHistoryService(null!, new ConfigurationBuilder().Build()));
        Set(page, "_selectedPeriod", period);
        Set(page, "_parsedWorkbook", Workbook(2024, "Original"));
        await LoadDefaultWindow(page, false);

        // Keep the child parameters at the first upload's values, as they are
        // while the parent's upload handler builds the next chart options.
        var selector = new VariableSelector();
        typeof(VariableSelector).GetProperty("AvailableMedications")!
            .SetValue(selector, new List<string> { "Original", "Hidden" });
        typeof(VariableSelector).GetProperty("AvailableTargets")!
            .SetValue(selector, new List<string> { "Original" });
        Get<HashSet<string>>(selector, "_hiddenMedications").Add("Hidden");
        selector.GetTargetState("Hidden").Visible = false;
        Set(page, "_variableSelector", selector);

        Set(page, "_parsedWorkbook", WorkbookMergeService.Merge(
            Workbook(2024, "Original"),
            new[] { Workbook(addedYear, "Added"), Workbook(addedYear, "Hidden") }));
        await LoadDefaultWindow(page, true);
        var stateType = typeof(Home).GetNestedType("ChartViewState", BindingFlags.NonPublic)!;
        Set(page, "_renderedChartState", Activator.CreateInstance(stateType,
            ChartDisplayType.Bar, false, false, true, false)!);
        Invoke(page, "BuildChartOptions");

        var series = Get<List<string>>(page, "validMedsList");
        Assert.Contains("Original (mg)", series);
        Assert.Contains("Added (mg)", series);
        Assert.DoesNotContain("Hidden (mg)", series);
        var targets = (List<string>)typeof(Home)
            .GetProperty("VisibleTargets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
        Assert.Contains("Added", targets);
        Assert.DoesNotContain("Hidden", targets);

        var model = Get<DashboardViewModel>(page, "_viewModel");
        Assert.Contains(model.DailyBehaviorCounts, point => point.Date.Year == addedYear);
        foreach (var name in new[] { "Original", "Added" })
        {
            var points = (List<DailyBehaviorCount>)Invoke(page, "BuildMedicationPlotData", $"{name} (mg)")!;
            var year = name == "Original" ? 2024 : addedYear;
            Assert.Contains(points, point => point.Date.Year == year && point.Rate == 10);
        }
    }

    [Fact]
    public async Task HoverSubcategoriesCountShiftOccurrencesRatherThanEpisodes()
    {
        var workbook = Workbook(2025, "Original");
        workbook.Behaviors[0].Subcategory = "hitting, grabbing";
        workbook.Behaviors[0].Episode_Count = 5;
        workbook.Behaviors[1].Subcategory = "grabbing";
        workbook.Behaviors[1].Episode_Count = 3;
        var service = new PatientHistoryService(null!, new ConfigurationBuilder().Build());
        var model = await service.GetPatientHistoryFromRecordsAsync(workbook.Behaviors, [],
            AggregationPeriod.Month);
        Assert.Equal(8, Assert.Single(model.DailyBehaviorCounts).Count);

        using var page = new Home();
        Set(page, "_viewModel", model);
        Set(page, "_selectedPeriod", AggregationPeriod.Month);
        await (Task)Invoke(page, "UpdateHoverDetails", model.DailyBehaviorCounts[0])!;
        var details = Get<List<KeyValuePair<string, int>>>(page, "_hoveredSubcategories").ToDictionary();
        Assert.Equal(1, details["hitting"]);
        Assert.Equal(2, details["grabbing"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(35)]
    [InlineData(123)]
    public async Task FrequencyAxisHasIntegerTickIntervals(int count)
    {
        using var page = new Home();
        typeof(Home).GetProperty("PatientHistoryService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new PatientHistoryService(null!, new ConfigurationBuilder().Build()));
        var workbook = Workbook(2025, "Original");
        workbook.Behaviors[0].Episode_Count = count;
        Set(page, "_parsedWorkbook", workbook);
        Set(page, "_selectedPeriod", AggregationPeriod.Month);
        Set(page, "_yAxisMetric", YAxisMetric.Frequency);
        await LoadDefaultWindow(page, true);
        Invoke(page, "BuildChartOptions");
        var options = Get<ApexCharts.ApexChartOptions<DailyBehaviorCount>>(page, "_options");
        var axis = options.Yaxis.First();
        Assert.Equal(5, axis.TickAmount);
        Assert.True((decimal)axis.Max >= count);
        Assert.Equal(0m, (decimal)axis.Max % 5);
    }

    [Fact]
    public async Task PdfSummaryUsesSelectedDaysIncludingEndOfFinalMonth()
    {
        using var page = new Home();
        typeof(Home).GetProperty("PatientHistoryService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new PatientHistoryService(null!, new ConfigurationBuilder().Build()));
        var workbook = Workbook(2025, "Original");
        workbook.Behaviors[0].Episode_Count = 100;
        workbook.Behaviors[1].Episode_Count = 7;
        Set(page, "_parsedWorkbook", workbook);
        Set(page, "_selectedPeriod", AggregationPeriod.Month);
        await LoadDefaultWindow(page, true);
        Set(page, "_startDate", new DateTime(2025, 1, 10));
        Set(page, "_endDate", new DateTime(2025, 1, 31));
        Set(page, "_dateRange", new MudBlazor.DateRange(new DateTime(2025, 1, 10), new DateTime(2025, 1, 31)));
        await (Task)Invoke(page, "LoadDataAsync", false)!;
        var summary = System.Text.Json.JsonSerializer.SerializeToElement(Invoke(page, "BuildPdfSummary", "Test - Review"));
        Assert.Equal("Test - Review", summary.GetProperty("Resident").GetString());
        Assert.Equal(7, summary.GetProperty("TotalEpisodes").GetInt32());
        var med = Assert.Single(summary.GetProperty("Medications").EnumerateArray());
        Assert.Equal("Jan 10, 2025", med.GetProperty("Start").GetString());
        Assert.Equal("Jan 31, 2025", med.GetProperty("End").GetString());
        Assert.False(summary.TryGetProperty("MissingData", out _));
        Assert.False(summary.GetProperty("Behaviors")[0].TryGetProperty("AverageIntensity", out _));
    }

    [Theory]
    [InlineData(AggregationPeriod.Month, ChartDisplayType.Bar, false)]
    [InlineData(AggregationPeriod.Month, ChartDisplayType.Bar, true)]
    [InlineData(AggregationPeriod.Week, ChartDisplayType.Bar, true)]
    [InlineData(AggregationPeriod.Day, ChartDisplayType.Line, false)]
    [InlineData(AggregationPeriod.Average, ChartDisplayType.Bar, true)]
    public async Task ScipSeriesHaveCountAxisAndMarkersWithOtherOverlays(AggregationPeriod period, ChartDisplayType chartType, bool stacked)
    {
        using var page = new Home();
        typeof(Home).GetProperty("PatientHistoryService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new PatientHistoryService(null!, new ConfigurationBuilder().Build()));
        var workbook = Workbook(2025, "Original");
        workbook.Behaviors.ForEach(row => row.Intensity_01_Count = 1);
        workbook.Scip = [new(new DateTime(2025, 1, 1), TimeSpan.FromHours(8), "Procedure", 3, "Test", 106)];
        Set(page, "_parsedWorkbook", workbook);
        Set(page, "_selectedPeriod", period);
        await LoadDefaultWindow(page, true);
        Set(page, "_showScip", true);
        var stateType = typeof(Home).GetNestedType("ChartViewState", BindingFlags.NonPublic)!;
        Set(page, "_renderedChartState", Activator.CreateInstance(stateType, chartType, stacked, false, true, true)!);
        Invoke(page, "BuildChartOptions");
        var options = Get<ApexCharts.ApexChartOptions<DailyBehaviorCount>>(page, "_options");
        Assert.Contains("SCIP frequency", (List<string>)options.Yaxis.Last().Title.Text);
        Assert.Equal(0, options.Yaxis.Last().DecimalsInFloat);
        Assert.Equal(ApexCharts.ChartType.Line, options.Chart.Type);
        Assert.Contains(options.Markers.Discrete, marker => marker.Size == 5);
        var points = Get<Dictionary<string, List<DailyBehaviorCount>>>(page, "_scipSeries")["Procedure"];
        Assert.Equal(3, points.Sum(point => point.Count));
        Set(page, "_showScip", false);
        Invoke(page, "BuildChartOptions");
        Assert.Empty(Get<Dictionary<string, List<DailyBehaviorCount>>>(page, "_scipSeries"));
        Assert.DoesNotContain(Get<ApexCharts.ApexChartOptions<DailyBehaviorCount>>(page, "_options").Yaxis,
            axis => axis.Title != null && ((List<string>)axis.Title.Text).Contains("SCIP frequency"));
    }

    [Fact]
    public async Task ScipDefaultsToRestrictiveColumnsAndHonorsSelectionOverrides()
    {
        var page = new Home();
        typeof(Home).GetProperty("PatientHistoryService", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new PatientHistoryService(null!, new ConfigurationBuilder().Build()));
        var workbook = Workbook(2025, "Original");
        workbook.Scip = Enumerable.Range(95, 18).Select(column =>
            new ScipRecord(new DateTime(2025, 1, 1), TimeSpan.FromHours(8),
                $"Maneuver {column}", 2, "Test", column)).ToList();
        Set(page, "_parsedWorkbook", workbook);
        await LoadDefaultWindow(page, true);
        Set(page, "_showScip", true);
        Invoke(page, "BuildChartOptions");
        var series = Get<Dictionary<string, List<DailyBehaviorCount>>>(page, "_scipSeries");
        Assert.Equal(7, series.Count);
        Assert.All(Enumerable.Range(106, 7), column => Assert.Contains($"Maneuver {column}", series.Keys));
        var selection = Get<Dictionary<string, bool>>(page, "_scipSelection");
        selection["Maneuver 106"] = false;
        selection["Maneuver 95"] = true;
        Invoke(page, "BuildChartOptions");
        series = Get<Dictionary<string, List<DailyBehaviorCount>>>(page, "_scipSeries");
        Assert.DoesNotContain("Maneuver 106", series.Keys);
        Assert.Contains("Maneuver 95", series.Keys);
    }

    private static ParsedWorkbookResult Workbook(int year, string name) => new()
    {
        Behaviors = new[] { 1, 31 }.Select(day => new PatientHistoryService.BehaviorCsvRow
        {
            Date = new DateTime(year, 1, day), Person_ID = "Test", Target = name,
            Episode_Count = 1, Time = new TimeSpan(8, 0, 0)
        }).ToList(),
        Medications = new()
        {
            new() { Name = name, Dose = "10", Unit = "mg",
                StartDate = new DateTime(year, 1, 1), EndDate = new DateTime(year, 1, 31) }
        }
    };

    private static Task LoadDefaultWindow(Home page, bool full) =>
        (Task)Invoke(page, "LoadDefaultDateRangeAsync", full)!;

    private static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);

    private static T Get<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
