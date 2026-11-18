using MeterReadingSystem.Services;
using MeterReadingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MeterReadingSystem.Controllers;

public class ReportsController(ReportService reports, MeterService meters) : Controller
{
    public async Task<IActionResult> Index([FromQuery] ReportFilter filter) =>
        View(new ReportViewModel
        {
            Report = await reports.GetReportAsync(filter),
            Meters = await meters.ListForSelectAsync(activeOnly: false)
        });

    public async Task<IActionResult> ExportCsv([FromQuery] ReportFilter filter)
    {
        var r = await reports.GetReportAsync(filter);
        return File(ExportService.ReportCsv(r), "text/csv; charset=utf-8", $"report-{FileStamp(r)}.csv");
    }

    public async Task<IActionResult> ExportExcel([FromQuery] ReportFilter filter)
    {
        var r = await reports.GetReportAsync(filter);
        return File(ExportService.ReportExcel(r),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"report-{FileStamp(r)}.xlsx");
    }

    private static string FileStamp(ReportResult r) =>
        $"{r.Filter.Period}{(r.Filter.Year is { } y && r.Filter.Period == "monthly" ? "-" + y : "")}-{DateTime.Now:yyyyMMdd-HHmm}";
}
