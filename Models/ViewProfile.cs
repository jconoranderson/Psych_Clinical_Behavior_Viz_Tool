namespace PsychDashboard.Models;

// Settings only: never persist workbook rows, counts, dates, or clinical notes.
public sealed class ViewProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? Person { get; set; }
    public AggregationPeriod Period { get; set; }
    public YAxisMetric Measure { get; set; }
    public ChartDisplayType ChartType { get; set; }
    public string[] Shifts { get; set; } = [];
    public DayOfWeek[] Days { get; set; } = [];
    public bool Intensity { get; set; }
    public bool Duration { get; set; }
    public bool Medication { get; set; }
    public bool Scip { get; set; }
    public bool MissingData { get; set; }
    public bool Trend { get; set; }
    public bool DurationFirst { get; set; }
    public Dictionary<string, ProfileVariable> Behaviors { get; set; } = new();
    public Dictionary<string, ProfileVariable> Medications { get; set; } = new();
    public Dictionary<string, bool> Maneuvers { get; set; } = new();
}

public sealed class ProfileVariable
{
    public bool Visible { get; set; }
    public string Color { get; set; } = "#2196F3";
}
