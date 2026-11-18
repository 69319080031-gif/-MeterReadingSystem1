using MeterReadingSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeterReadingSystem.Controllers;

public class DashboardController(ReportService reports) : Controller
{
    public async Task<IActionResult> Index() => View(await reports.GetDashboardAsync());
}
