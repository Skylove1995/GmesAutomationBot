# GMES Production Importer

C# .NET worker that opens GMES in Microsoft Edge IE mode, exports the `Prod. Analysis > Set` Excel report, parses production rows, and imports them into `mex_mes.tb_gmes_production`.

## Commands

```powershell
GmesImporter.exe validate-config
GmesImporter.exe dry-run
GmesImporter.exe dry-run --file "C:\Users\user\Downloads\Excel_Export_[0428_101327].xlsx"
GmesImporter.exe run-once
GmesImporter.exe protect-text "secret value"
```

`dry-run` executes the same import flow but rolls back database changes. `run-once --file <xlsx>` skips browser automation and imports a known Excel file.

## First-Time Setup

1. Publish a self-contained build:

```powershell
dotnet publish src\GmesImporter.App\GmesImporter.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

2. Either keep plaintext values in `appsettings.json` while testing, or create encrypted values on the same Windows account that will run the Scheduled Task:

```powershell
.\GmesImporter.exe protect-text "server=10.7.10.6;user=<least_privilege_user>;password=<password>;database=mex_mes;ConvertZeroDateTime=True"
.\GmesImporter.exe protect-text "LGEVH_HS"
.\GmesImporter.exe protect-text "<GMES password>"
```

3. Put either the plaintext values or returned `dpapi:...` strings into `appsettings.json`.

4. Confirm `IEDriverServer.exe` is beside `GmesImporter.exe` or update `Browser:IeDriverPath`.

5. Validate:

```powershell
.\GmesImporter.exe validate-config
```

6. Test with a known export file first:

```powershell
.\GmesImporter.exe dry-run --file "C:\Path\To\Excel_Export_sample.xlsx"
```

7. Test full GMES export on the internal machine:

```powershell
.\GmesImporter.exe dry-run
```

8. Install the scheduled task:

```powershell
.\scripts\install-scheduled-task.ps1 -ExePath "C:\Path\To\publish\GmesImporter.exe" -WorkingDirectory "C:\Path\To\publish" -IntervalMinutes 15
```

## Data Mapping

| Excel column | Database column |
| --- | --- |
| `W/O` | `WO` |
| `Model.Suffix` | `EBR` |
| `WIP S/N` | `PID` |
| `In Scan Time` | `aoi_input` |
| `Out Scan Time` | `aoi_output` |

The importer inserts new `PID` values, skips existing rows with both datetime fields populated, and fills only missing `aoi_input` or `aoi_output` values when GMES later provides them.

## Notes

- Edge IE mode must already be configured for `http://10.224.5.14/`.
- The Scheduled Task should run under a logged-in Windows user session so Edge IE mode can be automated.
- The GMES selectors are text-based and avoid screen coordinates. If the real DOM uses custom controls differently from the screenshots, adjust only `GmesBrowserAutomation`.
- Successful downloads move to `%LOCALAPPDATA%\GmesImporter\archive\yyyyMMdd`; failed imports move to `%LOCALAPPDATA%\GmesImporter\failed\yyyyMMdd`.
