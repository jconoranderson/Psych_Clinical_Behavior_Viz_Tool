using PsychDashboard.Models;
using Xunit;

namespace PsychDashboard.Tests;

public class ChartCompositionStateTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MedicationVisibilityIsIndependentOfIntensity(bool showIntensity, bool showMedications)
    {
        var state = ChartCompositionState.Resolve(
            ChartDisplayType.Bar,
            showIntensity,
            hasIntensityData: true,
            showDuration: false,
            hasDurationData: false,
            showMedications);

        Assert.Equal(showIntensity, state.IsStacked);
        Assert.Equal(showIntensity, state.StackOnlyBars);
        Assert.Equal(showMedications, state.ShowMedicationSeries);
        Assert.Equal(showIntensity ? 6 : 1, state.SeriesPerTarget);
    }

    [Fact]
    public void DurationCompositionAccountsForAllBucketsAndUnavailableSeries()
    {
        var state = ChartCompositionState.Resolve(
            ChartDisplayType.Bar,
            showIntensity: false,
            hasIntensityData: false,
            showDuration: true,
            hasDurationData: true,
            showMedications: true);

        Assert.True(state.IsStacked);
        Assert.True(state.StackOnlyBars);
        Assert.True(state.ShowMedicationSeries);
        Assert.Equal(7, state.SeriesPerTarget);
    }

    [Fact]
    public void LineChartKeepsOverlaysUnstacked()
    {
        var state = ChartCompositionState.Resolve(
            ChartDisplayType.Line,
            showIntensity: true,
            hasIntensityData: true,
            showDuration: false,
            hasDurationData: false,
            showMedications: true);

        Assert.False(state.IsStacked);
        Assert.False(state.StackOnlyBars);
        Assert.True(state.ShowMedicationSeries);
        Assert.Equal(1, state.SeriesPerTarget);
    }

    [Fact]
    public void MedicationABSequencePreservesTheSelectedIntensityMode()
    {
        var medicationStates = new[] { true, false, true };

        var compositions = medicationStates.Select(showMedications =>
            ChartCompositionState.Resolve(
                ChartDisplayType.Bar,
                showIntensity: true,
                hasIntensityData: true,
                showDuration: false,
                hasDurationData: false,
                showMedications)).ToList();

        Assert.All(compositions, state =>
        {
            Assert.True(state.IsStacked);
            Assert.True(state.StackOnlyBars);
            Assert.Equal(6, state.SeriesPerTarget);
        });
        Assert.Equal(medicationStates, compositions.Select(state => state.ShowMedicationSeries));
    }
}
