using System;
using System.IO;
using System.Linq;
using PsychDashboard.Services;
using PsychDashboard.Models;

namespace PsychDashboard {
    public class TestParse {
        public static async System.Threading.Tasks.Task Run()
        {
            var path = "/Users/canderson/ASP.NET/psych_visualization_tool/DP Beh Data 2024-25.xlsm";
            using var stream = File.OpenRead(path);
            var excelService = new ExcelParsingService();
            var parsedWorkbook = await excelService.ParseWorkbookAsync(stream);
            
            var phService = new PatientHistoryService(null);
            var viewModel = await phService.GetPatientHistoryFromRecordsAsync(
                parsedWorkbook.Behaviors, parsedWorkbook.Medications,
                AggregationPeriod.Month);
                
            Console.WriteLine($"ViewModel Residents: {string.Join(", ", viewModel.AvailableResidents)}");
            Console.WriteLine($"ViewModel Data Points: {viewModel.DailyBehaviorCounts.Count}");
        }
    }
}
