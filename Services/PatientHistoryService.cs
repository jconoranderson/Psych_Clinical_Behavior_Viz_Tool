using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.AspNetCore.Hosting;
using PsychDashboard.Models;
using PsychDashboard.ViewModels;

namespace PsychDashboard.Services
{
    public class PatientHistoryService
    {
        private readonly IWebHostEnvironment _environment;

        public PatientHistoryService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<DashboardViewModel> GetPatientHistoryAsync(
            AggregationPeriod period, 
            HashSet<string>? selectedShifts = null,
            string? selectedResident = null,
            DateTime? filterStartDate = null, 
            DateTime? filterEndDate = null)
        {
            var behaviorRecords = new List<BehaviorCsvRow>();
            var behaviorPath = "/Users/canderson/Python/behavior/data_out/behavior_recent.csv";
            if (System.IO.File.Exists(behaviorPath))
            {
                using var reader = new System.IO.StreamReader(behaviorPath);
                using var csv = new CsvHelper.CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null
                });
                behaviorRecords = csv.GetRecords<BehaviorCsvRow>().ToList();
            }

            var medRecords = new List<MedicationCsvRow>();
            var medicationPath = "/Users/canderson/Python/medications_behavior_workbooks/data_out/meds_for_psych_vis_tool.csv";
            if (System.IO.File.Exists(medicationPath))
            {
                using var reader = new System.IO.StreamReader(medicationPath);
                using var csv = new CsvHelper.CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null
                });
                medRecords = csv.GetRecords<MedicationCsvRow>().ToList();
            }

            return ProcessRecords(new DashboardViewModel(), behaviorRecords, medRecords, period, selectedShifts, selectedResident, filterStartDate, filterEndDate);
        }

        public Task<DashboardViewModel> GetPatientHistoryFromRecordsAsync(
            List<BehaviorCsvRow> behaviorRecords,
            List<PsychDashboard.Models.Medication> medications,
            AggregationPeriod period, 
            HashSet<string>? selectedShifts = null,
            DateTime? filterStartDate = null, 
            DateTime? filterEndDate = null)
        {
            var viewModel = new DashboardViewModel();
            viewModel.Medications = medications;
            return Task.FromResult(ProcessRecords(viewModel, behaviorRecords, new List<MedicationCsvRow>(), period, selectedShifts, null, filterStartDate, filterEndDate));
        }

        private DashboardViewModel ProcessRecords(
            DashboardViewModel viewModel,
            List<BehaviorCsvRow> records,
            List<MedicationCsvRow> medRecords,
            AggregationPeriod period,
            HashSet<string>? selectedShifts,
            string? selectedResident,
            DateTime? filterStartDate,
            DateTime? filterEndDate)
        {
            if (records.Any())
            {
// Extract all unique resident IDs from the data.
                // Prefer Person_ID; fall back to Name when Person_ID is absent.
                viewModel.AvailableResidents = records
                    .Select(r => GetResidentKey(r))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                // Filter by resident if selected
                var residentRecords = records;
                if (!string.IsNullOrEmpty(selectedResident))
                {
                    residentRecords = records.Where(r => GetResidentKey(r) == selectedResident).ToList();
                }

                // Separate valid data rows from no-data/star rows
                var validRecords = residentRecords.Where(r => r.Date.HasValue && r.Target != "*").ToList();
                var noDataRows = residentRecords.Where(r => r.Date.HasValue && (r.Target == "*" || r.Behavior_No_Data_Recorded == true)).ToList();

                // Date range is scoped to the selected resident's data window
                if (validRecords.Any())
                {
                    viewModel.GlobalMinDate = validRecords.Min(r => r.Date!.Value.Date);
                    viewModel.GlobalMaxDate = validRecords.Max(r => r.Date!.Value.Date);
                }

                // Build available targets and subcategories
                viewModel.AvailableTargets = validRecords
                    .Select(r => r.Target ?? "Unknown")
                    .Where(t => t != "Unknown")
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();

                viewModel.AvailableSubcategories = validRecords
                    .Where(r => !string.IsNullOrEmpty(r.Subcategory) && r.Subcategory != "*")
                    .GroupBy(r => r.Target ?? "Unknown")
                    .ToDictionary(
                        g => g.Key,
                        g => g.SelectMany(r => ParseSubcategories(r.Subcategory))
                              .Distinct()
                              .OrderBy(s => s)
                              .ToList()
                    );

                // Apply shift filter if provided
                var filteredRecords = validRecords;
                if (selectedShifts != null && selectedShifts.Any())
                {
                    filteredRecords = filteredRecords
                        .Where(r => selectedShifts.Contains(GetShift(r.Time)))
                        .ToList();
                }

                // Build missing data ranges
                viewModel.MissingDataRanges = BuildMissingDataRanges(noDataRows, residentRecords);

                // Group and aggregate
                List<DailyBehaviorCount> grouped = period switch
                {
                    
                    AggregationPeriod.Day => AggregateByDay(filteredRecords),
                    
                    AggregationPeriod.Week => AggregateByWeek(filteredRecords),
                    AggregationPeriod.Month => AggregateByMonth(filteredRecords),
                    _ => AggregateByDay(filteredRecords)
                };

                viewModel.DailyBehaviorCounts = grouped;
                viewModel.UnreducedBehaviorCounts = validRecords;
            }

            // --- LOAD MEDICATION DATA ---
            if (medRecords.Any())
            {
// Filter by selected resident
                if (!string.IsNullOrEmpty(selectedResident))
                {
                    medRecords = medRecords.Where(r => r.Person_ID == selectedResident).ToList();
                }

                // Unroll date ranges into daily points
                var dailyMeds = new List<DailyMedication>();
                var availableMeds = new HashSet<string>();

                foreach (var row in medRecords)
                {
                    // If End is missing, assume the medication is currently active up to today
                    if (row.Start.HasValue && !string.IsNullOrEmpty(row.Medication) && row.ParsedDose.HasValue)
                    {
                        availableMeds.Add(row.Medication);
                        
                        var maxBehaviorDate = viewModel.DailyBehaviorCounts.Any() ? viewModel.DailyBehaviorCounts.Max(c => c.Date) : DateTime.Today;
                        
                        var startDate = row.Start.Value.Date;
                        var endDate = row.End.HasValue ? row.End.Value.Date : maxBehaviorDate;
                        
                        if (startDate > endDate) continue;
                        
                        var days = (endDate - startDate).TotalDays;
                        if (days > 3650) days = 3650; // Cap at ~10 years
                        
                        for (int i = 0; i <= days; i++)
                        {
                            var currentDate = startDate.AddDays(i);
                            dailyMeds.Add(new DailyMedication
                            {
                                Date = currentDate,
                                Name = row.Medication,
                                Dose = row.ParsedDose.Value,
                                Unit = row.Unit ?? "",
                                LogDose = Math.Log10(row.ParsedDose.Value + 1)
                            });
                        }
                    }
                }
                
                // Add the newly parsed Excel medications to the pool
                if (viewModel.Medications != null)
                {
                    foreach (var med in viewModel.Medications)
                    {
                        if (!string.IsNullOrEmpty(med.Name) && double.TryParse(med.Dose, out double parsedDose))
                        {
                            availableMeds.Add(med.Name);
                            
                            var startDate = med.StartDate.Date;
                            var endDate = med.EndDate.Date;
                            
                            if (startDate > endDate) continue;
                            
                            var days = (endDate - startDate).TotalDays;
                            if (days > 3650) days = 3650;
                            
                            for (int i = 0; i <= days; i++)
                            {
                                var currentDate = startDate.AddDays(i);
                                dailyMeds.Add(new DailyMedication
                                {
                                    Date = currentDate,
                                    Name = med.Name,
                                    Dose = parsedDose,
                                    Unit = "",
                                    LogDose = Math.Log10(parsedDose + 1)
                                });
                            }
                        }
                    }
                }

                var groupedMeds = dailyMeds
                    .GroupBy(m => new { m.Name, m.Date })
                    .Select(g => g.OrderByDescending(x => x.Dose).First())
                    .OrderBy(m => m.Name).ThenBy(m => m.Date)
                    .ToList();
                    
                var filteredMeds = new List<DailyMedication>();
                
                // Reduce points to only start, end, and dose change events
                foreach (var group in groupedMeds.GroupBy(m => m.Name))
                {
                    var sorted = group.ToList();
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        var current = sorted[i];
                        bool isFirst = i == 0;
                        bool isLast = i == sorted.Count - 1;
                        
                        bool doseChangedFromPrev = !isFirst && current.Dose != sorted[i-1].Dose;
                        bool doseChangedToNext = !isLast && current.Dose != sorted[i+1].Dose;
                        
                        bool gapFromPrev = !isFirst && (current.Date - sorted[i-1].Date).TotalDays > 1;
                        bool gapToNext = !isLast && (sorted[i+1].Date - current.Date).TotalDays > 1;
                        
                        if (isFirst || isLast || doseChangedFromPrev || doseChangedToNext || gapFromPrev || gapToNext)
                        {
                            filteredMeds.Add(current);
                        }
                    }
                }

                viewModel.DailyMedications = filteredMeds.OrderBy(m => m.Date).ToList();
                viewModel.UnreducedMedications = groupedMeds;
                
                // Sort AvailableMedications descending by maximum historical dose
                viewModel.AvailableMedications = availableMeds
                    .OrderByDescending(m => {
                        var medDoses = viewModel.DailyMedications.Where(x => x.Name == m).ToList();
                        return medDoses.Any() ? medDoses.Max(x => x.Dose) : 0;
                    })
                    .ToList();
            }

            return viewModel;
        }

        #region Aggregation Methods

        private List<DailyBehaviorCount> AggregateByDay(List<BehaviorCsvRow> records)
        {
            return records
                .GroupBy(r => new { Date = r.Date!.Value.Date, Target = r.Target ?? "Unknown" })
                .Select(g =>
                {
                    var shiftCount = g.Select(r => GetShift(r.Time)).Distinct().Count();
                    return new DailyBehaviorCount
                    {
                        Date = g.Key.Date,
                        Label = $"{g.Key.Date:MMM dd}",
                        BehaviorType = "All",
                        Target = g.Key.Target,
                        Count = (int)g.Sum(x => x.Episode_Count ?? 0),
                        Rate = g.Where(x => x.Episode_Count.HasValue).Any()
                            ? g.Where(x => x.Episode_Count.HasValue).Average(x => x.Episode_Count.Value)
                            : 0,
                        AverageIntensity = ComputeGroupAverageIntensity(g),
                        TotalDuration = g.Sum(x => ComputeTotalDuration(x)),
                        Subcategories = g.SelectMany(x => ParseSubcategories(x.Subcategory)).Distinct().ToList(),
                        IntensityBuckets = AggregateBuckets(g).intensity,
                        DurationBuckets = AggregateBuckets(g).duration
                    };
                })
                .OrderBy(x => x.Date)
                .ToList();
        }

        

        private List<DailyBehaviorCount> AggregateByAverage(List<BehaviorCsvRow> records)
        {
            if (!records.Any()) return new List<DailyBehaviorCount>();
            
            var minDate = records.Min(r => r.Date!.Value.Date);
            var maxDate = records.Max(r => r.Date!.Value.Date);
            var totalDays = (maxDate - minDate).TotalDays + 1;
            if (totalDays <= 0) totalDays = 1;

            return records
                .GroupBy(r => new { Target = r.Target ?? "Unknown" })
                .Select(g =>
                {
                    return new DailyBehaviorCount
                    {
                        Date = minDate, // Align to the start of the timeframe
                        Label = $"Overall Average",
                        BehaviorType = "All",
                        Target = g.Key.Target,
                        Count = (int)g.Sum(x => x.Episode_Count ?? 0), // Total count over the period
                        Rate = g.Where(x => x.Episode_Count.HasValue).Any()
                            ? g.Where(x => x.Episode_Count.HasValue).Average(x => x.Episode_Count.Value)
                            : 0,
                        AverageIntensity = ComputeGroupAverageIntensity(g),
                        TotalDuration = g.Sum(x => ComputeTotalDuration(x)) / totalDays, // Daily average duration
                        Subcategories = g.SelectMany(x => ParseSubcategories(x.Subcategory)).Distinct().ToList(),
                        IntensityBuckets = AggregateBuckets(g).intensity,
                        DurationBuckets = AggregateBuckets(g).duration
                    };
                })
                .OrderBy(x => x.Target)
                .ToList();
        }

        

        private List<DailyBehaviorCount> AggregateByWeek(List<BehaviorCsvRow> records)
        {
            return records
                .GroupBy(r => new
                {
                    Year = r.Date!.Value.Year,
                    Week = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(r.Date!.Value, CalendarWeekRule.FirstDay, DayOfWeek.Sunday),
                    Target = r.Target ?? "Unknown"
                })
                .Select(g =>
                {
                    var totalShifts = g.Select(r => new { r.Date!.Value.Date, Shift = GetShift(r.Time) }).Distinct().Count();
                    return new DailyBehaviorCount
                    {
                        Date = FirstDateOfWeekISO8601(g.Key.Year, g.Key.Week),
                        Label = $"Week {g.Key.Week}",
                        BehaviorType = $"Week {g.Key.Week}",
                        Target = g.Key.Target,
                        Count = (int)g.Sum(x => x.Episode_Count ?? 0),
                        Rate = g.Where(x => x.Episode_Count.HasValue).Any()
                            ? g.Where(x => x.Episode_Count.HasValue).Average(x => x.Episode_Count.Value)
                            : 0,
                        AverageIntensity = ComputeGroupAverageIntensity(g),
                        TotalDuration = g.Sum(x => ComputeTotalDuration(x)),
                        Subcategories = g.SelectMany(x => ParseSubcategories(x.Subcategory)).Distinct().ToList(),
                        IntensityBuckets = AggregateBuckets(g).intensity,
                        DurationBuckets = AggregateBuckets(g).duration
                    };
                })
                .OrderBy(x => x.Date)
                .ToList();
        }

        private List<DailyBehaviorCount> AggregateByMonth(List<BehaviorCsvRow> records)
        {
            return records
                .GroupBy(r => new { Year = r.Date!.Value.Year, Month = r.Date!.Value.Month, Target = r.Target ?? "Unknown" })
                .Select(g =>
                {
                    var totalShifts = g.Select(r => new { r.Date!.Value.Date, Shift = GetShift(r.Time) }).Distinct().Count();
                    return new DailyBehaviorCount
                    {
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Label = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                        BehaviorType = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                        Target = g.Key.Target,
                        Count = (int)g.Sum(x => x.Episode_Count ?? 0),
                        Rate = g.Where(x => x.Episode_Count.HasValue).Any()
                            ? g.Where(x => x.Episode_Count.HasValue).Average(x => x.Episode_Count.Value)
                            : 0,
                        AverageIntensity = ComputeGroupAverageIntensity(g),
                        TotalDuration = g.Sum(x => ComputeTotalDuration(x)),
                        Subcategories = g.SelectMany(x => ParseSubcategories(x.Subcategory)).Distinct().ToList(),
                        IntensityBuckets = AggregateBuckets(g).intensity,
                        DurationBuckets = AggregateBuckets(g).duration
                    };
                })
                .OrderBy(x => x.Date)
                .ToList();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Mean of all non-null Episode_Count values in the group.
        /// This is the Rate metric: average episodes per recorded datapoint.
        /// </summary>
        private static double MeanEpisodeCount(IEnumerable<BehaviorCsvRow> rows)
        {
            var validRows = rows.Where(x => x.Episode_Count.HasValue && x.Behavior_LOA != true).ToList();
            return validRows.Any() ? validRows.Average(x => x.Episode_Count!.Value) : 0;
        }

        /// <summary>
        /// Returns Person_ID when present; falls back to Name.
        /// This is the key used to populate and filter the resident selector.
        /// </summary>
        private static string GetResidentKey(BehaviorCsvRow r) =>
            !string.IsNullOrEmpty(r.Person_ID) ? r.Person_ID : r.Name ?? "";

        private string GetShift(TimeSpan? time)
        {
            if (!time.HasValue) return "Unknown";
            if (time.Value.Hours >= 7 && time.Value.Hours < 15) return "7-3";
            if (time.Value.Hours >= 15 && time.Value.Hours < 23) return "3-11";
            return "11-7";
        }

        private int GetShiftOffset(string shift)
        {
            return shift switch
            {
                "7-3" => 7,
                "3-11" => 15,
                "11-7" => 23,
                _ => 0
            };
        }

        private static DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;
            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = CultureInfo.CurrentCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            var weekNum = weekOfYear;
            if (firstWeek <= 1) weekNum -= 1;
            var result = firstThursday.AddDays(weekNum * 7);
            return result.AddDays(-3);
        }

        /// <summary>
        /// Computes weighted average intensity across a group, correctly weighting episodes.
        /// Intensity levels: 1=Minimal, 2=Mild, 3=Moderate, 4=Severe, 5=Extreme.
        /// Ignores Intensity_01 which is "Not Specified".
        /// </summary>
        private double ComputeGroupAverageIntensity(IEnumerable<BehaviorCsvRow> group)
        {
            double totalWeight = 0;
            double totalCount = 0;

            foreach (var row in group)
            {
                if (row.Intensity_02_Count.HasValue && row.Intensity_02_Count.Value > 0) { totalWeight += row.Intensity_02_Count.Value * 1; totalCount += row.Intensity_02_Count.Value; }
                if (row.Intensity_03_Count.HasValue && row.Intensity_03_Count.Value > 0) { totalWeight += row.Intensity_03_Count.Value * 2; totalCount += row.Intensity_03_Count.Value; }
                if (row.Intensity_04_Count.HasValue && row.Intensity_04_Count.Value > 0) { totalWeight += row.Intensity_04_Count.Value * 3; totalCount += row.Intensity_04_Count.Value; }
                if (row.Intensity_05_Count.HasValue && row.Intensity_05_Count.Value > 0) { totalWeight += row.Intensity_05_Count.Value * 4; totalCount += row.Intensity_05_Count.Value; }
            }

            return totalCount > 0 ? totalWeight / totalCount : 0;
        }

        /// <summary>
        /// Sums all duration bucket counts for a row
        /// </summary>
        private double ComputeTotalDuration(BehaviorCsvRow row)
        {
            double total = 0;
            if (row.Duration_01_Count.HasValue) total += row.Duration_01_Count.Value;
            if (row.Duration_02_Count.HasValue) total += row.Duration_02_Count.Value;
            if (row.Duration_03_Count.HasValue) total += row.Duration_03_Count.Value;
            if (row.Duration_04_Count.HasValue) total += row.Duration_04_Count.Value;
            if (row.Duration_05_Count.HasValue) total += row.Duration_05_Count.Value;
            if (row.Duration_06_Count.HasValue) total += row.Duration_06_Count.Value;
            return total;
        }

        /// <summary>
        /// Aggregates per-level intensity and per-bucket duration counts across a set of rows
        /// </summary>
        private (double[] intensity, double[] duration) AggregateBuckets(IEnumerable<BehaviorCsvRow> rows)
        {
            var intensity = new double[5];
            var duration = new double[6];
            foreach (var r in rows)
            {
                // Shift intensities down by 1 because Intensity_01 is "Not Specified"
                intensity[0] += r.Intensity_02_Count ?? 0;
                intensity[1] += r.Intensity_03_Count ?? 0;
                intensity[2] += r.Intensity_04_Count ?? 0;
                intensity[3] += r.Intensity_05_Count ?? 0;
                // intensity[4] would be Intensity_06 if it existed
                duration[0] += r.Duration_01_Count ?? 0;
                duration[1] += r.Duration_02_Count ?? 0;
                duration[2] += r.Duration_03_Count ?? 0;
                duration[3] += r.Duration_04_Count ?? 0;
                duration[4] += r.Duration_05_Count ?? 0;
                duration[5] += r.Duration_06_Count ?? 0;
            }
            return (intensity, duration);
        }

        private List<string> ParseSubcategories(string? subcategory)
        {
            if (string.IsNullOrEmpty(subcategory) || subcategory == "*")
                return new List<string>();

            return subcategory.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private List<MissingDataRange> BuildMissingDataRanges(List<BehaviorCsvRow> noDataRows, List<BehaviorCsvRow> allRecords)
        {
            var ranges = new List<MissingDataRange>();

            // Get dates with Behavior_No_Data_Recorded or LOA
            var noDataDates = noDataRows
                .Where(r => r.Date.HasValue)
                .Select(r => new
                {
                    Date = r.Date!.Value.Date,
                    IsLOA = r.Behavior_LOA == true
                })
                .Distinct()
                .OrderBy(x => x.Date)
                .ToList();

            if (!noDataDates.Any()) return ranges;

            // Merge consecutive dates into ranges
            DateTime? rangeStart = null;
            DateTime? rangePrev = null;
            string reason = "No Data";

            foreach (var nd in noDataDates)
            {
                if (rangeStart == null)
                {
                    rangeStart = nd.Date;
                    rangePrev = nd.Date;
                    reason = nd.IsLOA ? "LOA" : "No Data";
                }
                else if (nd.Date == rangePrev!.Value.AddDays(1))
                {
                    rangePrev = nd.Date;
                }
                else
                {
                    ranges.Add(new MissingDataRange
                    {
                        Start = rangeStart.Value,
                        End = rangePrev!.Value,
                        Reason = reason
                    });
                    rangeStart = nd.Date;
                    rangePrev = nd.Date;
                    reason = nd.IsLOA ? "LOA" : "No Data";
                }
            }

            if (rangeStart != null)
            {
                ranges.Add(new MissingDataRange
                {
                    Start = rangeStart.Value,
                    End = rangePrev!.Value,
                    Reason = reason
                });
            }

            return ranges;
        }

        #endregion

        #region CSV Row Model

        public class BehaviorCsvRow
        {
            [Name("Date")]
            public DateTime? Date { get; set; }

            [Name("Time")]
            public TimeSpan? Time { get; set; }

            [Name("Target_Clean")]
            public string? Target { get; set; }

            [Name("Subcategory")]
            public string? Subcategory { get; set; }

            [Name("Episode_Count")]
            public double? Episode_Count { get; set; }

            [Name("Duration_Specific")]
            public string? Duration_Specific { get; set; }

            [Name("Time_Sample_Percent")]
            public double? Time_Sample_Percent { get; set; }

            [Name("Duration_01_Count")]
            public double? Duration_01_Count { get; set; }

            [Name("Duration_02_Count")]
            public double? Duration_02_Count { get; set; }

            [Name("Duration_03_Count")]
            public double? Duration_03_Count { get; set; }

            [Name("Duration_04_Count")]
            public double? Duration_04_Count { get; set; }

            [Name("Duration_05_Count")]
            public double? Duration_05_Count { get; set; }

            [Name("Duration_06_Count")]
            public double? Duration_06_Count { get; set; }

            [Name("Intensity_01_Count")]
            public double? Intensity_01_Count { get; set; }

            [Name("Intensity_02_Count")]
            public double? Intensity_02_Count { get; set; }

            [Name("Intensity_03_Count")]
            public double? Intensity_03_Count { get; set; }

            [Name("Intensity_04_Count")]
            public double? Intensity_04_Count { get; set; }

            [Name("Intensity_05_Count")]
            public double? Intensity_05_Count { get; set; }

            [Name("Behavior_Notes")]
            public string? Behavior_Notes { get; set; }

            [Name("Behavior_LOA")]
            public bool? Behavior_LOA { get; set; }

            [Name("Behavior_None")]
            public bool? Behavior_None { get; set; }

            [Name("Behavior_No_Data_Recorded")]
            public bool? Behavior_No_Data_Recorded { get; set; }

            [Name("Name")]
            public string? Name { get; set; }

            [Name("Person_ID")]
            public string? Person_ID { get; set; }
        }

        private class MedicationCsvRow
        {
            [Name("Person_ID")]
            public string? Person_ID { get; set; }

            [Name("Medication")]
            public string? Medication { get; set; }

            [Name("Dose")]
            public string? DoseRaw { get; set; }

            [Name("Unit")]
            public string? Unit { get; set; }

            [Name("Start")]
            public DateTime? Start { get; set; }

            [Name("End")]
            public DateTime? End { get; set; }

            [Ignore]
            public double? ParsedDose 
            {
                get 
                {
                    if (string.IsNullOrWhiteSpace(DoseRaw)) return null;
                    // Try to parse just the first numeric part if there's a space (e.g., "20 20")
                    var parts = DoseRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && double.TryParse(parts[0], out double result))
                    {
                        return result;
                    }
                    return null;
                }
            }
        }

        #endregion
    }
}
