using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MeterReadingSystem.ViewModels;

namespace MeterReadingSystem.Controllers;

[AllowAnonymous]
public class AccountController(IConfiguration config) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(SafeReturn(returnUrl));
        ViewBag.ReturnUrl = returnUrl;
        ShowDemo();
        return View(new LoginViewModel());
    }

    // Shows the admin credentials on the login page only when ShowDemoCredentials=true (for demo/grading).
    private void ShowDemo()
    {
        if (config.GetValue("ShowDemoCredentials", false))
        {
            ViewBag.DemoUser = config["Admin:Username"];
            ViewBag.DemoPass = config["Admin:Password"];
        }
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        ShowDemo();
        if (!ModelState.IsValid) return View(vm);

        var user = config["Admin:Username"] ?? "";
        var pass = config["Admin:Password"] ?? "";
        // Evaluate both so timing doesn't reveal which one was wrong.
        var userOk = Same(vm.Username, user);
        var passOk = Same(vm.Password, pass);
        if (user.Length == 0 || pass.Length == 0 || !(userOk & passOk))
        {
            await Task.Delay(500); // slow down password guessing
            ModelState.AddModelError("", "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง");
            vm.Password = "";
            return View(vm);
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, user), new Claim(ClaimTypes.Role, "Admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return LocalRedirect(SafeReturn(returnUrl));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private string SafeReturn(string? url) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : "/";

    private static bool Same(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
