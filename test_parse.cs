using System;
using System.IO;
using System.Threading.Tasks;
using PsychDashboard.Services;
using Microsoft.Extensions.DependencyInjection;

class Program {
    static async Task Main() {
        var services = new ServiceCollection();
        services.AddSingleton<ExcelParsingService>();
        var provider = services.BuildServiceProvider();
        
        var service = provider.GetRequiredService<ExcelParsingService>();
        
        // Try parsing a workbook from the python script's input folder
        string path = "/Users/canderson/Python/behavior/data_in/Test_Workbook.xlsm";
        if (File.Exists(path)) {
            using var stream = File.OpenRead(path);
            var result = await service.ParseWorkbookAsync(stream);
            Console.WriteLine($"Parsed {result.Behaviors.Count} behaviors and {result.Medications.Count} medications from {path}");
        } else {
            // Find any xlsm
            var files = Directory.GetFiles("/Users/canderson/Python", "*.xlsm", SearchOption.AllDirectories);
            if (files.Length > 0) {
                path = files[0];
                using var stream = File.OpenRead(path);
                var result = await service.ParseWorkbookAsync(stream);
                Console.WriteLine($"Parsed {result.Behaviors.Count} behaviors and {result.Medications.Count} medications from {path}");
            } else {
                Console.WriteLine("No xlsm files found.");
            }
        }
    }
}
