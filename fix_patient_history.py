import re

with open("Services/PatientHistoryService.cs", "r") as f:
    content = f.read()

# Replace GetPatientHistoryAsync signature
new_signature = """        public async Task<DashboardViewModel> GetPatientHistoryAsync(
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
"""

pattern = r"        public async Task<DashboardViewModel> GetPatientHistoryAsync\(.*?var records = csv\.GetRecords<BehaviorCsvRow>\(\)\.ToList\(\);\s+"
content = re.sub(pattern, new_signature, content, flags=re.DOTALL)


med_pattern = r"            // --- LOAD MEDICATION DATA ---.*?\n                var medRecords = csv\.GetRecords<MedicationCsvRow>\(\)\.ToList\(\);\s+"
med_replacement = "            // --- LOAD MEDICATION DATA ---\n            if (medRecords.Any())\n            {\n"
content = re.sub(med_pattern, med_replacement, content, flags=re.DOTALL)

# Add closing brace for the `if (medRecords.Any())` block, which replaced the File.Exists block.
# Wait, no, File.Exists block was `if (File.Exists(medicationPath)) { ... }` and we replaced it with `if (medRecords.Any()) { ... }`.
# The braces already exist!

with open("Services/PatientHistoryService.cs", "w") as f:
    f.write(content)

