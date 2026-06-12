using System;
using System.Collections.Generic;
using PsychDashboard.Models;

namespace PsychDashboard.ViewModels
{
    public class DashboardViewModel
    {
        public List<DailyBehaviorCount> DailyBehaviorCounts { get; set; } = new();
        public List<PsychDashboard.Services.PatientHistoryService.BehaviorCsvRow> UnreducedBehaviorCounts { get; set; } = new();
        public List<DailyMedication> DailyMedications { get; set; } = new();
        public List<DailyMedication> UnreducedMedications { get; set; } = new();
        public List<MissingDataRange> MissingDataRanges { get; set; } = new();
        public DateTime? GlobalMinDate { get; set; }
        public DateTime? GlobalMaxDate { get; set; }

        /// <summary>
        /// All distinct resident names found in the data (from the "Name" column)
        /// </summary>
        public List<string> AvailableResidents { get; set; } = new();

        /// <summary>
        /// All distinct target names found in the data (e.g. Aggression, Self-injury)
        /// </summary>
        public List<string> AvailableTargets { get; set; } = new();

        /// <summary>
        /// All distinct subcategory values found, keyed by target name
        /// </summary>
        public Dictionary<string, List<string>> AvailableSubcategories { get; set; } = new();

        /// <summary>
        /// All distinct medication names found in the data
        /// </summary>
        public List<string> AvailableMedications { get; set; } = new();
    }

    public class DailyBehaviorCount
    {
        public DateTime Date { get; set; }
        public string Label { get; set; } = string.Empty;
        public string BehaviorType { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public int Count { get; set; }

        /// <summary>Average episodes per shift within this aggregation period</summary>
        public double Rate { get; set; }

        /// <summary>Weighted average intensity (1-5 scale)</summary>
        public double AverageIntensity { get; set; }

        /// <summary>Sum of duration counts across all duration buckets</summary>
        public double TotalDuration { get; set; }

        /// <summary>Whether actual data was recorded for this period (false = no data / LOA)</summary>
        public bool HasData { get; set; } = true;

        /// <summary>Subcategories (specific maneuvers) seen in this aggregated period</summary>
        public List<string> Subcategories { get; set; } = new();

        /// <summary>Intensity counts per level (index 0=Level1/Minimal .. 4=Level5/Extreme)</summary>
        public double[] IntensityBuckets { get; set; } = new double[5];

        /// <summary>Duration counts per bucket (index 0=Bucket1 .. 5=Bucket6)</summary>
        public double[] DurationBuckets { get; set; } = new double[6];
    }

    public class DailyMedication
    {
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Dose { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double LogDose { get; set; }
    }

    /// <summary>
    /// Represents a contiguous date range with no data recorded
    /// </summary>
    public class MissingDataRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Reason { get; set; } = "No Data"; // "No Data" or "LOA"
    }

    /// <summary>
    /// Represents a user-defined intervention/phase-change date
    /// </summary>
    public class InterventionDate
    {
        public DateTime Date { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF5722";
    }
}
