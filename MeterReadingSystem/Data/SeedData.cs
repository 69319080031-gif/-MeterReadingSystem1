using MeterReadingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace MeterReadingSystem.Data;

/// <summary>
/// Inserts sample meters and 12 months of readings when the database is empty
/// (controlled by "SeedSampleData" in appsettings.json). Data is real rows in SQLite.
/// </summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(ApplicationDbContext db, DateTime today)
    {
        if (await db.Meters.AnyAsync()) return;

        var now = DateTime.Now;
        var meters = new (string Code, MeterType Type, string Location, MeterStatus Status, decimal Start, int MinUse, int MaxUse)[]
        {
            ("W-001", MeterType.Water, "อาคารสำนักงาน ชั้น 1", MeterStatus.Active, 1000, 110, 160),
            ("W-002", MeterType.Water, "อาคารเรียน A", MeterStatus.Active, 2500, 180, 260),
            ("W-003", MeterType.Water, "โรงอาหารกลาง", MeterStatus.Active, 820, 90, 140),
            ("E-001", MeterType.Electricity, "อาคารสำนักงาน (ตู้ MDB หลัก)", MeterStatus.Active, 15200, 1800, 2600),
            ("E-002", MeterType.Electricity, "อาคารเรียน A", MeterStatus.Active, 30450, 2400, 3400),
            ("E-003", MeterType.Electricity, "หอพักนักศึกษา", MeterStatus.Maintenance, 8800, 900, 1400),
        };

        var rnd = new Random(2026);
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-12);

        foreach (var m in meters)
        {
            var meter = new Meter
            {
                MeterCode = m.Code,
                MeterType = m.Type,
                Unit = MeterLabels.UnitOf(m.Type),
                Location = m.Location,
                InstallationDate = firstMonth.AddMonths(-6),
                Status = m.Status,
                Notes = "ข้อมูลตัวอย่างสำหรับทดลองใช้งาน",
                CreatedAt = now,
                UpdatedAt = now
            };

            decimal value = m.Start;
            decimal? previous = null;
            for (var d = firstMonth; d <= today; d = d.AddMonths(1))
            {
                if (previous.HasValue)
                {
                    // Seasonal swing: more usage in the hot months (Mar–May).
                    var season = d.Month is >= 3 and <= 5 ? 1.2m : 1.0m;
                    value += Math.Round(rnd.Next(m.MinUse, m.MaxUse) * season, 1);
                }
                meter.Readings.Add(new MeterReading
                {
                    ReadingDate = d,
                    CumulativeReading = value,
                    Consumption = previous.HasValue ? value - previous.Value : null,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                previous = value;
            }
            db.Meters.Add(meter);
        }

        await db.SaveChangesAsync();
    }
}
