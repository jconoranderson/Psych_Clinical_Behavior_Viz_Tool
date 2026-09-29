using PsychDashboard.Models;
using Xunit;

namespace PsychDashboard.Tests;

public sealed class LinearTrendTests
{
    [Fact]
    public void CalculateReturnsRegressionEndpointsForIrregularDates()
    {
        var result = LinearTrend.Calculate(new[]
        {
            (new DateTime(2025, 1, 1), 2d),
            (new DateTime(2025, 1, 11), 4d),
            (new DateTime(2025, 1, 31), 8d)
        });

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2025, 1, 1), result[0].Date);
        Assert.Equal(2d, result[0].Value, 10);
        Assert.Equal(new DateTime(2025, 1, 31), result[1].Date);
        Assert.Equal(8d, result[1].Value, 10);
    }

    [Fact]
    public void CalculateRequiresTwoDistinctDates()
    {
        var date = new DateTime(2025, 1, 1);

        Assert.Empty(LinearTrend.Calculate(new[] { (date, 2d) }));
        Assert.Empty(LinearTrend.Calculate(new[] { (date, 2d), (date, 4d) }));
    }

    [Fact]
    public void CalculateDoesNotProjectBelowZero()
    {
        var result = LinearTrend.Calculate(new[]
        {
            (new DateTime(2025, 1, 1), 1d),
            (new DateTime(2025, 1, 2), 0d),
            (new DateTime(2025, 1, 3), 0d)
        });

        Assert.All(result, point => Assert.True(point.Value >= 0));
    }
}
