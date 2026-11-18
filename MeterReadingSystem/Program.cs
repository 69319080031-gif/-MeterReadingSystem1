using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.WebEncoders;
using MeterReadingSystem.Data;
using MeterReadingSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQLite file lives next to the app (resolved against the content root, not the shell's cwd).
var dbFile = builder.Configuration["Database:File"] ?? "App_Data/meterreading.db";
var dbPath = Path.Combine(builder.Environment.ContentRootPath, dbFile);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MeterService>();
builder.Services.AddScoped<ReadingService>();
builder.Services.AddScoped<ReportService>();

// Emit Thai text as-is in HTML (still HTML-escaped for <, >, &, quotes) instead of &#x..; entities.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddControllersWithViews(o =>
{
    // Every POST/PUT/DELETE must carry a valid anti-forgery token.
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Every page requires login unless marked [AllowAnonymous].
    o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

    // Thai model-binding messages (also emitted as client-side validation messages).
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustBeANumberAccessor(_ => "กรุณากรอกเป็นตัวเลข");
    m.SetAttemptedValueIsInvalidAccessor((v, _) => $"ค่า \"{v}\" ไม่ถูกต้อง");
    m.SetValueIsInvalidAccessor(v => $"ค่า \"{v}\" ไม่ถูกต้อง");
    m.SetValueMustNotBeNullAccessor(_ => "กรุณากรอกข้อมูลช่องนี้");
    m.SetMissingBindRequiredValueAccessor(_ => "กรุณากรอกข้อมูลช่องนี้");
    m.SetMissingKeyOrValueAccessor(() => "กรุณากรอกข้อมูลช่องนี้");
    m.SetUnknownValueIsInvalidAccessor(_ => "ค่าที่กรอกไม่ถูกต้อง");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(v => $"ค่า \"{v}\" ไม่ถูกต้อง");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "ค่าที่กรอกไม่ถูกต้อง");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "กรุณากรอกเป็นตัวเลข");
    m.SetMissingRequestBodyRequiredValueAccessor(() => "ไม่พบข้อมูลที่ส่งมา");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Login";
        o.Cookie.Name = "MeterPro.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Always when served over HTTPS
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration["Admin:Username"]) || string.IsNullOrWhiteSpace(app.Configuration["Admin:Password"]))
    app.Logger.LogWarning("Admin:Username / Admin:Password are not set — nobody can log in.");

// Apply EF Core migrations (creates the database on first run) and optionally seed sample data.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    if (app.Configuration.GetValue("SeedSampleData", true))
        await SeedData.EnsureSeededAsync(db, DateTime.Today);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");

// Gregorian calendar and "." decimal separator for parsing/formatting regardless of the Windows locale.
var culture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program;
