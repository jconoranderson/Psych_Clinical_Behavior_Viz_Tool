using PsychDashboard.Models;
using PsychDashboard.ViewModels;

namespace PsychDashboard.Services;

public static class ScipAggregationService
{
    public static IEnumerable<ScipRecord> InRange(IEnumerable<ScipRecord> records,
        DateTime? start, DateTime? end, IReadOnlyCollection<string>? shifts) =>
        records.Where(row => (!start.HasValue || row.Date.Date >= start.Value.Date)
            && (!end.HasValue || row.Date.Date <= end.Value.Date)
            && (shifts == null || shifts.Contains(Shift(row.Time))));

    public static bool HasPositiveCount(IEnumerable<ScipRecord> records, string name,
        DateTime? start, DateTime? end, IReadOnlyCollection<string>? shifts) =>
        InRange(records, start, end, shifts).Any(row => row.Name == name && row.Count > 0);

    // Use the behavior chart's actual period boundaries, including its anchored
    // seven-day weeks. SCIP remains a count even when behavior is shown as rate.
    public static List<DailyBehaviorCount> Aggregate(
        IEnumerable<ScipRecord> records, string name, AggregationPeriod period,
        IEnumerable<DailyBehaviorCount> behaviorPeriods, IReadOnlyCollection<string>? shifts,
        DateTime? selectedStart = null, DateTime? selectedEnd = null)
    {
        var rows = records.Where(row => row.Name == name &&
            (shifts == null || shifts.Contains(Shift(row.Time)))).ToList();
        var periods = behaviorPeriods.GroupBy(point => point.Date.Date)
            .Select(group => group.First()).OrderBy(point => point.Date).ToList();
        var result = new List<DailyBehaviorCount>();
        foreach (var point in periods)
        {
            var start = period == AggregationPeriod.Average ? selectedStart ?? point.Date : point.Date.Date;
            var end = period switch
            {
                AggregationPeriod.Month => start.AddMonths(1).AddDays(-1),
                AggregationPeriod.Week => point.PeriodEnd >= start ? point.PeriodEnd : start.AddDays(6),
                AggregationPeriod.Average => selectedEnd ?? point.PeriodEnd,
                _ => start
            };
            var matches = rows.Where(row => row.Date.Date >= start.Date && row.Date.Date <= end.Date).ToList();
            result.Add(new DailyBehaviorCount
            {
                Date = point.Date, PeriodEnd = end, Target = $"SCIP: {name}",
                Count = matches.Sum(row => row.Count), HasData = matches.Count > 0
            });
        }
        return result;
    }

    private static string Shift(TimeSpan? time) => time == null ? "Unknown"
        : time.Value.Hours is >= 7 and < 15 ? "7-3"
        : time.Value.Hours is >= 15 and < 23 ? "3-11" : "11-7";
}
