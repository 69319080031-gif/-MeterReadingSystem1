using MeterReadingSystem.Data;
using MeterReadingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace MeterReadingSystem.Services;

public record MeterInput(string MeterCode, MeterType MeterType, string Location, DateTime InstallationDate,
    MeterStatus Status, string? Notes);

public record MeterListItem(Meter Meter, int ReadingCount, MeterReading? LatestReading);

public class MeterService(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<List<MeterListItem>> ListAsync(string? search = null, MeterType? type = null, MeterStatus? status = null)
    {
        var q = db.Meters.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(m => m.MeterCode.ToLower().Contains(s) || m.Location.ToLower().Contains(s));
        }
        if (type.HasValue) q = q.Where(m => m.MeterType == type.Value);
        if (status.HasValue) q = q.Where(m => m.Status == status.Value);

        return await q.OrderBy(m => m.MeterCode)
            .Select(m => new MeterListItem(
                m,
                m.Readings.Count,
                m.Readings.OrderByDescending(r => r.ReadingDate).FirstOrDefault()))
            .ToListAsync();
    }

    /// <summary>Meters for dropdowns. activeOnly excludes "Inactive" meters (they cannot take new readings).</summary>
    public Task<List<Meter>> ListForSelectAsync(bool activeOnly) =>
        db.Meters.AsNoTracking()
            .Where(m => !activeOnly || m.Status != MeterStatus.Inactive)
            .OrderBy(m => m.MeterType).ThenBy(m => m.MeterCode)
            .ToListAsync();

    public Task<Meter?> GetAsync(int id) => db.Meters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);

    public Task<int> CountReadingsAsync(int meterId) => db.MeterReadings.CountAsync(r => r.MeterId == meterId);

    public async Task<ServiceResult<Meter>> CreateAsync(MeterInput input)
    {
        var code = input.MeterCode.Trim();
        if (await CodeExistsAsync(code, null))
            return ServiceResult<Meter>.Fail($"รหัสมิเตอร์ \"{code}\" มีอยู่ในระบบแล้ว กรุณาใช้รหัสอื่น");

        var now = clock.GetLocalNow().DateTime;
        var meter = new Meter { CreatedAt = now };
        Apply(meter, input, now);
        db.Meters.Add(meter);
        await db.SaveChangesAsync();
        return ServiceResult<Meter>.Ok(meter);
    }

    public async Task<ServiceResult<Meter>> UpdateAsync(int id, MeterInput input)
    {
        var meter = await db.Meters.FirstOrDefaultAsync(m => m.Id == id);
        if (meter is null) return ServiceResult<Meter>.Fail("ไม่พบมิเตอร์ที่ต้องการแก้ไข");

        var code = input.MeterCode.Trim();
        if (await CodeExistsAsync(code, id))
            return ServiceResult<Meter>.Fail($"รหัสมิเตอร์ \"{code}\" มีอยู่ในระบบแล้ว กรุณาใช้รหัสอื่น");

        var readings = db.MeterReadings.Where(r => r.MeterId == id);
        if (input.MeterType != meter.MeterType && await readings.AnyAsync())
            return ServiceResult<Meter>.Fail("ไม่สามารถเปลี่ยนประเภทมิเตอร์ได้ เนื่องจากมีการบันทึกค่าอ่านแล้ว (หน่วยวัดจะไม่ตรงกัน)");

        var firstReading = await readings.OrderBy(r => r.ReadingDate).Select(r => (DateTime?)r.ReadingDate).FirstOrDefaultAsync();
        if (firstReading.HasValue && input.InstallationDate.Date > firstReading.Value)
            return ServiceResult<Meter>.Fail(
                $"วันที่ติดตั้งต้องไม่หลังวันที่อ่านมิเตอร์ครั้งแรก ({Fmt.Date(firstReading.Value)})");

        Apply(meter, input, clock.GetLocalNow().DateTime);
        await db.SaveChangesAsync();
        return ServiceResult<Meter>.Ok(meter);
    }

    /// <summary>Deletes a meter together with all of its readings (cascade) in one transaction.</summary>
    public async Task<ServiceResult> DeleteAsync(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var meter = await db.Meters.FirstOrDefaultAsync(m => m.Id == id);
        if (meter is null) return ServiceResult.Fail("ไม่พบมิเตอร์ที่ต้องการลบ");
        db.Meters.Remove(meter);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult.Ok();
    }

    private Task<bool> CodeExistsAsync(string code, int? excludeId)
    {
        var lower = code.ToLower();
        return db.Meters.AnyAsync(m => m.MeterCode.ToLower() == lower && m.Id != (excludeId ?? 0));
    }

    private static void Apply(Meter meter, MeterInput input, DateTime now)
    {
        meter.MeterCode = input.MeterCode.Trim();
        meter.MeterType = input.MeterType;
        meter.Unit = MeterLabels.UnitOf(input.MeterType);
        meter.Location = input.Location.Trim();
        meter.InstallationDate = input.InstallationDate.Date;
        meter.Status = input.Status;
        meter.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        meter.UpdatedAt = now;
    }
}
