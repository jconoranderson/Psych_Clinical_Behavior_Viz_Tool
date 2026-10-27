# Psych Clinical Behavior Viz Tool

## Overview

The Psychology Visualization Tool helps clinical staff explore behavior history alongside medication changes. Load Excel behavior workbooks or use configured warehouse CSV exports, then select behaviors, shifts, and a date range to explore the data.

![Dashboard showing monthly behavior rates, medication dose changes, a hover tooltip, behavior subcategories, and the history navigator](docs/images/dashboard-overview.png)

The example above shows monthly behavior rates as colored bars with medication doses overlaid as dotted lines. Hovering over the chart displays behavior values and medication details; the left panel breaks down behavior subcategories. Drag the highlighted window below the chart to explore a different period.

## Features

- **Workbook uploads:** Drag and drop or choose `.xlsx`, `.xlsm`, or `.xls` files. Combine workbooks to explore multiple years of history.
- **Behavior comparisons:** Choose which behaviors to display, switch between line and bar charts, and compare frequency or rate per recorded shift.
- **Time and shift filters:** Group observations by day, week, month, or rolling view; select shifts and adjust the date range.
- **Optional overlays:** Show medication dose changes, SCIP counts, intensity, duration, missing data, and trend lines.
- **SCIP workbook overlay:** Reads named columns CR–DI (row 1 headers; `0` or blank headers are ignored). Each SCIP procedure appears as a solid line with circular markers, summed by day, week, month, or the selected rolling range and filtered by the selected shifts. A separate frequency axis keeps SCIP counts distinct from behavior rates. Recorded zeros are retained; missing-data/LOA and unrecorded template rows are excluded. SCIP is available for uploaded workbooks.
- **Interactive exploration:** Inspect hover details, zoom into the chart, and navigate the full history using the range selector.
- **PDF export:** Export the current chart view for sharing or review.

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.

### Running Locally

1. Clone the repository:
   ```bash
   git clone https://github.com/jconoranderson/Psych_Clinical_Behavior_Viz_Tool.git
   ```
2. Navigate to the project directory:
   ```bash
   cd Psych_Clinical_Behavior_Viz_Tool
   ```
3. Run the application:
   ```bash
   dotnet watch run
   ```
4. Open your browser to the URL indicated in your terminal. The HTTP launch profile uses `http://localhost:5008`.

## Workbook loading

If a workbook cannot be read or contains no readable behavior data, the upload message identifies the file. No files from the failed batch are added, and previously loaded data remains available. Differences or repeated labels in workbook headers do not block loading.

## Saved viewing profiles

After loading a workbook, configure the chart and choose **Save current view**.
Give the profile a name and choose whether it applies only to this individual
(matched using the workbook name) or to any individual. Select a saved profile
and click **Apply** after loading another workbook. Use **Update** to explicitly
replace its settings, or **Rename** and **Delete** to manage it.

Profiles remember behavior and medication selections/colors, SCIP selections,
overlays, chart type, measure, grouping, shifts, and weekdays. They leave the
current date range unchanged. Variables missing from a workbook remain in the
saved profile; newly encountered variables are unselected when applying it.

Profiles are stored as JSON in localStorage under
`psych-dashboard.view-profiles.v1`. They persist across browser sessions on the
same site and browser profile. Clearing site data removes them; they do not sync
between computers. Only viewing settings, profile names, and optional individual
names are saved, never workbook observations or clinical notes.

## Technologies Used

- [ASP.NET Core Blazor](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor)
- [MudBlazor](https://mudblazor.com/)
- [Blazor-ApexCharts](https://github.com/apexcharts/Blazor-ApexCharts)
- [CsvHelper](https://joshclose.github.io/CsvHelper/)
