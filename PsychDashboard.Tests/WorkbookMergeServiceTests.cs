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
            StudentInfoName = "Example Student",
            Behaviors =
            [
                Behavior(new DateTime(2023, 7, 1), 1),
                shared
            ],
            Medications =
            [
                Medication("Example", new DateTime(2023, 7, 1))
            ],
            IntensityLabels = ["Low", "High"],
            DurationLabels = ["< 5 minutes"]
        };
        var second = new ParsedWorkbookResult
        {
            StudentInfoName = "Example Student",
            Behaviors =
            [
                shared,
                Behavior(new DateTime(2025, 6, 30), 3)
            ],
            Medications =
            [
                Medication("Example", new DateTime(2023, 7, 1))
            ],
            IntensityLabels = ["low", "Critical"],
            DurationLabels = ["< 5 minutes", "5+ minutes"]
        };

        var merged = WorkbookMergeService.Merge(first, new[] { second });

        Assert.Equal(3, merged.Behaviors.Count);
        Assert.Equal(new DateTime(2023, 7, 1), merged.Behaviors.First().Date);
        Assert.Equal(new DateTime(2025, 6, 30), merged.Behaviors.Last().Date);
        Assert.Single(merged.Medications);
        Assert.Equal(new[] { "Low", "High", "Critical" }, merged.IntensityLabels);
        Assert.Equal(new[] { "< 5 minutes", "5+ minutes" }, merged.DurationLabels);
    }

    [Theory]
    [InlineData("Other Student")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsMismatchedOrMissingIdentityWithoutChangingExistingData(string? name)
    {
        var existing = new ParsedWorkbookResult { StudentInfoName = "Example Student",
            Behaviors = [Behavior(new DateTime(2025, 7, 1), 2)] };
        var addition = new ParsedWorkbookResult { StudentInfoName = name };
        var error = Assert.Throws<InvalidDataException>(() => WorkbookMergeService.Merge(existing, [addition]));
        Assert.Contains("STUDENT INFO!A1", error.Message);
        Assert.Single(existing.Behaviors);
    }

    [Fact]
    public void ValidatesNamesWithinSimultaneousUpload()
    {
        Assert.Throws<InvalidDataException>(() => WorkbookMergeService.Merge(null,
            [new() { StudentInfoName = "First Student" }, new() { StudentInfoName = "Second Student" }]));
    }

    [Fact]
    public void IgnoresCaseAndSurroundingWhitespaceAndPreservesIdentityForNextUpload()
    {
        var merged = WorkbookMergeService.Merge(new() { StudentInfoName = "Example Student" },
            [new() { StudentInfoName = " example student " }]);
        Assert.Equal("Example Student", merged.StudentInfoName);
        Assert.Throws<InvalidDataException>(() => WorkbookMergeService.Merge(merged,
            [new() { StudentInfoName = "Other Student" }]));
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
