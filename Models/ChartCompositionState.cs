namespace PsychDashboard.Models;

/// <summary>
/// Describes the series shape required for the current chart controls.
/// Keeping this decision in one place prevents the Razor series and the
/// ApexCharts option arrays from disagreeing during overlay transitions.
/// </summary>
public readonly record struct ChartCompositionState(
    bool IsStacked,
    bool StackOnlyBars,
    bool ShowMedicationSeries,
    int SeriesPerTarget)
{
    public static ChartCompositionState Resolve(
        ChartDisplayType chartType,
        bool showIntensity,
        bool hasIntensityData,
        bool showDuration,
        bool hasDurationData,
        bool showMedications,
        int intensityBucketCount = 5,
        int durationBucketCount = 6)
    {
        var isStacked = chartType == ChartDisplayType.Bar &&
            ((showIntensity && hasIntensityData) ||
             (showDuration && hasDurationData));

        var seriesPerTarget = !isStacked
            ? 1
            : showIntensity
                ? Math.Max(1, intensityBucketCount) + 1
                : showDuration
                    ? Math.Max(1, durationBucketCount) + 1
                    : 1;

        return new ChartCompositionState(
            IsStacked: isStacked,
            StackOnlyBars: isStacked,
            ShowMedicationSeries: showMedications,
            SeriesPerTarget: seriesPerTarget);
    }
}
