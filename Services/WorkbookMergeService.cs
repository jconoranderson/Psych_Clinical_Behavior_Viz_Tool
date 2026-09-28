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

        var issues = sources.SelectMany(source => source.Issues).ToList();
        string? resident = null;
        ParsedWorkbookResult? schema = null;
        foreach (var source in sources)
        {
            if (source.Issues.Any(issue => issue.Severity == WorkbookIssueSeverity.Error)) continue;
            var residents = source.Behaviors
                .Select(row => (string.IsNullOrWhiteSpace(row.Person_ID) ? row.Name : row.Person_ID)?.Trim())
                .Append(source.ResidentName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var name in residents)
            {
                resident ??= name;
                if (!string.Equals(resident, name, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new(source.SourceFileName, "", "", WorkbookIssueSeverity.Error,
                        "This upload contains a different resident. Load only one resident's workbooks at a time; clear the loaded workbooks to switch residents."));
            }

            if (schema != null)
            {
                if (!schema.IntensityLabels.SequenceEqual(source.IntensityLabels, StringComparer.OrdinalIgnoreCase) ||
                    !schema.DurationLabels.SequenceEqual(source.DurationLabels, StringComparer.OrdinalIgnoreCase))
                    issues.Add(new(source.SourceFileName, "", "", WorkbookIssueSeverity.Error,
                        "Intensity or duration labels differ from the loaded workbooks (including their order). Use matching labels or load this workbook separately."));
            }
            schema ??= source;
        }
        if (issues.Any(issue => issue.Severity == WorkbookIssueSeverity.Error))
            throw new WorkbookValidationException(issues);

        return new ParsedWorkbookResult
        {
            ResidentName = resident ?? "",
            SourceFileName = sources.FirstOrDefault()?.SourceFileName ?? "",
            Issues = issues.Distinct().ToList(),
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
            IntensityLabels = schema?.IntensityLabels.ToList() ?? new(),
            DurationLabels = schema?.DurationLabels.ToList() ?? new()
        };
    }
}
