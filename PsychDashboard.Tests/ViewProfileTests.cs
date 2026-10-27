using System.Text.Json;
using PsychDashboard.Models;
using PsychDashboard.Components.Shared;
using Xunit;

namespace PsychDashboard.Tests;

public class ViewProfileTests
{
    [Fact]
    public void SettingsRoundTripWithoutWorkbookDataOrDates()
    {
        var profile = new ViewProfile
        {
            Name = "Weekend review", Person = "Example",
            Period = AggregationPeriod.Month, Measure = YAxisMetric.Frequency,
            ChartType = ChartDisplayType.Bar, Scip = true, Intensity = true,
            Days = [DayOfWeek.Saturday, DayOfWeek.Sunday], Shifts = ["7-3"],
            Behaviors = new() { ["Aggression"] = new() { Visible = true, Color = "#F44336" } },
            Maneuvers = new() { ["Escort"] = true }
        };
        var json = JsonSerializer.Serialize(profile);
        var restored = JsonSerializer.Deserialize<ViewProfile>(json)!;
        Assert.Equal(profile.Name, restored.Name);
        Assert.Equal(profile.Days, restored.Days);
        Assert.True(restored.Scip && restored.Intensity);
        Assert.True(restored.Maneuvers["Escort"]);
        Assert.DoesNotContain("Episode_Count", json);
        Assert.DoesNotContain("StartDate", json);
    }

    [Fact]
    public void ApplyMatchesNamesAndLeavesSavedMissingVariablesIntact()
    {
        var selector = new VariableSelector();
        typeof(VariableSelector).GetProperty("AvailableTargets")!.SetValue(selector,
            new List<string> { "Aggression", "New behavior" });
        typeof(VariableSelector).GetProperty("AvailableMedications")!.SetValue(selector,
            new List<string> { "Example medication" });
        var profile = new ViewProfile
        {
            Behaviors = new()
            {
                ["Aggression"] = new() { Visible = true, Color = "#123456" },
                ["Previous year behavior"] = new() { Visible = true, Color = "#abcdef" }
            },
            Medications = new() { ["Example medication"] = new() { Visible = false, Color = "#654321" } }
        };
        selector.ApplyProfileVariables(profile);
        Assert.True(selector.IsTargetVisible("Aggression"));
        Assert.False(selector.IsTargetVisible("New behavior"));
        Assert.Equal("#123456", selector.GetTargetColor("Aggression"));
        Assert.False(selector.IsMedicationVisible("Example medication"));
        Assert.Contains("Previous year behavior", profile.Behaviors.Keys);
    }
}
