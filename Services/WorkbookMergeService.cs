using System.Text.Json;

namespace PsychDashboard.Services;

public static class WorkbookMergeService
{
    public static ParsedWorkbookResult Merge(
        ParsedWorkbookResult? existing,
        IEnumerable<ParsedWorkbookResult> additions)
    {
        var sources = new[] { existing }
            .Concat(additions)
            .Where(source => source != null)
            .Cast<ParsedWorkbookResult>()
            .ToList();

        return new ParsedWorkbookResult
        {
            Behaviors = sources
                .SelectMany(source => source.Behaviors)
                .DistinctBy(record => JsonSerializer.Serialize(record))
                .OrderBy(record => record.Date)
                .ThenBy(record => record.Time)
                .ToList(),
            Medications = sources
                .SelectMany(source => source.Medications)
                .DistinctBy(medication => JsonSerializer.Serialize(medication))
                .OrderBy(medication => medication.StartDate)
                .ThenBy(medication => medication.Name)
                .ToList()
        };
    }
}
