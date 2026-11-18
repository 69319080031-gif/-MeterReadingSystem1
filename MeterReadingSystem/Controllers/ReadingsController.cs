using MeterReadingSystem.Models;
using MeterReadingSystem.Services;
using MeterReadingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MeterReadingSystem.Controllers;

public class ReadingsController(ReadingService readings, MeterService meters) : Controller
{
    // ---------- History ----------
    public async Task<IActionResult> Index([FromQuery] ReadingFilter filter) =>
        View(new ReadingHistoryViewModel
        {
            Filter = filter,
            Result = await readings.SearchAsync(filter),
            Meters = await meters.ListForSelectAsync(activeOnly: false)
        });

    public async Task<IActionResult> Export([FromQuery] ReadingFilter filter)
    {
        var data = await readings.ListAllAsync(filter);
        return File(ExportService.ReadingsCsv(data), "text/csv; charset=utf-8", $"meter-readings-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    // ---------- Record ----------
    public async Task<IActionResult> Create(int? meterId) =>
        View("Form", new ReadingFormViewModel
        {
            MeterId = meterId,
            ReadingDate = DateTime.Today,
            Meters = await meters.ListForSelectAsync(activeOnly: true)
        });

    [HttpPost]
    public async Task<IActionResult> Create(ReadingFormViewModel vm, string? saveAndNew)
    {
        if (ModelState.IsValid)
        {
            var result = await readings.CreateAsync(ToInput(vm));
            if (result.Success)
            {
                var r = result.Value!;
                var meter = await meters.GetAsync(r.MeterId);
                TempData["Success"] = $"บันทึกค่าอ่านมิเตอร์ {meter?.MeterCode} วันที่ {Fmt.Date(r.ReadingDate)} เรียบร้อยแล้ว" +
                                      (r.Consumption.HasValue ? $" (ปริมาณการใช้ {Fmt.Num(r.Consumption)} {meter?.Unit})" : " (ค่าอ่านครั้งแรก)");
                return saveAndNew is not null ? RedirectToAction(nameof(Create)) : RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", result.Error!);
        }
        vm.Meters = await meters.ListForSelectAsync(activeOnly: true);
        return View("Form", vm);
    }

    public async Task<IActionResult> Edit(int id, string? returnUrl)
    {
        var r = await readings.GetAsync(id);
        if (r is null) return NotFound();
        ViewBag.ReturnUrl = SafeReturn(returnUrl);
        return View("Form", new ReadingFormViewModel
        {
            Id = r.Id,
            MeterId = r.MeterId,
            Meter = r.Meter,
            ReadingDate = r.ReadingDate,
            CumulativeReading = r.CumulativeReading,
            Notes = r.Notes
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ReadingFormViewModel vm, string? returnUrl)
    {
        var existing = await readings.GetAsync(id);
        if (existing is null) return NotFound();
        vm.Id = id;
        vm.MeterId = existing.MeterId; // the meter of a saved reading cannot change
        ModelState.Remove(nameof(vm.MeterId));

        if (ModelState.IsValid)
        {
            var result = await readings.UpdateAsync(id, ToInput(vm));
            if (result.Success)
            {
                TempData["Success"] = $"แก้ไขค่าอ่านมิเตอร์ {existing.Meter!.MeterCode} เรียบร้อยแล้ว และคำนวณปริมาณการใช้ใหม่แล้ว";
                return Redirect(SafeReturn(returnUrl));
            }
            ModelState.AddModelError("", result.Error!);
        }
        vm.Meter = existing.Meter;
        ViewBag.ReturnUrl = SafeReturn(returnUrl);
        return View("Form", vm);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var existing = await readings.GetAsync(id);
        var result = await readings.DeleteAsync(id);
        if (result.Success)
            TempData["Success"] = $"ลบค่าอ่านมิเตอร์ {existing?.Meter?.MeterCode} วันที่ {(existing is null ? "" : Fmt.Date(existing.ReadingDate))} เรียบร้อยแล้ว และคำนวณปริมาณการใช้ของรายการถัดไปใหม่แล้ว";
        else
            TempData["Error"] = result.Error;
        return Redirect(SafeReturn(returnUrl));
    }

    // ---------- AJAX: meter details + neighbouring readings for the record form ----------
    [HttpGet]
    public async Task<IActionResult> MeterInfo(int meterId, DateTime? date, int? excludeId)
    {
        var meter = await meters.GetAsync(meterId);
        if (meter is null) return NotFound(new { error = "ไม่พบมิเตอร์" });

        var n = await readings.GetNeighboursAsync(meterId, date ?? DateTime.Today, excludeId);
        object? Map(MeterReading? r) => r is null ? null : new
        {
            date = Fmt.Date(r.ReadingDate),
            value = r.CumulativeReading,
            display = Fmt.Num(r.CumulativeReading)
        };

        return Json(new
        {
            id = meter.Id,
            code = meter.MeterCode,
            type = meter.MeterType.ToString(),
            typeName = MeterLabels.TypeName(meter.MeterType),
            location = meter.Location,
            unit = meter.Unit,
            status = MeterLabels.StatusName(meter.Status),
            installationDate = Fmt.Iso(meter.InstallationDate),
            previous = Map(n.Previous),
            next = Map(n.Next)
        });
    }

    private static ReadingInput ToInput(ReadingFormViewModel vm) =>
        new(vm.MeterId!.Value, vm.ReadingDate!.Value, vm.CumulativeReading!.Value, vm.Notes);

    private string SafeReturn(string? url) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : Url.Action(nameof(Index))!;
}
