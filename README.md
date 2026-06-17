# Psych Clinical Behavior Viz Tool

## Overview

The Psych Clinical Behavior Viz Tool is an advanced, Blazor-based analytical dashboard engineered specifically for psychology professionals and clinical staff. It is designed to ingest and translate highly complex, multidimensional behavioral datasets into actionable, intuitive visualizations.

In modern clinical environments, patient data is vast and multifaceted. This tool bridges the gap between raw data storage and clinical insight by interfacing directly with an underlying SQL data warehouse. It synthesizes longitudinal patient data to empower clinicians to:
- Monitor granular changes in patient behavior over extended periods of time.
- Correlate behavioral shifts with medical interventions, including complex medication regimens and dosage adjustments.
- Identify patterns and anomalies across various clinical factors to support evidence-based, personalized treatment plans.
- Navigate rigorous user requirements for data accuracy, temporal precision, and clear clinical presentation.

By transforming dense relational data into interactive visual insights, the tool significantly reduces the cognitive load on clinical staff and facilitates more informed, data-driven psychological assessments.

## Features

- **Interactive Visualizations:** Utilizes [Blazor-ApexCharts](https://github.com/apexcharts/Blazor-ApexCharts) for rendering dynamic, interactive charts.
- **Modern UI:** Built with [MudBlazor](https://mudblazor.com/) for a clean, responsive, and accessible user interface.
- **Data Integration:** Displays complex clinical and behavioral metrics in an easy-to-understand dashboard format.

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
4. Open your browser to the URL indicated in your terminal (usually `http://localhost:5000` or `https://localhost:5001`).

## Technologies Used

- [ASP.NET Core Blazor](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor)
- [MudBlazor](https://mudblazor.com/)
- [Blazor-ApexCharts](https://github.com/apexcharts/Blazor-ApexCharts)
- [CsvHelper](https://joshclose.github.io/CsvHelper/)
