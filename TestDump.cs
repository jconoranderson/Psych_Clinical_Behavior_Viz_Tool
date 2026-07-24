using System;
using System.IO;
using ExcelDataReader;

namespace PsychDashboard {
    public class TestDump {
        public static void Run() {
            var path = "/Users/canderson/ASP.NET/psych_visualization_tool/DP Beh Data 2024-25.xlsm";
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var stream = File.OpenRead(path);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet();
            Console.WriteLine($"Tables found: {result.Tables.Count}");
            foreach (System.Data.DataTable table in result.Tables) {
                Console.WriteLine($"- {table.TableName}");
                if (table.TableName == "YiVis") {
                    if (table.Rows.Count >= 5 && table.Columns.Count >= 2) {
                        Console.WriteLine($"YiVis B5: {table.Rows[4][1]?.ToString()}");
                    }
                }
            }
        }
    }
}
