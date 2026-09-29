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
    [InlineData(AggregationPeriod.Month, ChartDisplayType.Bar)]
    [InlineData(AggregationPeriod.Week, ChartDisplayType.Bar)]
    [InlineData(AggregationPeriod.Day, ChartDisplayType.Bar)]
    [InlineData(AggregationPeriod.Month, ChartDisplayType.Line)]
    public void MidPeriodChangesAndGapsUseActualDates(AggregationPeriod period, ChartDisplayType chartType)
    {
        using var page = CreatePage(period, false, true);
        var model = (DashboardViewModel)Field("_viewModel").GetValue(page)!;
        var start = new DateTime(2025, 1, 1);
        model.AvailableMedications = new() { "Example" };
        model.UnreducedMedications = Enumerable.Range(0, 31)
            .Where(day => day < 20 || day >= 26)
            .Select(day => new DailyMedication
            {
                Name = "Example", Unit = "mg", Date = start.AddDays(day), Dose = day < 14 ? 10 : 25
            }).ToList();
        model.DailyMedications = model.UnreducedMedications;
        var stateType = typeof(Home).GetNestedType("ChartViewState", BindingFlags.NonPublic)!;
        Field("_renderedChartState").SetValue(page, Activator.CreateInstance(stateType,
            chartType, false, false, true, false));

        var points = (List<DailyBehaviorCount>)Invoke(page, "BuildMedicationPlotData", "Example (mg)")!;
        Assert.Contains(points, p => p.Date == start.AddDays(14) && p.Rate == 25);
        Assert.Contains(points, p => p.Date == start.AddDays(20) && p.Rate == 0.1);
        Assert.Contains(points, p => p.Date == start.AddDays(26) && p.Rate == 25);
        Assert.Equal(start.AddDays(30), points.Last().Date);

        Invoke(page, "BuildChartOptions");
        var options = (ApexChartOptions<DailyBehaviorCount>)Field("_options").GetValue(page)!;
        var change = Assert.Single(options.Annotations.Points);
        var expectedX = period == AggregationPeriod.Month
            ? -0.5 + 14d / 31
            : new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        Assert.Equal(expectedX, Convert.ToDouble(change.X), 8);
        if (period == AggregationPeriod.Month)
            Assert.Equal(expectedX, Convert.ToDouble(Invoke(page, "GetMedicationXValue", points[1])), 8);
    }

    [Fact]
    public void MonthlyHoverReportsDoseChangesWithinTheMonth()
    {
        using var page = CreatePage(AggregationPeriod.Month, false, true);
        var model = (DashboardViewModel)Field("_viewModel").GetValue(page)!;
        model.UnreducedMedications.Add(new DailyMedication
        {
            Name = "Low", Dose = 15, Unit = "mg", Date = new DateTime(2025, 1, 15)
        });
        var lookup = (Dictionary<string, string>)Invoke(page, "BuildMedicationTooltipLookup",
            new HashSet<string> { "Low" })!;
        Assert.Contains("5 mg", lookup["0"]);
        Assert.Contains("15 mg", lookup["0"]);
        Assert.Contains("Jan 15", lookup["0"]);
    }

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
