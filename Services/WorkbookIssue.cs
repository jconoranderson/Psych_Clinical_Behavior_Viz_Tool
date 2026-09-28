namespace PsychDashboard.Services;

public enum WorkbookIssueSeverity { Warning, Error }

public sealed record WorkbookIssue(
    string FileName,
    string Sheet,
    string Cell,
    WorkbookIssueSeverity Severity,
    string Message);

public sealed class WorkbookValidationException : Exception
{
    public IReadOnlyList<WorkbookIssue> Issues { get; }

    public WorkbookValidationException(IEnumerable<WorkbookIssue> issues)
        : base("Workbook validation failed.") => Issues = issues.ToList();
}
