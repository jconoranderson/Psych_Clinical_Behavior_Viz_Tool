using PsychDashboard.Models;
using PsychDashboard.Services;

namespace PsychDashboard.Components.Pages;

public partial class Home
{
    private ViewProfile CaptureProfile()
    {
        var targets = _viewModel?.AvailableTargets ?? new();
        var meds = _viewModel?.AvailableMedications ?? new();
        return new ViewProfile
        {
            Period = _selectedPeriod, Measure = _yAxisMetric, ChartType = _chartType,
            Shifts = _selectedShifts.ToArray(), Days = _selectedDays.ToArray(),
            Intensity = _showIntensity, Duration = _showDuration, Medication = _showMedications,
            Scip = _showScip, MissingData = _showMissingData, Trend = _showTrendLines,
            DurationFirst = _overlayPriority == OverlayMetric.Duration,
            Behaviors = targets.ToDictionary(t => t, t => new ProfileVariable
            { Visible = _variableSelector?.IsTargetVisible(t) ?? true, Color = _variableSelector?.GetTargetColor(t) ?? "#2196F3" }),
            Medications = meds.ToDictionary(m => m, m => new ProfileVariable
            { Visible = _variableSelector?.IsMedicationVisible(m) ?? true, Color = _variableSelector?.GetMedicationColor(m) ?? "#3e2723" }),
            Maneuvers = AvailableScipManeuvers.ToDictionary(m => m, IsScipSelected)
        };
    }

    private async Task ApplyProfile(ViewProfile profile)
    {
        CancelNavigatorLiveUpdate();
        ClearChartZoomBaseline();
        _selectedPeriod = profile.Period;
        _yAxisMetric = profile.Measure;
        _chartType = profile.ChartType;
        _selectedShifts = profile.Shifts;
        _selectedDays = profile.Days;
        _showIntensity = profile.Intensity;
        _showDuration = profile.Duration;
        _showMedications = profile.Medication;
        _showScip = profile.Scip;
        _showMissingData = profile.MissingData;
        _showTrendLines = profile.Trend;
        _overlayPriority = profile.DurationFirst ? OverlayMetric.Duration :
            profile.Intensity ? OverlayMetric.Intensity : OverlayMetric.None;
        _scipSelection.Clear();
        foreach (var name in AvailableScipManeuvers)
            _scipSelection[name] = profile.Maneuvers.GetValueOrDefault(name);
        _variableSelector?.ApplyProfileVariables(profile);
        // Leave the loaded workbook's current date range intact.
        await LoadDataAsync();
        _renderedChartState = new ChartViewState(_chartType, _showIntensity, _showDuration, _showMedications, _showTrendLines);
        BuildChartOptions();
        _chartRevision++;
        _navigatorRevision++;
        await InvokeAsync(StateHasChanged);
    }
}
