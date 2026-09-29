namespace PsychDashboard.Models;

public static class LinearTrend
{
    public static IReadOnlyList<(DateTime Date, double Value)> Calculate(
        IEnumerable<(DateTime Date, double Value)> observations)
    {
        var points = observations.OrderBy(point => point.Date).ToList();
        if (points.Count < 2 || points.Select(point => point.Date).Distinct().Count() < 2)
            return Array.Empty<(DateTime, double)>();

        var origin = points[0].Date;
        var xValues = points.Select(point => (point.Date - origin).TotalDays).ToArray();
        var xMean = xValues.Average();
        var yMean = points.Average(point => point.Value);
        var denominator = xValues.Sum(x => Math.Pow(x - xMean, 2));
        if (denominator <= 0) return Array.Empty<(DateTime, double)>();

        var slope = points
            .Select((point, index) => (xValues[index] - xMean) * (point.Value - yMean))
            .Sum() / denominator;
        var intercept = yMean - slope * xMean;

        return new[] { points.First().Date, points.Last().Date }
            .Select(date => (
                date,
                Math.Max(0, intercept + slope * (date - origin).TotalDays)))
            .ToList();
    }
}
