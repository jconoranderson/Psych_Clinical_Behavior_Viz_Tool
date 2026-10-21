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
            Scip = sources.SelectMany(source => source.Scip)
                .OrderByDescending(record => record.SourceColumn is >= 106 and <= 112)
                .DistinctBy(record => (record.Date, record.Time, record.Name, record.Count, record.PersonId))
                .OrderBy(record => record.Date).ThenBy(record => record.Time).ToList(),
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
                .ToList(),
            IntensityLabels = sources
                .SelectMany(source => source.IntensityLabels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            DurationLabels = sources
                .SelectMany(source => source.DurationLabels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
