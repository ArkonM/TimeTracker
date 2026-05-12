using ClosedXML.Excel;
using TimeTracker.Helpers;
using TimeTracker.Models;

namespace TimeTracker.Services;

public enum ExportMode { Actual, Corrected }

public class ExportService
{
    private readonly TimeEntryService _entries;
    private readonly HolidayService _holidays;

    public ExportService(TimeEntryService entries, HolidayService holidays)
    {
        _entries = entries;
        _holidays = holidays;
    }

    public void Export(IEnumerable<(int Year, int Month)> months, string filePath, ExportMode mode = ExportMode.Actual)
    {
        using var wb = new XLWorkbook();
        foreach (var (y, m) in months)
        {
            if (mode == ExportMode.Corrected)
                WriteSheetCorrected(wb, y, m);
            else
                WriteSheet(wb, y, m);
        }
        wb.SaveAs(filePath);
    }

    private void WriteSheet(XLWorkbook wb, int year, int month)
    {
        var title = new DateTime(year, month, 1).ToString("MMMM yyyy");
        var ws = wb.Worksheets.Add(title);
        var settings = _entries.Settings;

        string[] headers = { "Date", "Day", "Start", "End", "Break", "Work", "Overtime" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var entryMap = _entries.GetEntriesForMonth(year, month).ToDictionary(e => e.Date);
        int days = DateTime.DaysInMonth(year, month);

        for (int d = 1; d <= days; d++)
        {
            var date = new DateOnly(year, month, d);
            var dt = date.ToDateTime(TimeOnly.MinValue);
            bool isWeekend = dt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            bool isHoliday = _holidays.IsHoliday(date);
            string? holidayName = _holidays.GetHolidayName(date);
            entryMap.TryGetValue(date, out var entry);

            int row = d + 1;
            ws.Cell(row, 1).Value = date.ToString("dd.MM.yyyy");
            ws.Cell(row, 2).Value = dt.ToString("ddd") + (holidayName != null ? $" ({holidayName})" : "");
            if (entry != null && entry.EntryType == EntryType.Vacation)
            {
                ws.Cell(row, 3).Value = "Vacation";
            }
            else if (entry != null && entry.EntryType == EntryType.Sick)
            {
                ws.Cell(row, 3).Value = "Sick Day";
            }
            else if (entry != null)
            {
                ws.Cell(row, 3).Value = DurationFormatter.FormatTime(entry.StartTime);
                ws.Cell(row, 4).Value = DurationFormatter.FormatTime(entry.EndTime);
                ws.Cell(row, 5).Value = DurationFormatter.Format(entry.BreakDuration);
                ws.Cell(row, 6).Value = DurationFormatter.Format(entry.WorkDuration);
                var diff = entry.WorkDuration - settings.DailyTarget;
                var cell = ws.Cell(row, 7);
                cell.Value = diff == TimeSpan.Zero ? "0:00"
                    : diff > TimeSpan.Zero ? "+" + DurationFormatter.Format(diff)
                    : "-" + DurationFormatter.Format(diff.Duration());
                cell.Style.Font.FontColor = diff < TimeSpan.Zero ? XLColor.Red
                    : entry.WorkDuration >= settings.OvertimeThreshold ? XLColor.DarkGreen
                    : XLColor.FromHtml("#E65100");
            }

            var bg = isHoliday ? XLColor.FromHtml("#FFF3CD")
                   : isWeekend ? XLColor.FromHtml("#F0F0F0")
                   : XLColor.NoColor;

            if (bg != XLColor.NoColor)
                for (int c = 1; c <= 7; c++)
                    ws.Cell(row, c).Style.Fill.BackgroundColor = bg;
        }

        // Summary
        var allEntries = _entries.GetEntriesForMonth(year, month);
        var absenceDays = allEntries.Count(e => e.EntryType != EntryType.Work);
        var totalWork = allEntries.Where(e => e.EntryType == EntryType.Work)
            .Aggregate(TimeSpan.Zero, (s, e) => s + e.WorkDuration);
        int workdays = Enumerable.Range(1, days)
            .Select(d => new DateOnly(year, month, d))
            .Count(d => d.ToDateTime(TimeOnly.MinValue).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                        && !_holidays.IsHoliday(d));
        var totalExpected = settings.DailyTarget * (workdays - absenceDays);

        int summaryRow = days + 2;
        ws.Cell(summaryRow, 1).Value = "Total";
        ws.Cell(summaryRow, 1).Style.Font.Bold = true;
        ws.Cell(summaryRow, 5).Value = $"{workdays} workdays";
        ws.Cell(summaryRow, 6).Value = DurationFormatter.Format(totalWork);
        ws.Cell(summaryRow, 6).Style.Font.Bold = true;
        var totalBalance = totalWork - totalExpected;
        var totalCell = ws.Cell(summaryRow, 7);
        totalCell.Value = totalBalance == TimeSpan.Zero ? "0:00"
            : totalBalance > TimeSpan.Zero ? "+" + DurationFormatter.Format(totalBalance)
            : "-" + DurationFormatter.Format(totalBalance.Duration());
        totalCell.Style.Font.Bold = true;
        totalCell.Style.Font.FontColor = totalBalance < TimeSpan.Zero ? XLColor.Red : XLColor.DarkGreen;

        ws.Columns().AdjustToContents();
    }

    private void WriteSheetCorrected(XLWorkbook wb, int year, int month)
    {
        var title = new DateTime(year, month, 1).ToString("MMMM yyyy");
        var ws = wb.Worksheets.Add(title);
        var settings = _entries.Settings;
        var breakDuration = TimeSpan.FromMinutes(30);

        string[] headers = { "Date", "Day", "Start", "End", "Break", "Work", "Overtime" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var entryMap = _entries.GetEntriesForMonth(year, month)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.MinBy(e => e.StartTime)!);
        int days = DateTime.DaysInMonth(year, month);
        int workdays = 0;

        for (int d = 1; d <= days; d++)
        {
            var date = new DateOnly(year, month, d);
            var dt = date.ToDateTime(TimeOnly.MinValue);
            bool isWeekend = dt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            bool isHoliday = _holidays.IsHoliday(date);
            string? holidayName = _holidays.GetHolidayName(date);
            entryMap.TryGetValue(date, out var entry);

            int row = d + 1;
            ws.Cell(row, 1).Value = date.ToString("dd.MM.yyyy");
            ws.Cell(row, 2).Value = dt.ToString("ddd") + (holidayName != null ? $" ({holidayName})" : "");

            if (!isWeekend && !isHoliday)
            {
                if (entry?.EntryType == EntryType.Vacation)
                {
                    ws.Cell(row, 3).Value = "Vacation";
                }
                else if (entry?.EntryType == EntryType.Sick)
                {
                    ws.Cell(row, 3).Value = "Sick Day";
                }
                else
                {
                    workdays++;
                    var start = entry?.StartTime ?? new TimeSpan(8, 0, 0);
                    var end = start + settings.DailyTarget + breakDuration;
                    ws.Cell(row, 3).Value = DurationFormatter.FormatTime(start);
                    ws.Cell(row, 4).Value = DurationFormatter.FormatTime(end);
                    ws.Cell(row, 5).Value = DurationFormatter.Format(breakDuration);
                    ws.Cell(row, 6).Value = DurationFormatter.Format(settings.DailyTarget);
                    ws.Cell(row, 7).Value = "0:00";
                    ws.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml("#E65100");
                }
            }

            var bg = isHoliday ? XLColor.FromHtml("#FFF3CD")
                   : isWeekend ? XLColor.FromHtml("#F0F0F0")
                   : XLColor.NoColor;
            if (bg != XLColor.NoColor)
                for (int c = 1; c <= 7; c++)
                    ws.Cell(row, c).Style.Fill.BackgroundColor = bg;
        }

        var totalWork = settings.DailyTarget * workdays;
        int summaryRow = days + 2;
        ws.Cell(summaryRow, 1).Value = "Total";
        ws.Cell(summaryRow, 1).Style.Font.Bold = true;
        ws.Cell(summaryRow, 5).Value = $"{workdays} workdays";
        ws.Cell(summaryRow, 6).Value = DurationFormatter.Format(totalWork);
        ws.Cell(summaryRow, 6).Style.Font.Bold = true;
        ws.Cell(summaryRow, 7).Value = "0:00";
        ws.Cell(summaryRow, 7).Style.Font.Bold = true;
        ws.Cell(summaryRow, 7).Style.Font.FontColor = XLColor.DarkGreen;

        ws.Columns().AdjustToContents();
    }
}
