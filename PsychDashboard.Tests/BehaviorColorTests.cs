using PsychDashboard.Components.Shared;
using Xunit;

namespace PsychDashboard.Tests;

public class BehaviorColorTests
{
    [Fact]
    public void NewBehaviorsHaveUniqueStableColorsEvenBeforeSelectorParametersRefresh()
    {
        var selector = new VariableSelector();
        string[] targets = ["Aggression", "Self Mutilation", "SIB", "Disruptive",
            "Property Destruction", "Elopement", "Other A", "Other B", "Other C"];
        var colors = targets.ToDictionary(target => target, selector.GetTargetColor);
        Assert.Equal(targets.Length, colors.Values.Distinct().Count());
        Assert.Equal("#2196F3", colors["Self Mutilation"]);
        Assert.Equal("#FF9800", colors["Disruptive"]);
        selector.GetTargetColor("Added next year");
        Assert.All(targets, target => Assert.Equal(colors[target], selector.GetTargetColor(target)));
    }

    [Fact]
    public void SeparateLabelsSharingPreferredColorStillReceiveUniqueColors()
    {
        var selector = new VariableSelector();
        string[] targets = ["SIB", "Self-injury", "Self Mutilation", "Self-Mutilation",
            "Disruptive", "Disruption", "Disruptive Behaviors"];
        Assert.Equal(targets.Length, targets.Select(selector.GetTargetColor).Distinct().Count());
    }

    [Fact]
    public void UnrecognizedBehaviorsDoNotRepeatWhenPaletteIsExhausted()
    {
        var selector = new VariableSelector();
        Assert.Equal(24, Enumerable.Range(1, 24).Select(i => selector.GetTargetColor($"Behavior {i}"))
            .Distinct().Count());
    }
}
