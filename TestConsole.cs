using System;
using System.IO;
using System.Threading.Tasks;
using PsychDashboard.Services;
using PsychDashboard.Models;

namespace PsychDashboard {
    public class TestConsole {
        public static async Task RunAsync() {
            var path = "/Users/canderson/ASP.NET/psych_visualization_tool/DP Beh Data 2024-25.xlsm";
            using var stream = File.OpenRead(path);
            var excelService = new ExcelParsingService();
            var parsedWorkbook = await excelService.ParseWorkbookAsync(stream);
            Console.WriteLine($"Parsed Behaviors: {parsedWorkbook.Behaviors.Count}");
            foreach (var b in parsedWorkbook.Behaviors) {
                if (b.Name != "Test Student") {
                    Console.WriteLine($"Found other name! {b.Name} / {b.Person_ID}");
                    return;
                }
            }
            Console.WriteLine("All behaviors are for Test Student!");
        }
    }
}
