namespace PsychDashboard.Models;

public sealed record ScipRecord(DateTime Date, TimeSpan? Time, string Name, int Count, string PersonId, int SourceColumn = -1);
