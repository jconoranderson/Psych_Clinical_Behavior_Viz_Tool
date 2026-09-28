using System.Data;
using System.IO.Compression;
using System.Xml.Linq;
using PsychDashboard.Services;
using Xunit;

namespace PsychDashboard.Tests;

public sealed class ExcelParsingServiceTests
{
    private readonly ExcelParsingService _parser = new();

    [Fact]
    public async Task ExcelStreamPreservesWarningLocationAndValidData()
    {
        using var data = Workbook();
        data.Tables["MEDICATIONS"]!.Rows[4][5] = "8";
        using var stream = WriteXlsx(data);

        var result = await _parser.ParseWorkbookAsync(stream, "stream.xlsx");

        Assert.Single(result.Behaviors);
        Assert.Empty(result.Medications);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("stream.xlsx", issue.FileName);
        Assert.Equal("F5", issue.Cell);
    }

    [Fact]
    public void ValidWorkbookLoadsWithoutIssuesAndMedicationIsParsedOnce()
    {
        using var data = Workbook();
        var august = data.Tables["July"]!.Copy();
        august.TableName = "Aug";
        data.Tables.Add(august);

        var result = _parser.ParseDataSet(data, "valid.xlsx");

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.Behaviors.Count);
        Assert.Single(result.Medications);
        Assert.Equal(new DateTime(2024, 8, 1), result.Medications[0].EndDate);
        Assert.Equal(new[] { "Low", "High" }, result.IntensityLabels);
    }

    [Theory]
    [InlineData("8", "", "")]
    [InlineData("13", "", "2024")]
    [InlineData("2", "30", "2025")]
    [InlineData("6", "1", "2024")]
    [InlineData("", "1", "2024")]
    [InlineData("8", "oops", "2024")]
    public void InvalidEndDateSkipsOnlyThatMedicationAndReportsLocation(string month, string day, string year)
    {
        using var data = Workbook();
        var meds = data.Tables["MEDICATIONS"]!;
        meds.Rows[4][5] = month;
        meds.Rows[4][6] = day;
        meds.Rows[4][7] = year;
        meds.Rows.Add(meds.Rows[4].ItemArray);
        meds.Rows[5][5] = meds.Rows[5][6] = meds.Rows[5][7] = "";

        var result = _parser.ParseDataSet(data, "dates.xlsx");

        Assert.Single(result.Behaviors);
        Assert.Single(result.Medications);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("dates.xlsx", issue.FileName);
        Assert.Equal("MEDICATIONS", issue.Sheet);
        Assert.Equal("F5", issue.Cell);
        Assert.Equal(WorkbookIssueSeverity.Warning, issue.Severity);
        Assert.Contains("end date", issue.Message);
    }

    [Fact]
    public void BlankEndDayUsesLastDayOfValidMonth()
    {
        using var data = Workbook();
        data.Tables["MEDICATIONS"]!.Rows[4][5] = "2";
        data.Tables["MEDICATIONS"]!.Rows[4][7] = "2025";
        var result = _parser.ParseDataSet(data, "month.xlsx");
        Assert.Empty(result.Issues);
        Assert.Equal(new DateTime(2025, 2, 28), Assert.Single(result.Medications).EndDate);
    }

    [Theory]
    [InlineData(0, "32", "A5")]
    [InlineData(0, "typo", "A5")]
    [InlineData(1, "unknown", "B5")]
    [InlineData(7, "-1", "H5")]
    [InlineData(7, "NaN", "H5")]
    [InlineData(7, "oops", "H5")]
    [InlineData(16, "-2", "Q5")]
    public void BadObservationReportsCellAndPreservesGoodObservation(int column, string value, string cell)
    {
        using var data = Workbook();
        var month = data.Tables["July"]!;
        month.Rows.Add(month.Rows[3].ItemArray);
        month.Rows[4][column] = value;
        var result = _parser.ParseDataSet(data, "observations.xlsx");

        Assert.Single(result.Behaviors);
        Assert.Contains(result.Issues, issue => issue.Sheet == "July" && issue.Cell == cell && issue.Severity == WorkbookIssueSeverity.Warning);
    }

    [Fact]
    public void DuplicateBucketLabelsAreReported()
    {
        using var data = Workbook();
        data.Tables["July"]!.Rows[2][17] = "Low";
        var result = _parser.ParseDataSet(data, "duplicates.xlsx");
        Assert.Contains(result.Issues, issue => issue.Severity == WorkbookIssueSeverity.Error && issue.Message.Contains("unique"));
    }

    [Fact]
    public void ConflictingMonthLabelsBlockMerge()
    {
        using var data = Workbook();
        var august = data.Tables["July"]!.Copy();
        august.TableName = "Aug";
        august.Rows[2][17] = "Critical";
        data.Tables.Add(august);
        var result = _parser.ParseDataSet(data, "labels.xlsx");
        Assert.Contains(result.Issues, issue => issue.Sheet == "Aug" && issue.Severity == WorkbookIssueSeverity.Error);
        Assert.Throws<WorkbookValidationException>(() => WorkbookMergeService.Merge(null, [result]));
    }

    [Theory]
    [InlineData("STUDENT INFO")]
    [InlineData("YiVis")]
    [InlineData("July")]
    public void MissingRequiredMetadataBlocksImport(string sheet)
    {
        using var data = Workbook();
        data.Tables.Remove(sheet);
        var result = _parser.ParseDataSet(data, "missing.xlsx");
        Assert.Contains(result.Issues, issue => issue.Severity == WorkbookIssueSeverity.Error);
        Assert.Empty(result.Behaviors);
    }

    [Fact]
    public void BlankFutureRowsDoNotGenerateWarnings()
    {
        using var data = Workbook();
        var month = data.Tables["July"]!;
        month.Rows.Add();
        month.Rows[4][0] = "32";
        month.Rows[4][7] = "0";
        var result = _parser.ParseDataSet(data, "template.xlsx");
        Assert.Empty(result.Issues);
        Assert.Single(result.Behaviors);
    }

    private static DataSet Workbook()
    {
        var data = new DataSet();
        Table(data, "STUDENT INFO", 1, 1).Rows[0][0] = "Test Resident";
        Table(data, "YiVis", 2, 5).Rows[4][1] = "2024-2025";
        var month = Table(data, "July", 22, 4);
        month.Rows[0][4] = "Aggression";
        month.Rows[2][10] = "Short";
        month.Rows[2][11] = "Long";
        month.Rows[2][16] = "Low";
        month.Rows[2][17] = "High";
        month.Rows[3][0] = "1";
        month.Rows[3][1] = "7-3";
        month.Rows[3][7] = "2";
        month.Rows[3][10] = "2";
        month.Rows[3][16] = "2";
        var meds = Table(data, "MEDICATIONS", 8, 5);
        meds.Rows[1][0] = "Test Medication";
        meds.Rows[4][0] = "10";
        meds.Rows[4][1] = "mg";
        meds.Rows[4][2] = "7";
        meds.Rows[4][3] = "1";
        meds.Rows[4][4] = "2024";
        return data;
    }

    private static DataTable Table(DataSet data, string name, int columns, int rows)
    {
        var table = new DataTable(name);
        for (int c = 0; c < columns; c++) table.Columns.Add($"Column{c}");
        for (int r = 0; r < rows; r++) table.Rows.Add();
        data.Tables.Add(table);
        return table;
    }

    // Synthetic Excel input exercises the real reader without storing resident files.
    private static MemoryStream WriteXlsx(DataSet data)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            XNamespace sheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace docRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace contentNs = "http://schemas.openxmlformats.org/package/2006/content-types";
            void Write(string path, XElement xml)
            {
                using var output = zip.CreateEntry(path).Open();
                new XDocument(xml).Save(output);
            }
            Write("[Content_Types].xml", new XElement(contentNs + "Types",
                new XElement(contentNs + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(contentNs + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                data.Tables.Cast<DataTable>().Select((_, i) => new XElement(contentNs + "Override",
                    new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            Write("_rels/.rels", new XElement(relNs + "Relationships", new XElement(relNs + "Relationship",
                new XAttribute("Id", "rId1"), new XAttribute("Type", docRelNs.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
            Write("xl/workbook.xml", new XElement(sheetNs + "workbook", new XElement(sheetNs + "sheets",
                data.Tables.Cast<DataTable>().Select((table, i) => new XElement(sheetNs + "sheet",
                    new XAttribute("name", table.TableName), new XAttribute("sheetId", i + 1), new XAttribute(docRelNs + "id", $"rId{i + 1}"))))));
            Write("xl/_rels/workbook.xml.rels", new XElement(relNs + "Relationships",
                data.Tables.Cast<DataTable>().Select((_, i) => new XElement(relNs + "Relationship",
                    new XAttribute("Id", $"rId{i + 1}"), new XAttribute("Type", docRelNs.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{i + 1}.xml")))));
            for (int i = 0; i < data.Tables.Count; i++)
            {
                var table = data.Tables[i];
                Write($"xl/worksheets/sheet{i + 1}.xml", new XElement(sheetNs + "worksheet", new XElement(sheetNs + "sheetData",
                    table.Rows.Cast<DataRow>().Select((row, r) => new XElement(sheetNs + "row", new XAttribute("r", r + 1),
                        row.ItemArray.Select((value, c) => new XElement(sheetNs + "c",
                            new XAttribute("r", $"{(char)('A' + c)}{r + 1}"), new XAttribute("t", "inlineStr"),
                            new XElement(sheetNs + "is", new XElement(sheetNs + "t", value?.ToString() ?? "")))))))));
            }
        }
        stream.Position = 0;
        return stream;
    }
}
