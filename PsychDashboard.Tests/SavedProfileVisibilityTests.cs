using System.Reflection;
using PsychDashboard.Components.Shared;
using PsychDashboard.Models;
using Xunit;

namespace PsychDashboard.Tests;

public class SavedProfileVisibilityTests
{
    [Fact]
    public void SavedIndividualProfileRemainsSelectableBeforeWorkbookIsLoaded()
    {
        var component = new SavedProfiles();
        var profile = new ViewProfile { Name = "Review", Person = "Example Resident" };
        Set(component, "_profiles", new List<ViewProfile> { profile });
        Set(component, "_selectedId", profile.Id);
        Assert.Same(profile, Get(component, "Selected"));
        Assert.Equal(false, Get(component, "CanApply"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Another Resident", false)]
    [InlineData(" example resident ", true)]
    public void ApplyingRequiresMatchingLoadedIndividual(string? person, bool expected)
    {
        var component = new SavedProfiles();
        var profile = new ViewProfile { Name = "Review", Person = "Example Resident" };
        Set(component, "_profiles", new List<ViewProfile> { profile });
        Set(component, "_selectedId", profile.Id);
        typeof(SavedProfiles).GetProperty("Enabled")!.SetValue(component, true);
        typeof(SavedProfiles).GetProperty("Person")!.SetValue(component, person);
        Assert.Same(profile, Get(component, "Selected"));
        Assert.Equal(expected, Get(component, "CanApply"));
    }

    [Theory]
    [InlineData("Example Resident", 1)]
    [InlineData("Another Resident", 0)]
    public async Task DropdownSelectionAppliesOnlyMatchingProfile(string person, int expectedCalls)
    {
        var component = new SavedProfiles();
        var profile = new ViewProfile { Name = "Review", Person = "Example Resident" };
        Set(component, "_profiles", new List<ViewProfile> { profile });
        Set(component, "_selectedId", profile.Id);
        typeof(SavedProfiles).GetProperty("Enabled")!.SetValue(component, true);
        typeof(SavedProfiles).GetProperty("Person")!.SetValue(component, person);
        typeof(SavedProfiles).GetProperty("Capture")!.SetValue(component, (Func<ViewProfile>)(() => new()));
        var calls = 0;
        var callback = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<ViewProfile>(
            new object(), (Action<ViewProfile>)(selected => { Assert.Same(profile, selected); calls++; }));
        typeof(SavedProfiles).GetProperty("OnApply")!.SetValue(component, callback);
        await (Task)typeof(SavedProfiles).GetMethod("OnProfileSelected",
            BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, null)!;
        Assert.Equal(expectedCalls, calls);
    }

    private static void Set(object obj, string name, object value) =>
        obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(obj, value);
    private static object? Get(object obj, string name) =>
        obj.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj);
}
