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
