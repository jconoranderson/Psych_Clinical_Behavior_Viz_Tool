using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;

namespace PsychDashboard.Services
{
    public class ParsedWorkbookResult
    {
        public string SourceFileName { get; set; } = "";
        public string ResidentName { get; set; } = "";
        public List<WorkbookIssue> Issues { get; set; } = new();
        public List<PatientHistoryService.BehaviorCsvRow> Behaviors { get; set; } = new();
        public List<PsychDashboard.Models.Medication> Medications { get; set; } = new();
        public List<string> IntensityLabels { get; set; } = new();
        public List<string> DurationLabels { get; set; } = new();
    }

    public class ExcelParsingService
    {
        private static readonly string[] MonthSheets = { "July", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "June" };

        public ExcelParsingService()
        {
            // Required for ExcelDataReader
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public Task<ParsedWorkbookResult> ParseWorkbookAsync(Stream fileStream, string fileName = "Workbook")
        {
            using var reader = ExcelReaderFactory.CreateReader(fileStream);
            using var data = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
            });
            return Task.FromResult(ParseDataSet(data, fileName));
        }

        internal ParsedWorkbookResult ParseDataSet(DataSet result, string fileName)
        {
            var results = new ParsedWorkbookResult { SourceFileName = fileName };
            string studentName = "";
            string personId = "";
            int startYear = 0;
            void Report(string sheet, int row, int column, string message,
                WorkbookIssueSeverity severity = WorkbookIssueSeverity.Warning) =>
                results.Issues.Add(new(fileName, sheet, CellAddress(row, column), severity, message));

            // 1. Extract Student Info
            if (result.Tables.Contains("STUDENT INFO"))
            {
                var infoTable = result.Tables["STUDENT INFO"]!;
                if (infoTable.Rows.Count > 0 && infoTable.Columns.Count > 0)
                {
                    studentName = infoTable.Rows[0][0]?.ToString()?.Trim() ?? "";
                    personId = studentName; // In old system they sometimes just used name, or we can look for ID
                }
            }
            else
            {
                // Fallback for older formats: Check C1 in any month sheet
                foreach (var mSheet in MonthSheets)
                {
                    if (result.Tables.Contains(mSheet))
                    {
                        var mTable = result.Tables[mSheet]!;
                        if (mTable.Rows.Count > 0 && mTable.Columns.Count > 2)
                        {
                            var name = mTable.Rows[0][2]?.ToString()?.Trim(); // C1
                            if (!string.IsNullOrEmpty(name))
                            {
                                studentName = name;
                                personId = studentName;
                                break;
                            }
                        }
                    }
                }
            }

            // Determine year
            bool yearFound = false;
            if (result.Tables.Contains("YiVis"))
            {
                var yivisTable = result.Tables["YiVis"]!;
                if (yivisTable.Rows.Count >= 5 && yivisTable.Columns.Count >= 2)
                {
                    var yearStr = yivisTable.Rows[4][1]?.ToString(); // B5
                    if (!string.IsNullOrEmpty(yearStr) && yearStr.Contains("-"))
                    {
                        var parts = yearStr.Split('-');
                        if (parts.Length > 0 && int.TryParse(parts[0], out int sy))
                        {
                            startYear = sy;
                            if (startYear < 100) startYear += 2000;
                            yearFound = true;
                        }
                    }
                }
            }

            if (!yearFound)
            {
                // Fallback for older format: Check B4 in Month Notes Sheets
                var monthNotesDatasheetNames = new[] { "JanN", "FebN", "MarN", "AprN", "MayN", "JuneN", "JulN", "AugN", "SeptN", "OctN", "NovN", "DecN" };
                foreach (var nSheet in monthNotesDatasheetNames)
                {
                    if (result.Tables.Contains(nSheet))
                    {
                        var nTable = result.Tables[nSheet]!;
                        if (nTable.Rows.Count >= 4 && nTable.Columns.Count >= 2)
                        {
                            var monthYear = nTable.Rows[3][1]?.ToString()?.Replace("\"", "")?.Trim(); // B4
                            if (!string.IsNullOrEmpty(monthYear))
                            {
                                var parts = monthYear.Split(' ');
                                if (parts.Length > 1 && int.TryParse(parts[1], out int parsedYear))
                                {
                                    int monthIndex = Array.IndexOf(monthNotesDatasheetNames, nSheet);
                                    // Jan-Jun (0-5) are in startYear + 1, Jul-Dec (6-11) are in startYear
                                    startYear = (monthIndex < 6) ? parsedYear - 1 : parsedYear;
                                    yearFound = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            results.ResidentName = studentName;
            if (string.IsNullOrWhiteSpace(studentName))
                Report("STUDENT INFO", 0, 0, "Resident name is missing. Enter the resident name before uploading.", WorkbookIssueSeverity.Error);
            if (!yearFound || startYear < 2000 || startYear > 2099)
                Report("YiVis", 4, 1, "A valid school year (2000–2099) is required in YiVis B5 or a month notes sheet B4.", WorkbookIssueSeverity.Error);
            if (!MonthSheets.Any(result.Tables.Contains))
                Report("", -1, -1, "No supported monthly sheets were found. Use the behavior workbook template.", WorkbookIssueSeverity.Error);
            if (results.Issues.Any(issue => issue.Severity == WorkbookIssueSeverity.Error))
                return results;

            bool schemaInitialized = false;
            // 2. Extract Data from Monthly Sheets
            foreach (var sheetName in MonthSheets)
            {
                if (result.Tables.Contains(sheetName))
                {
                    var table = result.Tables[sheetName]!;
                    if (table.Rows.Count < 3 || table.Columns.Count < 22)
                    {
                        Report(sheetName, -1, -1, "The monthly sheet is missing required headers or target columns.", WorkbookIssueSeverity.Error);
                        continue;
                    }

                    var row0 = table.Rows[0]; // Target names
                    var row1 = table.Rows[1]; // Metric group headers
                    var bucketHeaderRow = table.Rows.Count > 2 ? table.Rows[2] : row1; // Actual per-person bins
                    var hasDedicatedBucketHeaderRow = table.Rows.Count > 2;

                    // Find how many targets we have
                    // Block starts at col 4, each block is 18 cols wide
                    var targetBlocks = new List<(string TargetName, int StartCol)>();
                    for (int c = 4; c < table.Columns.Count - 17; c += 18)
                    {
                        var tName = row0[c]?.ToString()?.Trim();
                        if (IsWorkbookBehaviorName(tName))
                        {
                            targetBlocks.Add((tName!, c));
                        }
                    }

                    foreach (var block in targetBlocks)
                    {
                        var duration = ReadMetricBuckets(bucketHeaderRow, bucketHeaderRow, block.StartCol + 6, 6,
                            new[] { "Not Specified", "<5 min", "6-10 min", "11-20 min", "21-30 min", "31-60 min" }, hasDedicatedBucketHeaderRow);
                        var intensity = ReadMetricBuckets(bucketHeaderRow, bucketHeaderRow, block.StartCol + 12, 5,
                            new[] { "Not Specified", "1", "2", "3", "4" }, hasDedicatedBucketHeaderRow);
                        if (duration.Labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != duration.Labels.Count ||
                            intensity.Labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != intensity.Labels.Count)
                            Report(sheetName, 2, block.StartCol,
                                "Bucket labels must be unique within each metric. Correct duplicate intensity or duration labels.", WorkbookIssueSeverity.Error);
                        if (schemaInitialized &&
                            (!results.DurationLabels.SequenceEqual(duration.Labels, StringComparer.OrdinalIgnoreCase) ||
                             !results.IntensityLabels.SequenceEqual(intensity.Labels, StringComparer.OrdinalIgnoreCase)))
                            Report(sheetName, 2, block.StartCol,
                                "Intensity or duration labels differ between targets or months. Use the same labels in the same order throughout this workbook.", WorkbookIssueSeverity.Error);
                        else if (!schemaInitialized)
                        {
                            results.DurationLabels = duration.Labels;
                            results.IntensityLabels = intensity.Labels;
                            schemaInitialized = true;
                        }
                    }

                    // Determine month number
                    int monthNum = GetMonthNum(sheetName);
                    // If month is Jan-June, it's startYear + 1
                    int currentYear = (monthNum < 7) ? startYear + 1 : startYear;

                    int lastValidDayNum = -1;

                    // Row 3 contains bucket headers; observations start on row 4.
                    for (int r = 3; r < table.Rows.Count; r++)
                    {
                        var row = table.Rows[r];
                        var dateStr = row[0]?.ToString();
                        int dayNum;
                        var hasEnteredData = IsTruthy(row[3]) || !string.IsNullOrWhiteSpace(row[2]?.ToString()) ||
                            targetBlocks.Any(block => Enumerable.Range(block.StartCol + 3, 14).Any(column =>
                            {
                                var text = row[column]?.ToString();
                                return !string.IsNullOrWhiteSpace(text) &&
                                    (!double.TryParse(text, out var value) || value != 0);
                            }));

                        if (string.IsNullOrWhiteSpace(dateStr))
                        {
                            if (lastValidDayNum != -1)
                            {
                                // Carry forward the date from the previous row (merged cells for shifts)
                                dayNum = lastValidDayNum;
                            }
                            else
                            {
                                if (hasEnteredData) Report(sheetName, r, 0, "Missing day; this shift was skipped.");
                                continue;
                            }
                        }
                        else if (int.TryParse(dateStr, out int parsedDay))
                        {
                            dayNum = parsedDay;
                            lastValidDayNum = parsedDay;
                        }
                        else
                        {
                            // If there is text in the date column, it could be a typo or a summary row.
                            // If it contains "Total", we know we've reached the bottom summary block.
                            if (dateStr.Contains("Total", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }
                            lastValidDayNum = -1;
                            if (hasEnteredData) Report(sheetName, r, 0, "Invalid day; this shift was skipped.");
                            continue;
                        }

                        DateTime? date = null;
                        try
                        {
                            date = new DateTime(currentYear, monthNum, dayNum);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            if (hasEnteredData) Report(sheetName, r, 0, "Day is invalid for this month; this shift was skipped.");
                            continue;
                        }

                        var shiftStr = row[1]?.ToString()?.Trim();
                        if (hasEnteredData && GetTimeFromShift(shiftStr) == null)
                        {
                            Report(sheetName, r, 1, "Unrecognized shift; use 7-3, 3-11, or 11-7. This shift was skipped.");
                            continue;
                        }
                        var noDataStr = row[2]?.ToString();
                        // Workbook versions differ here: some use a dedicated
                        // checkbox/formula in column D, while others write
                        // "No Behaviors" in column C alongside the missing-data
                        // statuses. Both represent a recorded zero-frequency
                        // observation and must not be mistaken for a blank
                        // preformatted future row.
                        var behaviorNone = IsTruthy(row[3]) ||
                            (!string.IsNullOrWhiteSpace(noDataStr) &&
                             noDataStr.Contains("No Behaviors", StringComparison.OrdinalIgnoreCase));

                        bool noData = !string.IsNullOrEmpty(noDataStr) &&
                            (noDataStr.Contains("No Data", StringComparison.OrdinalIgnoreCase) ||
                             noDataStr.Contains("LOA", StringComparison.OrdinalIgnoreCase));
                        bool loa = !string.IsNullOrEmpty(noDataStr) &&
                            noDataStr.Contains("LOA", StringComparison.OrdinalIgnoreCase);

                        // No Data/LOA is missing data, not a zero-frequency observation.
                        if (noData)
                        {
                            results.Behaviors.Add(new PatientHistoryService.BehaviorCsvRow
                            {
                                Date = date,
                                Time = GetTimeFromShift(shiftStr),
                                Target = "*",
                                Behavior_No_Data_Recorded = noData,
                                Behavior_LOA = loa,
                                Name = studentName,
                                Person_ID = personId
                            });
                            continue;
                        }

                        var parsedTargets = targetBlocks.Select(block =>
                        {
                            var frequencyText = row[block.StartCol + 3]?.ToString();
                            var isBlank = string.IsNullOrWhiteSpace(frequencyText);
                            var isValid = double.TryParse(frequencyText, out var frequency) &&
                                double.IsFinite(frequency) && frequency >= 0 && frequency <= int.MaxValue && frequency == Math.Truncate(frequency);
                            if (!isBlank && !isValid)
                                Report(sheetName, r, block.StartCol + 3, "Frequency must be a nonnegative whole number; this target observation was skipped.");
                            return new { Block = block, Frequency = isValid ? (double?)frequency : null, Invalid = !isBlank && !isValid };
                        }).ToList();

                        // Workbook templates contain preformatted future rows whose
                        // formulas can evaluate to zero. They are not observations.
                        // A checked "no behaviors" cell makes an all-zero shift valid;
                        // otherwise at least one target must contain a positive count.
                        var hasBehaviorOccurrence = parsedTargets.Any(item => item.Frequency > 0);
                        if (!behaviorNone && !hasBehaviorOccurrence)
                            continue;

                        // Process each target block
                        foreach (var parsedTarget in parsedTargets)
                        {
                            if (parsedTarget.Invalid) continue;
                            var block = parsedTarget.Block;
                            int b = block.StartCol;
                            bool invalidBuckets = false;
                            for (int column = b + 6; column < b + 17; column++)
                            {
                                var text = row[column]?.ToString();
                                if (string.IsNullOrWhiteSpace(text)) continue;
                                if (!double.TryParse(text, out var count) || !double.IsFinite(count) || count < 0 || count > int.MaxValue || count != Math.Truncate(count))
                                {
                                    Report(sheetName, r, column, "Bucket count must be a nonnegative whole number; this target observation was skipped.");
                                    invalidBuckets = true;
                                }
                                else if (count > 0 && string.IsNullOrWhiteSpace(bucketHeaderRow[column]?.ToString()))
                                {
                                    Report(sheetName, r, column, "A bucket count has no label; this target observation was skipped. Add a matching header.");
                                    invalidBuckets = true;
                                }
                            }
                            if (invalidBuckets) continue;
                            var durationBuckets = ReadMetricBuckets(
                                bucketHeaderRow, row, b + 6, 6,
                                new[] { "Not Specified", "<5 min", "6-10 min", "11-20 min", "21-30 min", "31-60 min" },
                                hasDedicatedBucketHeaderRow);
                            var intensityBuckets = ReadMetricBuckets(
                                bucketHeaderRow, row, b + 12, 5,
                                new[] { "Not Specified", "1", "2", "3", "4" },
                                hasDedicatedBucketHeaderRow);


                            var csvRow = new PatientHistoryService.BehaviorCsvRow
                            {
                                Date = date,
                                Time = GetTimeFromShift(shiftStr),
                                Target = block.TargetName,
                                Subcategory = row[b]?.ToString(),
                                // Once a shift is known to be recorded, an empty target
                                // is a meaningful zero relative to the other targets.
                                Episode_Count = parsedTarget.Frequency ?? 0,
                                Duration_Specific = row[b + 4]?.ToString(),
                                Time_Sample_Percent = TryParseDouble(row[b + 5]?.ToString()),
                                Duration_01_Count = durationBuckets.Values.ElementAtOrDefault(0),
                                Duration_02_Count = durationBuckets.Values.ElementAtOrDefault(1),
                                Duration_03_Count = durationBuckets.Values.ElementAtOrDefault(2),
                                Duration_04_Count = durationBuckets.Values.ElementAtOrDefault(3),
                                Duration_05_Count = durationBuckets.Values.ElementAtOrDefault(4),
                                Duration_06_Count = durationBuckets.Values.ElementAtOrDefault(5),
                                Intensity_01_Count = intensityBuckets.Values.ElementAtOrDefault(0),
                                Intensity_02_Count = intensityBuckets.Values.ElementAtOrDefault(1),
                                Intensity_03_Count = intensityBuckets.Values.ElementAtOrDefault(2),
                                Intensity_04_Count = intensityBuckets.Values.ElementAtOrDefault(3),
                                Intensity_05_Count = intensityBuckets.Values.ElementAtOrDefault(4),
                                Behavior_Notes = row[b + 17]?.ToString(),
                                Behavior_No_Data_Recorded = noData,
                                Behavior_LOA = loa,
                                Behavior_None = behaviorNone,
                                Name = studentName,
                                Person_ID = personId
                            };

                            results.Behaviors.Add(csvRow);
                        }
                    }
                }

            }

            // --- 3. Extract Medication Data ---
            if (result.Tables.Contains("Year Custom Med")) // V1
            {
                var table = result.Tables["Year Custom Med"]!;
                int[] medCols = { 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 }; // O through X

                for (int i = 0; i < medCols.Length; i++)
                {
                    int c = medCols[i];
                    if (c >= table.Columns.Count || table.Rows.Count < 3) continue;

                    var medName = table.Rows[2][c]?.ToString()?.Replace("\"", "")?.Trim();
                    if (string.IsNullOrEmpty(medName)) continue;

                    for (int rowIdx = 3; rowIdx < 15; rowIdx++) // July to June
                    {
                        if (rowIdx >= table.Rows.Count) break;

                        var doseStr = table.Rows[rowIdx][c]?.ToString()?.Replace("\"", "");
                        if (string.IsNullOrWhiteSpace(doseStr)) continue;

                        // Remove shifts appended via {}
                        if (doseStr.Contains("{")) doseStr = doseStr.Split('{')[0].Trim();

                        if (double.TryParse(doseStr, out double dose) && double.IsFinite(dose) && dose >= 0)
                        {
                            int month = (rowIdx - 3 + 6) % 12 + 1; // row 3 is July (7)
                            int year = (month >= 7) ? startYear : startYear + 1;

                            results.Medications.Add(new PsychDashboard.Models.Medication
                            {
                                Name = medName,
                                Dose = dose.ToString(),
                                StartDate = new DateTime(year, month, 15),
                                EndDate = new DateTime(year, month, 15, 23, 59, 59)
                            });
                        }
                        else
                            Report("Year Custom Med", rowIdx, c, "Dose must be a finite, nonnegative number; this medication entry was skipped.");
                    }
                }
            }
            else if (result.Tables.Contains("MEDICATIONS")) // V2
            {
                var table = result.Tables["MEDICATIONS"]!;
                var medCols = new[]
                {
                        new { NameCol=0, Dose=0, Unit=1, M1=2, D1=3, Y1=4, M2=5, D2=6, Y2=7, R1=1 },   // A
                        new { NameCol=9, Dose=9, Unit=10, M1=11, D1=12, Y1=13, M2=14, D2=15, Y2=16, R1=1 }, // J
                        new { NameCol=18, Dose=18, Unit=19, M1=20, D1=21, Y1=22, M2=23, D2=24, Y2=25, R1=1 }, // S
                        new { NameCol=27, Dose=27, Unit=28, M1=29, D1=30, Y1=31, M2=32, D2=33, Y2=34, R1=1 }, // AB
                        new { NameCol=36, Dose=36, Unit=37, M1=38, D1=39, Y1=40, M2=41, D2=42, Y2=43, R1=1 }, // AK
                        new { NameCol=45, Dose=45, Unit=46, M1=47, D1=48, Y1=49, M2=50, D2=51, Y2=52, R1=1 }, // AT
                        new { NameCol=54, Dose=54, Unit=55, M1=56, D1=57, Y1=58, M2=59, D2=60, Y2=61, R1=1 }, // BC
                        new { NameCol=63, Dose=63, Unit=64, M1=65, D1=66, Y1=67, M2=68, D2=69, Y2=70, R1=1 }, // BL
                        new { NameCol=72, Dose=72, Unit=73, M1=74, D1=75, Y1=76, M2=77, D2=78, Y2=79, R1=1 }, // BU
                        new { NameCol=81, Dose=81, Unit=82, M1=83, D1=84, Y1=85, M2=86, D2=87, Y2=88, R1=1 }  // CD
                    };

                foreach (var mc in medCols)
                {
                    if (mc.NameCol >= table.Columns.Count || table.Rows.Count <= mc.R1) continue;

                    var medName = table.Rows[mc.R1][mc.NameCol]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(medName) || medName.Contains("Insert")) continue;
                    if (mc.Y2 >= table.Columns.Count)
                    {
                        Report("MEDICATIONS", mc.R1, mc.NameCol, "Medication columns are incomplete; this medication was skipped.");
                        continue;
                    }

                    for (int j = 4; j < 32; j++) // Rows 5 to 32 (0-indexed 4 to 31)
                    {
                        if (j >= table.Rows.Count) break;

                        var doseStr = table.Rows[j][mc.Dose]?.ToString();
                        if (string.IsNullOrWhiteSpace(doseStr)) continue;
                        if (doseStr.Contains("{")) doseStr = doseStr.Split('{')[0].Trim();

                        if (!double.TryParse(doseStr, out double dose) || !double.IsFinite(dose) || dose < 0)
                        {
                            Report("MEDICATIONS", j, mc.Dose, "Dose must be a finite, nonnegative number; this medication entry was skipped.");
                            continue;
                        }
                        var unitStr = table.Rows[j][mc.Unit]?.ToString();
                        if (!TryMedicationDate(table.Rows[j], mc.M1, mc.D1, mc.Y1, false, out var startDate))
                        {
                            Report("MEDICATIONS", j, mc.M1, "Invalid or incomplete start date; this medication entry was skipped. Enter a month and year, and a valid day if specified.");
                            continue;
                        }

                        DateTime endDate;
                        bool hasEndDate = new[] { mc.M2, mc.D2, mc.Y2 }
                            .Any(column => !string.IsNullOrWhiteSpace(table.Rows[j][column]?.ToString()));
                        if (hasEndDate)
                        {
                            if (!TryMedicationDate(table.Rows[j], mc.M2, mc.D2, mc.Y2, true, out endDate) || endDate < startDate)
                            {
                                Report("MEDICATIONS", j, mc.M2, "Invalid, incomplete, or reversed end date; this medication entry was skipped. Enter a month and year, and a valid day if specified, or leave the entire end date blank for an ongoing medication.");
                                continue;
                            }
                        }
                        else
                        {
                            endDate = results.Behaviors
                                .Where(behavior => behavior.Date.HasValue)
                                .Select(behavior => behavior.Date!.Value.Date)
                                .DefaultIfEmpty(startDate)
                                .Max();
                        }

                        results.Medications.Add(new PsychDashboard.Models.Medication
                        {
                            Name = medName,
                            Dose = dose.ToString(),
                            Unit = unitStr?.Trim() ?? "",
                            StartDate = startDate,
                            EndDate = endDate
                        });
                    }
                }
            }

            // Workbook target names are intentionally passed through as entered.
            // The warehouse supplies its separately normalized Target_Clean field,
            // but local workbooks must retain each distinct behavior label.

            if (!results.Behaviors.Any(row => row.Target != "*"))
                Report("", -1, -1, "No valid behavior observations were found. Check target names, dates, shifts and frequency entries.", WorkbookIssueSeverity.Error);
            return results;
        }

        private static string CellAddress(int row, int column)
        {
            if (row < 0 || column < 0) return "";
            var letters = "";
            for (int value = column + 1; value > 0; value = (value - 1) / 26)
                letters = (char)('A' + (value - 1) % 26) + letters;
            return $"{letters}{row + 1}";
        }

        private static bool TryMedicationDate(DataRow row, int monthColumn, int dayColumn,
            int yearColumn, bool endOfMonth, out DateTime date)
        {
            date = default;
            if (!int.TryParse(row[yearColumn]?.ToString(), out var year) || year < 2000 || year > 2100 ||
                !int.TryParse(row[monthColumn]?.ToString(), out var month) || month < 1 || month > 12)
                return false;
            var dayText = row[dayColumn]?.ToString();
            int day = endOfMonth ? DateTime.DaysInMonth(year, month) : 1;
            if (!string.IsNullOrWhiteSpace(dayText) && !int.TryParse(dayText, out day)) return false;
            if (day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
            date = new DateTime(year, month, day);
            return true;
        }

        private int GetMonthNum(string monthStr)
        {
            return monthStr switch
            {
                "Jan" => 1,
                "Feb" => 2,
                "Mar" => 3,
                "Apr" => 4,
                "May" => 5,
                "June" => 6,
                "July" => 7,
                "Aug" => 8,
                "Sep" => 9,
                "Oct" => 10,
                "Nov" => 11,
                "Dec" => 12,
                _ => 1
            };
        }

        private static bool IsWorkbookBehaviorName(string? target)
        {
            if (string.IsNullOrWhiteSpace(target) ||
                target == "0" ||
                target.Equals("Insert", StringComparison.OrdinalIgnoreCase))
                return false;

            return !target.StartsWith("Select Target", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTruthy(object? value)
        {
            if (value is bool boolean) return boolean;

            var text = value?.ToString()?.Trim();
            return text != null &&
                   (text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("x", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("checked", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("no behavior", StringComparison.OrdinalIgnoreCase) ||
                    text == "1");
        }

        private (List<string> Labels, List<double?> Values) ReadMetricBuckets(
            DataRow headerRow,
            DataRow dataRow,
            int startColumn,
            int count,
            IReadOnlyList<string> fallbackLabels,
            bool hasDedicatedHeaderRow)
        {
            var labels = new List<string>();
            var values = new List<double?>();
            for (var offset = 0; offset < count; offset++)
            {
                var column = startColumn + offset;
                var header = column < headerRow.Table.Columns.Count
                    ? headerRow[column]?.ToString()?.Trim()
                    : null;
                // On current templates, blank cells in the dedicated bucket
                // header row mean that level does not exist for this person.
                // Older templates without that row retain positional fallbacks.
                if (hasDedicatedHeaderRow && string.IsNullOrWhiteSpace(header))
                    continue;

                var label = string.IsNullOrWhiteSpace(header) ? fallbackLabels[offset] : header;
                if (IsNotSpecifiedLabel(label)) continue;

                labels.Add(label);
                values.Add(column < dataRow.Table.Columns.Count
                    ? TryParseDouble(dataRow[column]?.ToString())
                    : null);
            }
            return (labels, values);
        }

        private static bool IsNotSpecifiedLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return false;
            var normalized = new string(label.Where(char.IsLetterOrDigit).ToArray());
            return normalized.Equals("NotSpecified", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Unspecified", StringComparison.OrdinalIgnoreCase);
        }

        private TimeSpan? GetTimeFromShift(string? shift)
        {
            if (shift == "7-3") return new TimeSpan(7, 0, 0);
            if (shift == "3-11") return new TimeSpan(15, 0, 0);
            if (shift == "11-7") return new TimeSpan(23, 0, 0);
            return null;
        }

        private double? TryParseDouble(string? val)
        {
            if (double.TryParse(val, out double d)) return d;
            return null;
        }

    }
}
