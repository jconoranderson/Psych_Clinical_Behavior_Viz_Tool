using System.Reflection;
using ApexCharts;
using PsychDashboard.Components.Pages;
using PsychDashboard.Models;
using PsychDashboard.ViewModels;
using Xunit;

namespace PsychDashboard.Tests;

public sealed class MedicationTooltipTests
{
    [Theory]
    [InlineData(AggregationPeriod.Month)]
    [InlineData(AggregationPeriod.Week)]
    [InlineData(AggregationPeriod.Day)]
    [InlineData(AggregationPeriod.Average)]
    public void MedicationRowsMatchChartCoordinatesAndSortByCurrentDose(AggregationPeriod period)
    {
        using var page = CreatePage(period, false, true);
        var lookup = (Dictionary<string, string>)Invoke(page, "BuildMedicationTooltipLookup",
            new HashSet<string> { "Low", "High <test>", "Stopped", "Absent" })!;
        var key = period switch
        {
            AggregationPeriod.Month => "0",
            AggregationPeriod.Average => "Behavior",
            _ => new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
                .ToUnixTimeMilliseconds().ToString()
        };

        var html = lookup[key];
        Assert.Contains("25 mg", html);
        Assert.Contains("5 mg", html);
        Assert.True(html.IndexOf("High &lt;test&gt;", StringComparison.Ordinal) < html.IndexOf("Low", StringComparison.Ordinal));
        Assert.DoesNotContain("Hidden", html);
        Assert.DoesNotContain("Stopped", html);
        Assert.DoesNotContain("Absent", html);
    }

    [Theory]
    [InlineData(AggregationPeriod.Month, false)]
    [InlineData(AggregationPeriod.Month, true)]
    [InlineData(AggregationPeriod.Week, false)]
    [InlineData(AggregationPeriod.Week, true)]
    [InlineData(AggregationPeriod.Day, false)]
    [InlineData(AggregationPeriod.Day, true)]
    [InlineData(AggregationPeriod.Average, false)]
    [InlineData(AggregationPeriod.Average, true)]
    public void BothTooltipPathsIncludeMedicationSection(AggregationPeriod period, bool stacked)
    {
        using var page = CreatePage(period, stacked, true);
        Invoke(page, "BuildChartOptions");
        var options = (ApexChartOptions<DailyBehaviorCount>)Field("_options").GetValue(page)!;
        var formatter = stacked ? Assert.Single(options.Tooltip.Custom) : options.Tooltip.X.Formatter;
        Assert.Contains("var medLookup =", formatter);
        Assert.Contains("Medications", formatter);
        Assert.Contains("25 mg", formatter);

        using var disabled = CreatePage(period, stacked, false);
        var lookup = (Dictionary<string, string>)Invoke(disabled, "BuildMedicationTooltipLookup",
            new HashSet<string> { "High <test>", "Low" })!;
        Assert.Empty(lookup);
    }

    private static Home CreatePage(AggregationPeriod period, bool stacked, bool showMedications)
    {
        var date = new DateTime(2025, 1, 1);
        var meds = new List<DailyMedication>
        {
            new() { Name = "Low", Dose = 5, Unit = "mg", Date = date },
            new() { Name = "High <test>", Dose = 25, Unit = "mg", Date = date },
            new() { Name = "Hidden", Dose = 100, Unit = "mg", Date = date },
            new() { Name = "Stopped", Dose = 0, Unit = "mg", Date = date },
            new() { Name = "Absent", Dose = 50, Unit = "mg", Date = date.AddDays(-1) },
            // A larger historical dose must not determine today's order.
            new() { Name = "Low", Dose = 200, Unit = "mg", Date = date.AddDays(-1) }
        };
        var model = new DashboardViewModel
        {
            GlobalMinDate = date, GlobalMaxDate = date.AddDays(30),
            AvailableTargets = new() { "Behavior" },
            AvailableMedications = meds.Select(m => m.Name).Distinct().ToList(),
            DailyMedications = meds, UnreducedMedications = meds,
            DailyBehaviorCounts = new()
            {
                new() { Target = "Behavior", Date = date, PeriodEnd = date.AddDays(6),
                    Count = 3, Rate = 3, HasData = true, AverageIntensity = 1,
                    IntensityBuckets = new double[] { 3, 0, 0, 0, 0 } }
            }
        };
        var page = new Home();
        Field("_selectedPeriod").SetValue(page, period);
        Field("_viewModel").SetValue(page, model);
        Field("_fullHistoryViewModel").SetValue(page, model);
        Field("_navigatorViewModel").SetValue(page, model);
        Field("_startDate").SetValue(page, date);
        Field("_endDate").SetValue(page, date.AddDays(30));
        var stateType = typeof(Home).GetNestedType("ChartViewState", BindingFlags.NonPublic)!;
        Field("_renderedChartState").SetValue(page, Activator.CreateInstance(stateType,
            ChartDisplayType.Bar, stacked, false, showMedications, false));
        return page;
    }

    private static FieldInfo Field(string name) =>
        typeof(Home).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static object? Invoke(Home page, string name, params object[] args) =>
        typeof(Home).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, args);
}
