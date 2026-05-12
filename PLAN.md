# TimeTracker — App Planning

## Goal

A Windows desktop application to track personal working time, with a monthly list view, system tray presence, and Excel export.

---

## Tech Stack

| Layer | Choice | Reason |
|---|---|---|
| Framework | WPF (.NET 8) | Native Windows, mature ecosystem, excellent MVVM support |
| Language | C# | First-class .NET citizen, strong typing |
| Storage | JSON flat file (System.Text.Json) | No DB, human-readable, single file in AppData |
| Excel Export | ClosedXML | Clean API for generating .xlsx files without Excel installed |
| UI Pattern | MVVM (CommunityToolkit.Mvvm) | Clean separation, testable ViewModels |
| Styling | ModernWpf or HandyControl | Modern Windows 11-style UI components |
| Holidays | Nager.Date | Austrian public holiday data, no internet required |

---

## Core Features

### 1. Time Tracking
- **Start button**: records current timestamp as `StartTime`, begins a live running timer display
- **End button**: opens the Break Dialog, then saves the finalized entry
- Manual entry: add or edit a past entry (date, start time, end time, break duration)
- Delete existing entries
- Multiple entries per day are theoretically possible — UI treatment to be decided later

### 1a. Break Dialog (shown on End)

When the user clicks End, a modal dialog asks:

> "Did you already take your break?"
>
> Break duration: [ 0:30 ] ← editable, defaults to 30 min
>
> [ Yes, break is done ]  [ No, add break to end ]

**Case A — Break already taken ("Yes")**
- `StartTime` and `EndTime` saved as-is (e.g. 10:00 – 17:00)
- `WorkDuration` = `EndTime − StartTime − BreakDuration` (e.g. 6h 30m)

**Case B — Break not yet taken ("No")**
- `EndTime` is pushed forward by `BreakDuration` (e.g. 17:00 → 17:30)
- `WorkDuration` = same formula, same result (e.g. 7h 00m), since EndTime was shifted

**The formula is always the same:**

`WorkDuration = EndTime − StartTime − BreakDuration`

| Field | Case A | Case B |
|---|---|---|
| StartTime | 10:00 | 10:00 |
| EndTime | 17:00 | 17:30 |
| BreakDuration | 00:30 | 00:30 |
| WorkDuration | 6h 30m | 7h 00m |

No extra flag stored — `EndTime` already encodes the break situation.

### 1b. Timer — Edge Cases
- If the app is **closed while the timer is running**, the start time is persisted and the timer resumes on next launch
- If the timer **runs past midnight**, it is automatically stopped and saved at 23:59 with the break dialog skipped (entry flagged as auto-stopped)

### 2. Monthly View
- Rows per day (not a calendar grid)
- Each row shows: date, weekday, start time, end time, work duration, time remaining toward daily target
- **Weekends** (Sat/Sun) are visually grayed out
- **Austrian public holidays** are highlighted in a distinct color (via Nager.Date)
- **Week boundaries** are visually separated (e.g. a subtle horizontal divider between weeks)
- Navigate between months with < / > arrows
- Current day is highlighted

### 2a. Daily Target & Time Remaining
- Configurable daily target (default: 8h 00m) stored in settings
- Each day row shows: `Remaining = DailyTarget − WorkDuration` (negative = overtime, shown in a different color)
- Monthly total row at the bottom: total worked vs. total expected (target × working days)

### 3. System Tray
- App launches to the **system tray** — no taskbar button while running
- Tray icon shows a tooltip with today's elapsed time
- Double-click tray icon (or right-click → Open) to bring up the main window
- Right-click menu: Open, Start/Stop, Exit

### 4. Excel Export
- Export by **full month** — no day-level granularity
- Option to select **multiple months** at once in the export dialog
- One sheet per month; each row: Date, Weekday, Start, End, Break, WorkDuration, Remaining
- Summary row per month: totals
- Holidays and weekends visually marked in the sheet (matching the app's color coding)
- Output as `.xlsx` via save dialog

---

## Data Model

```
TimeEntry
  Id              Guid
  Date            DateOnly
  StartTime       TimeSpan        # clock-in time, never modified
  EndTime         TimeSpan        # clock-out (shifted by BreakDuration if break not yet taken)
  BreakDuration   TimeSpan        # user-entered in dialog, default 00:30
  WorkDuration    TimeSpan        # always: EndTime − StartTime − BreakDuration
  AutoStopped     bool            # true if timer was cut off at midnight
  CreatedAt       DateTime

AppSettings
  DailyTargetHours   TimeSpan    # default 08:00
  DefaultBreakMinutes int        # default 30
```

No `Project` model — tagging is out of scope.

All entries serialized to a single JSON file:
`%APPDATA%\TimeTracker\data.json`

---

## Project Structure

```
TimeTracker/
├── TimeTracker.sln
├── src/
│   └── TimeTracker/
│       ├── App.xaml
│       ├── Models/          # TimeEntry, AppSettings
│       ├── ViewModels/      # MainViewModel, MonthViewModel, EntryDialogViewModel
│       ├── Views/           # MainWindow, MonthView, BreakDialog, EntryDialog
│       ├── Services/        # TimeEntryService, ExportService, HolidayService
│       ├── Data/            # JsonDataStore (read/write data.json)
│       └── Helpers/         # DurationFormatter, DateHelpers
```

---

## Key NuGet Packages

```
CommunityToolkit.Mvvm
ClosedXML
ModernWpfUI (or HandyControl)
Nager.Date
```

`System.Text.Json` is included in .NET 8 — no extra package needed for storage.

---

## Milestones

1. **M1 — Scaffolding**: Project setup, JSON storage, TimeEntry CRUD, AppSettings
2. **M2 — Monthly View**: Day rows, week separators, weekend/holiday coloring, month navigation, time remaining
3. **M3 — Timer + Tray**: Start/Stop, Break Dialog, midnight auto-stop, system tray with no taskbar button
4. **M4 — Excel Export**: Multi-month export dialog, per-month sheets, summary row, color coding
5. **M5 — Polish**: Validation, error handling, manual entry/edit UI, modern styling
