# Psychology Visualization Tool — Windows Server Deployment

This package is a framework-dependent ASP.NET Core application targeting .NET 8.
It is intended to run behind IIS on a 64-bit Windows Server.

## Server prerequisites

1. Install the current **.NET 8 ASP.NET Core Hosting Bundle** on the server.
   The SDK alone is not sufficient for IIS hosting because IIS also needs the
   ASP.NET Core Module.
2. Restart IIS after installing the Hosting Bundle:

   ```powershell
   iisreset
   ```

3. Enable the IIS Web Server role and create an HTTPS site/binding with a valid
   certificate.

## Deploy the package

1. Extract the ZIP to a versioned directory such as:

   ```text
   C:\Apps\PsychDashboard\releases\2026-07-29
   ```

2. Create an IIS application pool:

   - .NET CLR version: **No Managed Code**
   - Managed pipeline: **Integrated**
   - Enable 32-bit applications: **False**
   - Identity: a dedicated least-privilege service account

3. Point the IIS site/application at the extracted directory.
4. Give the application-pool identity read/execute access to the application
   directory. Give it read access to configured warehouse CSV locations only
   if Data Warehouse mode will be enabled.
5. Do not grant write access to the application directory unless operationally
   required.

The generated `web.config` starts the application with:

```text
dotnet .\PsychDashboard.dll
```

## Required production configuration

Set these as machine-level environment variables, application-pool environment
variables, or protected IIS configuration. Double underscores represent nested
.NET configuration keys.

```text
ASPNETCORE_ENVIRONMENT=Production
AllowedHosts=psych-dashboard.example.org
Security__RequireAuthentication=false
```

With authentication disabled, access must be restricted through the internal
network, IIS, or another control approved by IT.

When OpenID Connect authentication is approved later, configure:

```text
Authentication__Authority=https://identity-provider.example.org/tenant
Authentication__ClientId=<registered-application-client-id>
Authentication__ClientSecret=<secret-from-approved-secret-store>
Security__RequireAuthentication=true
```

Then register this redirect URI with the identity provider:

```text
https://psych-dashboard.example.org/signin-oidc
```

Never commit the client secret or place it in the deployment ZIP.

The application refuses to start only when authentication is explicitly enabled
but `Authentication__Authority` or `Authentication__ClientId` is missing.

## Optional warehouse configuration

Local Workbook is the default and does not read warehouse files. Configure
these only when the Data Warehouse integration is approved:

```text
DataSources__BehaviorCsvPath=D:\ApprovedData\behavior_recent.csv
DataSources__MedicationCsvPath=D:\ApprovedData\medications.csv
```

The application-pool identity needs read-only access to these files.

## Validation

After deployment:

1. Browse to `https://<hostname>/health`; it should return HTTP 200.
2. Browse to the application and verify it loads. If authentication has been
   enabled, verify redirection to the identity provider.
3. Upload a representative workbook and verify charts, tooltips, date filters,
   PDF export, multiple-workbook chaining, and workbook clearing.
4. Confirm that selecting Data Warehouse is the only action that reads the
   configured warehouse files.
5. Review Windows Event Viewer under **Windows Logs > Application** for startup
   or ASP.NET Core Module errors.

## Updating and rollback

Deploy each release to a new versioned directory. Stop the application pool,
change the IIS physical path to the new directory, and restart the pool.
Rollback consists of pointing IIS back to the prior release directory.

Do not overwrite a live deployment in place.
