using MeterReadingSystem.Models;
using MeterReadingSystem.Services;
using MeterReadingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace MeterReadingSystem.Controllers;

public class MetersController(MeterService meters) : Controller
{
    public async Task<IActionResult> Index(string? search, MeterType? type, MeterStatus? status) =>
        View(new MeterIndexViewModel
        {
            Items = await meters.ListAsync(search, type, status),
            Search = search,
            Type = type,
            Status = status
        });

    public IActionResult Create() =>
        View("Form", new MeterFormViewModel { InstallationDate = DateTime.Today });

    [HttpPost]
    public async Task<IActionResult> Create(MeterFormViewModel vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);

        var result = await meters.CreateAsync(ToInput(vm));
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(vm.MeterCode), result.Error!);
            return View("Form", vm);
        }
        TempData["Success"] = $"เพิ่มมิเตอร์ {result.Value!.MeterCode} เรียบร้อยแล้ว";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await meters.GetAsync(id);
        if (m is null) return NotFound();
        return View("Form", new MeterFormViewModel
        {
            Id = m.Id,
            MeterCode = m.MeterCode,
            MeterType = m.MeterType,
            Location = m.Location,
            InstallationDate = m.InstallationDate,
            Status = m.Status,
            Notes = m.Notes,
            HasReadings = await meters.CountReadingsAsync(id) > 0
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, MeterFormViewModel vm)
    {
        vm.Id = id;
        vm.HasReadings = await meters.CountReadingsAsync(id) > 0;
        if (!ModelState.IsValid) return View("Form", vm);

        var result = await meters.UpdateAsync(id, ToInput(vm));
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);
            return View("Form", vm);
        }
        TempData["Success"] = $"บันทึกการแก้ไขมิเตอร์ {result.Value!.MeterCode} เรียบร้อยแล้ว";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var meter = await meters.GetAsync(id);
        var result = await meters.DeleteAsync(id);
        if (result.Success) TempData["Success"] = $"ลบมิเตอร์ {meter?.MeterCode} และค่าอ่านทั้งหมดของมิเตอร์นี้เรียบร้อยแล้ว";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    private static MeterInput ToInput(MeterFormViewModel vm) =>
        new(vm.MeterCode, vm.MeterType!.Value, vm.Location, vm.InstallationDate!.Value, vm.Status, vm.Notes);
}
