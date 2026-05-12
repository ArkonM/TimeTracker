# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build
dotnet build src/TimeTracker.csproj

# Run
dotnet run --project src/TimeTracker.csproj

# Build release
dotnet publish src/TimeTracker.csproj -c Release
```

There are no automated tests in this project.

## Data Storage

Data is split across per-month entry files and a single meta file, all under `%APPDATA%\TimeTracker\`:

| File | Contents |
|---|---|
| `data.json` | `AppSettings` + `ActiveSession` only (no entries) |
| `MM_Entries.json` | `List<TimeEntry>` for that calendar month, e.g. `05_Entries.json` for May |

Every mutating operation writes only the affected file immediately. Missing or corrupt files are silently treated as empty — no migration logic exists.

A test data file for May 2026 is provided at `test-data-may-2026.json` in the repo root. To use it, rename/copy it to `%APPDATA%\TimeTracker\05_Entries.json`.

## Architecture

**Pattern:** WPF + MVVM using `CommunityToolkit.Mvvm`. `[ObservableProperty]` and `[RelayCommand]` attributes generate boilerplate at compile time via source generators.

**Layer responsibilities:**

| Layer | Key types | Responsibility |
|---|---|---|
| `Models/` | `TimeEntry`, `AppData`, `AppSettings`, `ActiveSession` | Plain data; `WorkDuration` is a computed `[JsonIgnore]` property on `TimeEntry` |
| `Data/` | `JsonDataStore` | Raw read/write of `data.json`; owns the `JsonSerializerOptions` with custom converters |
| `Services/` | `TimeEntryService`, `ExportService`, `HolidayService` | All business logic; `TimeEntryService` holds the in-memory `AppData` and calls `JsonDataStore.Save` after every mutation |
| `ViewModels/` | `MainViewModel`, `DayRowViewModel`, dialog VMs | UI state; `MainViewModel` owns the `DispatcherTimer` for live tracking |
| `Views/` | XAML + code-behind | Minimal code-behind — only dialog wiring (show dialog, read result) |

**Key flows:**

- **Start timer:** `MainViewModel.Start()` → `TimeEntryService.StartSession()` persists an `ActiveSession` → `DispatcherTimer` fires every second to update `ElapsedDisplay` and the today row via `DayRowViewModel.SetLiveTracking`.
- **Stop timer:** timer paused → `BreakDialog` shown → `TimeEntryService.AddEntry()` + `ClearSession()` → `LoadMonth()` rebuilds the full `DayRows` collection.
- **Midnight auto-stop:** `OnTick` detects `DateTime.Now.Date > _sessionStart.Date` → calls `AutoStop()`, which creates an entry with `EndTime = 23:59 + DefaultBreakMinutes` and `AutoStopped = true`.
- **App resume with active session:** `ResumeSession()` in constructor restores the timer if `ActiveSession` exists; if it started on a prior day, `AutoStop` is called immediately.
- **Monthly view:** `LoadMonth()` builds a `DayRows` range that extends to full weeks (Monday–Sunday), so days from adjacent months appear greyed out (`IsOutOfMonth`). Only in-month days get entries from `TimeEntryService`.
- **Export:** `ExportDialog` lets the user pick months and an export mode (`Actual` or `Corrected`). `ExportService.Export(months, filePath, mode)` creates one XLSX sheet per month via ClosedXML.
  - **Actual mode** (default): recorded start/end/break/work times, overtime column colour-coded red/orange/green against `DailyTarget`.
  - **Corrected mode:** every Mon–Fri non-holiday gets a normalised entry — start = earliest recorded `StartTime` for that day (or `08:00` if nothing was tracked), end = start + `DailyTarget` + 30 min break, work = `DailyTarget`, overtime = `0:00`. Weekends and holidays are left blank.

## Settings

`AppSettings` is stored inside `data.json` alongside entries. Key derived values:

- `DailyTarget = WeeklyHours / 5` (default 38.5 h/week → **7h 42min/day**)
- `OvertimeThreshold` = separate threshold for "actual overtime" colour (default **8h**)

## Holidays

`HolidayService` computes Austrian public holidays in-process using the Anonymous Gregorian Easter algorithm. No network calls, no third-party package — 13 fixed/Easter-relative dates per year, cached by year in a `Dictionary<int, List<HolidayEntry>>`.

## JSON Serialisation Notes

`TimeSpan` → `"hh:mm:ss"` string, `DateOnly` → `"yyyy-MM-dd"` string (custom converters in `Helpers/JsonConverters.cs`). Standard `System.Text.Json` cannot handle these types natively — always pass the shared `JsonSerializerOptions` instance from `JsonDataStore` if serialising manually.
