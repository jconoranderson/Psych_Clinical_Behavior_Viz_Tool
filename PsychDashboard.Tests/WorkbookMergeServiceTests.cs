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
            ],
            IntensityLabels = ["Low", "High"],
            DurationLabels = ["< 5 minutes"]
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
            ],
            IntensityLabels = ["low", "High"],
            DurationLabels = ["< 5 minutes"]
        };

        var merged = WorkbookMergeService.Merge(first, new[] { second });

        Assert.Equal(3, merged.Behaviors.Count);
        Assert.Equal(new DateTime(2023, 7, 1), merged.Behaviors.First().Date);
        Assert.Equal(new DateTime(2025, 6, 30), merged.Behaviors.Last().Date);
        Assert.Single(merged.Medications);
        Assert.Equal(new[] { "Low", "High" }, merged.IntensityLabels);
        Assert.Equal(new[] { "< 5 minutes" }, merged.DurationLabels);
    }

    [Fact]
    public void DifferentResidentsAreRejectedWithoutChangingExistingData()
    {
        var existing = new ParsedWorkbookResult { Behaviors = [Behavior(new DateTime(2024, 7, 1), 2)] };
        var other = Behavior(new DateTime(2024, 7, 1), 5);
        other.Person_ID = "resident-2";
        var addition = new ParsedWorkbookResult { SourceFileName = "other.xlsx", Behaviors = [other] };

        var error = Assert.Throws<WorkbookValidationException>(() => WorkbookMergeService.Merge(existing, [addition]));

        Assert.Contains(error.Issues, issue => issue.FileName == "other.xlsx" && issue.Message.Contains("different resident"));
        Assert.Equal(2, Assert.Single(existing.Behaviors).Episode_Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DifferentBucketLabelsAreRejected(bool duration)
    {
        var first = new ParsedWorkbookResult { IntensityLabels = ["Low", "High"], DurationLabels = ["Short", "Long"] };
        var second = new ParsedWorkbookResult { SourceFileName = "changed.xlsx", IntensityLabels = ["Low", "High"], DurationLabels = ["Short", "Long"] };
        if (duration) second.DurationLabels.Reverse();
        else second.IntensityLabels[1] = "Critical";

        var error = Assert.Throws<WorkbookValidationException>(() => WorkbookMergeService.Merge(first, [second]));

        Assert.Contains(error.Issues, issue => issue.FileName == "changed.xlsx" && issue.Severity == WorkbookIssueSeverity.Error);
    }

    [Fact]
    public void ValidationErrorsRejectWholeBatchAndWarningsSurviveSuccessfulMerge()
    {
        var warning = new WorkbookIssue("valid.xlsx", "MEDICATIONS", "F5", WorkbookIssueSeverity.Warning, "Entry skipped.");
        var valid = new ParsedWorkbookResult { Issues = [warning] };
        Assert.Equal(warning, Assert.Single(WorkbookMergeService.Merge(null, [valid]).Issues));
        var invalid = new ParsedWorkbookResult
        {
            Issues = [new("bad.xlsx", "", "", WorkbookIssueSeverity.Error, "Invalid workbook.")]
        };
        var error = Assert.Throws<WorkbookValidationException>(() => WorkbookMergeService.Merge(null, [valid, invalid]));
        Assert.Contains(warning, error.Issues);
        Assert.Equal(2, error.Issues.Count);
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
