using PsychDashboard.Services;
using PsychDashboard.ViewModels;
using Xunit;

namespace PsychDashboard.Tests;

public class MedicationChangeTests
{
    [Fact]
    public void ListsEveryDeltaInRangeIncludingFirstSelectedDayAndRecordedZero()
    {
        var day = new DateTime(2025, 1, 1);
        var history = new[] { 10d, 10, 20, 15, 0, 0, 8 }
            .Select((dose, index) => new DailyMedication { Name = "A", Unit = "mg", Date = day.AddDays(index), Dose = dose })
            .Concat([new DailyMedication { Name = "B", Unit = "mg", Date = day.AddDays(2), Dose = 50 }]);
        var changes = MedicationChangeService.GetChanges(history.Reverse(), day.AddDays(2), day.AddDays(5));
        Assert.Equal(new[] { 10d, -5, -15 }, changes.Select(change => change.Delta));
        Assert.Equal(new[] { day.AddDays(2), day.AddDays(3), day.AddDays(4) }, changes.Select(change => change.Date));
        Assert.Equal(10, changes[0].PreviousDose);
        Assert.Equal(0, changes[^1].Dose);
    }

    [Fact]
    public void KeepsDifferentMedicationsAndUnitsSeparate()
    {
        var day = new DateTime(2025, 1, 1);
        DailyMedication[] rows = [
            new() { Name = "A", Unit = "mg", Date = day, Dose = 10 },
            new() { Name = "B", Unit = "mg", Date = day, Dose = 20 },
            new() { Name = "A", Unit = "mg", Date = day.AddDays(1), Dose = 15 },
            new() { Name = "B", Unit = "mg", Date = day.AddDays(1), Dose = 10 },
            new() { Name = "A", Unit = "ml", Date = day.AddDays(2), Dose = 1 }];
        var changes = MedicationChangeService.GetChanges(rows, day, day.AddDays(3));
        Assert.Equal(new[] { "A", "B" }, changes.Select(change => change.Medication));
        Assert.Equal(new[] { 5d, -10 }, changes.Select(change => change.Delta));
    }
}
