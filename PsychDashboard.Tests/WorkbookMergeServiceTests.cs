using PsychDashboard.Models;
using PsychDashboard.Services;
using Xunit;

namespace PsychDashboard.Tests;

public sealed class WorkbookMergeServiceTests
{
    [Fact]
    public void MergeChainsYearsAndRemovesExactOverlaps()
    {
        var shared = Behavior(new DateTime(2024, 7, 1), 2);
        var first = new ParsedWorkbookResult
        {
            Behaviors =
            [
                Behavior(new DateTime(2023, 7, 1), 1),
                shared
            ],
            Medications =
            [
                Medication("Example", new DateTime(2023, 7, 1))
            ]
        };
        var second = new ParsedWorkbookResult
        {
            Behaviors =
            [
                shared,
                Behavior(new DateTime(2025, 6, 30), 3)
            ],
            Medications =
            [
                Medication("Example", new DateTime(2023, 7, 1))
            ]
        };

        var merged = WorkbookMergeService.Merge(first, new[] { second });

        Assert.Equal(3, merged.Behaviors.Count);
        Assert.Equal(new DateTime(2023, 7, 1), merged.Behaviors.First().Date);
        Assert.Equal(new DateTime(2025, 6, 30), merged.Behaviors.Last().Date);
        Assert.Single(merged.Medications);
    }

    private static PatientHistoryService.BehaviorCsvRow Behavior(DateTime date, double count) =>
        new()
        {
            Person_ID = "resident-1",
            Date = date,
            Time = new TimeSpan(8, 0, 0),
            Target = "Aggression",
            Episode_Count = count
        };

    private static Medication Medication(string name, DateTime start) =>
        new()
        {
            Name = name,
            Dose = "10",
            Unit = "mg",
            StartDate = start,
            EndDate = start.AddDays(30)
        };
}
