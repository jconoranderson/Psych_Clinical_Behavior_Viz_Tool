using PsychDashboard.ViewModels;

namespace PsychDashboard.Services;

public sealed record MedicationDoseChange(string Medication, DateTime Date, double PreviousDose, double Dose, string Unit)
{
    public double Delta => Dose - PreviousDose;
}

public static class MedicationChangeService
{
    public static List<MedicationDoseChange> GetChanges(IEnumerable<DailyMedication> history, DateTime? start, DateTime? end)
    {
        var changes = new List<MedicationDoseChange>();
        // Compare against the full history before filtering dates, so a change
        // on the first selected day retains its preceding dose. An initial
        // observation (or missing history) is not evidence of a change from zero.
        foreach (var medication in history.GroupBy(row => new { row.Name, row.Unit }))
        {
            DailyMedication? previous = null;
            foreach (var current in medication.OrderBy(row => row.Date))
            {
                if (previous != null && current.Dose != previous.Dose
                    && (!start.HasValue || current.Date.Date >= start.Value.Date)
                    && (!end.HasValue || current.Date.Date <= end.Value.Date))
                    changes.Add(new(current.Name, current.Date.Date, previous.Dose, current.Dose, current.Unit));
                previous = current;
            }
        }
        return changes.OrderBy(change => change.Date).ThenBy(change => change.Medication).ToList();
    }
}
