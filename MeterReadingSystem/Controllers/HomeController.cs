using System.Diagnostics;
using MeterReadingSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace MeterReadingSystem.Controllers;

[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [Route("Home/StatusCode/{code:int}")]
    public new IActionResult StatusCode(int code)
    {
        ViewBag.Code = code;
        return View("StatusCode");
    }
}
