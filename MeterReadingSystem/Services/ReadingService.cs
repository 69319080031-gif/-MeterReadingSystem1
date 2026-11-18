using MeterReadingSystem.Data;
using MeterReadingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace MeterReadingSystem.Services;

public record ReadingInput(int MeterId, DateTime ReadingDate, decimal CumulativeReading, string? Notes);

public class ReadingFilter
{
    public string? Search { get; set; }
    public MeterType? Type { get; set; }
    public int? MeterId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>"asc" or "desc" (default) by ReadingDate.</summary>
    public string Sort { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Neighbouring readings around a date, used by the record form.</summary>
public record ReadingNeighbours(MeterReading? Previous, MeterReading? Next);

/// <summary>
/// All business rules for meter readings: validation against neighbours (by ReadingDate),
/// duplicate-day protection, and consumption recalculation. Every write runs in a transaction.
/// </summary>
public class ReadingService(ApplicationDbContext db, TimeProvider clock)
{
    public Task<MeterReading?> GetAsync(int id) =>
        db.MeterReadings.Include(r => r.Meter).AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<ReadingNeighbours> GetNeighboursAsync(int meterId, DateTime date, int? excludeId = null)
    {
        date = date.Date;
        var q = db.MeterReadings.AsNoTracking().Where(r => r.MeterId == meterId && r.Id != (excludeId ?? 0));
        var prev = await q.Where(r => r.ReadingDate < date).OrderByDescending(r => r.ReadingDate).FirstOrDefaultAsync();
        var next = await q.Where(r => r.ReadingDate > date).OrderBy(r => r.ReadingDate).FirstOrDefaultAsync();
        return new ReadingNeighbours(prev, next);
    }

    public Task<ServiceResult<MeterReading>> CreateAsync(ReadingInput input) => SaveAsync(null, input);

    public Task<ServiceResult<MeterReading>> UpdateAsync(int id, ReadingInput input) => SaveAsync(id, input);

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var reading = await db.MeterReadings.FirstOrDefaultAsync(r => r.Id == id);
        if (reading is null) return ServiceResult.Fail("ไม่พบรายการค่าอ่านที่ต้องการลบ");

        db.MeterReadings.Remove(reading);
        await db.SaveChangesAsync();
        await RecalculateAsync(reading.MeterId);
        await tx.CommitAsync();
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult<MeterReading>> SaveAsync(int? id, ReadingInput input)
    {
        var date = input.ReadingDate.Date;
        var value = input.CumulativeReading;

        await using var tx = await db.Database.BeginTransactionAsync();

        var meter = await db.Meters.FirstOrDefaultAsync(m => m.Id == input.MeterId);
        if (meter is null) return ServiceResult<MeterReading>.Fail("ไม่พบมิเตอร์ที่เลือก");

        MeterReading? entity = null;
        if (id.HasValue)
        {
            entity = await db.MeterReadings.FirstOrDefaultAsync(r => r.Id == id.Value);
            if (entity is null) return ServiceResult<MeterReading>.Fail("ไม่พบรายการค่าอ่านที่ต้องการแก้ไข");
            if (entity.MeterId != meter.Id)
                return ServiceResult<MeterReading>.Fail("ไม่สามารถเปลี่ยนมิเตอร์ของรายการค่าอ่านที่บันทึกแล้วได้");
        }
        else if (meter.Status == MeterStatus.Inactive)
        {
            return ServiceResult<MeterReading>.Fail(
                $"มิเตอร์ {meter.MeterCode} อยู่ในสถานะ \"ยกเลิกใช้งาน\" ไม่สามารถบันทึกค่าอ่านใหม่ได้");
        }

        // Other readings of this meter, ordered by ReadingDate (never CreatedAt).
        var others = await db.MeterReadings
            .Where(r => r.MeterId == meter.Id && r.Id != (id ?? 0))
            .OrderBy(r => r.ReadingDate)
            .ToListAsync();

        var error = Validate(meter, others, date, value, clock.GetLocalNow().Date);
        if (error is not null) return ServiceResult<MeterReading>.Fail(error);

        var now = clock.GetLocalNow().DateTime;
        if (entity is null)
        {
            entity = new MeterReading { MeterId = meter.Id, CreatedAt = now };
            db.MeterReadings.Add(entity);
        }
        entity.ReadingDate = date;
        entity.CumulativeReading = value;
        entity.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        entity.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index (MeterId, ReadingDate) caught a concurrent duplicate.
            return ServiceResult<MeterReading>.Fail(DuplicateMessage(meter, date));
        }

        await RecalculateAsync(meter.Id);
        await tx.CommitAsync();
        return ServiceResult<MeterReading>.Ok(entity);
    }

    /// <summary>Returns a Thai error message, or null when the reading is valid.</summary>
    internal static string? Validate(Meter meter, List<MeterReading> othersOrdered, DateTime date, decimal value, DateTime today)
    {
        if (value < 0) return "ค่าอ่านสะสมต้องไม่ติดลบ";
        if (date > today) return "วันที่อ่านมิเตอร์ต้องไม่เป็นวันในอนาคต";
        if (date < meter.InstallationDate.Date)
            return $"วันที่อ่านมิเตอร์ต้องไม่ก่อนวันที่ติดตั้งมิเตอร์ ({Fmt.Date(meter.InstallationDate)})";

        if (othersOrdered.Any(r => r.ReadingDate.Date == date)) return DuplicateMessage(meter, date);

        var prev = othersOrdered.LastOrDefault(r => r.ReadingDate < date);
        var next = othersOrdered.FirstOrDefault(r => r.ReadingDate > date);

        if (prev is not null && value < prev.CumulativeReading)
            return $"ค่าอ่านสะสม {Fmt.Num(value)} {meter.Unit} ต่ำกว่าค่าอ่านครั้งก่อนหน้า " +
                   $"({Fmt.Num(prev.CumulativeReading)} {meter.Unit} เมื่อวันที่ {Fmt.Date(prev.ReadingDate)}) " +
                   "ค่าอ่านมิเตอร์ต้องไม่ลดลงตามลำดับวันที่";

        if (next is not null && value > next.CumulativeReading)
            return $"ค่าอ่านสะสม {Fmt.Num(value)} {meter.Unit} สูงกว่าค่าอ่านครั้งถัดไป " +
                   $"({Fmt.Num(next.CumulativeReading)} {meter.Unit} เมื่อวันที่ {Fmt.Date(next.ReadingDate)}) " +
                   "กรุณาตรวจสอบค่าอ่านหรือวันที่อีกครั้ง";

        return null;
    }

    private static string DuplicateMessage(Meter meter, DateTime date) =>
        $"มิเตอร์ {meter.MeterCode} มีการบันทึกค่าอ่านของวันที่ {Fmt.Date(date)} แล้ว ไม่สามารถบันทึกซ้ำในวันเดียวกันได้";

    /// <summary>Rebuilds Consumption for every reading of a meter. Caller owns the transaction.</summary>
    internal async Task RecalculateAsync(int meterId)
    {
        var readings = await db.MeterReadings
            .Where(r => r.MeterId == meterId)
            .OrderBy(r => r.ReadingDate)
            .ToListAsync();

        decimal? previous = null;
        foreach (var r in readings)
        {
            var consumption = previous.HasValue ? r.CumulativeReading - previous.Value : (decimal?)null;
            if (r.Consumption != consumption) r.Consumption = consumption;
            previous = r.CumulativeReading;
        }
        await db.SaveChangesAsync();
    }

    public async Task<PagedResult<MeterReading>> SearchAsync(ReadingFilter f)
    {
        var q = Filtered(f);
        var total = await q.CountAsync();
        var pageSize = Math.Clamp(f.PageSize, 5, 200);
        var page = Math.Max(1, f.Page);
        var items = await Sorted(q, f.Sort).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<MeterReading> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<List<MeterReading>> ListAllAsync(ReadingFilter f) => Sorted(Filtered(f), f.Sort).ToListAsync();

    private IQueryable<MeterReading> Filtered(ReadingFilter f)
    {
        var q = db.MeterReadings.Include(r => r.Meter).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            q = q.Where(r => r.Meter!.MeterCode.ToLower().Contains(s) || r.Meter!.Location.ToLower().Contains(s));
        }
        if (f.Type.HasValue) q = q.Where(r => r.Meter!.MeterType == f.Type.Value);
        if (f.MeterId.HasValue) q = q.Where(r => r.MeterId == f.MeterId.Value);
        if (f.From.HasValue) q = q.Where(r => r.ReadingDate >= f.From.Value.Date);
        if (f.To.HasValue) q = q.Where(r => r.ReadingDate <= f.To.Value.Date);
        return q;
    }

    private static IQueryable<MeterReading> Sorted(IQueryable<MeterReading> q, string sort) =>
        sort == "asc"
            ? q.OrderBy(r => r.ReadingDate).ThenBy(r => r.Meter!.MeterCode)
            : q.OrderByDescending(r => r.ReadingDate).ThenBy(r => r.Meter!.MeterCode);
}
