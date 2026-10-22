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

        // Validate the entire batch before combining any records. Use the
        // workbook identity cell, not a name inferred from behavior rows.
        if (sources.Count > 1)
        {
            if (sources.Any(source => string.IsNullOrWhiteSpace(source.StudentInfoName)))
                throw new InvalidDataException("Cannot combine workbooks because a name is missing from STUDENT INFO!A1. Please check the workbook and try again.");

            var expectedName = sources[0].StudentInfoName!.Trim();
            var mismatch = sources.Skip(1).FirstOrDefault(source =>
                !string.Equals(expectedName, source.StudentInfoName!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (mismatch != null)
                throw new InvalidDataException($"Workbook name mismatch: STUDENT INFO!A1 contains '{mismatch.StudentInfoName!.Trim()}', but the first workbook contains '{expectedName}'. Please check the workbook and select one for the same person.");
        }

        return new ParsedWorkbookResult
        {
            StudentInfoName = sources.FirstOrDefault()?.StudentInfoName,
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
