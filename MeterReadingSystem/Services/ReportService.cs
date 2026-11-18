using MeterReadingSystem.Data;
using MeterReadingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace MeterReadingSystem.Services;

public class DashboardData
{
    public int TotalMeters { get; init; }
    public int WaterMeters { get; init; }
    public int ElectricMeters { get; init; }
    public int ActiveMeters { get; init; }
    public int TotalReadings { get; init; }
    public LatestUsage? LatestWater { get; init; }
    public LatestUsage? LatestElectric { get; init; }
    public List<string> MonthLabels { get; init; } = [];
    public List<decimal> WaterSeries { get; init; } = [];
    public List<decimal> ElectricSeries { get; init; } = [];
    public List<MeterReading> RecentReadings { get; init; } = [];
}

/// <summary>Total consumption in the most recent month that has data, and the change from the month before.</summary>
public record LatestUsage(string MonthLabel, decimal Total, decimal? PreviousTotal)
{
    public decimal? ChangePercent => PreviousTotal is > 0 ? Math.Round((Total - PreviousTotal.Value) / PreviousTotal.Value * 100, 1) : null;
}

public class ReportFilter
{
    public MeterType? Type { get; set; }
    /// <summary>"monthly" (12 months of Year) or "yearly" (all years).</summary>
    public string Period { get; set; } = "monthly";
    public int? Year { get; set; }
    public int? MeterId { get; set; }
}

public record PeriodRow(string Label, decimal Water, decimal Electric, int ReadingCount);

public record MeterReportRow(Meter Meter, int ReadingCount, decimal Total, decimal? Average, decimal? Max, MeterReading? Latest);

public record PeriodMeterRow(string Period, Meter Meter, decimal Total, int ReadingCount);

public class ReportResult
{
    public ReportFilter Filter { get; init; } = new();
    public string Title { get; init; } = "";
    public List<int> AvailableYears { get; init; } = [];
    public List<PeriodRow> Periods { get; init; } = [];
    public List<MeterReportRow> Meters { get; init; } = [];
    public List<PeriodMeterRow> PeriodMeters { get; init; } = [];
    public List<MeterReading> Details { get; init; } = [];
    public decimal TotalWater => Periods.Sum(p => p.Water);
    public decimal TotalElectric => Periods.Sum(p => p.Electric);
    public int TotalReadings => Periods.Sum(p => p.ReadingCount);
    public bool ShowWater => Filter.Type != MeterType.Electricity;
    public bool ShowElectric => Filter.Type != MeterType.Water;
}

/// <summary>Read-only aggregations. Consumption is attributed to the month/year of its ReadingDate.</summary>
public class ReportService(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<DashboardData> GetDashboardAsync()
    {
        var meters = await db.Meters.AsNoTracking().Select(m => new { m.MeterType, m.Status }).ToListAsync();

        var today = clock.GetLocalNow().Date;
        var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

        // Aggregate in memory: SQLite stores decimal as TEXT and cannot SUM it reliably.
        var usage = await db.MeterReadings.AsNoTracking()
            .Where(r => r.Consumption != null)
            .Select(r => new { r.ReadingDate, r.Consumption, r.Meter!.MeterType })
            .ToListAsync();

        var byMonth = usage
            .GroupBy(u => (u.MeterType, Month: new DateTime(u.ReadingDate.Year, u.ReadingDate.Month, 1)))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Consumption!.Value));

        var labels = new List<string>();
        var water = new List<decimal>();
        var electric = new List<decimal>();
        for (var m = firstMonth; m <= today; m = m.AddMonths(1))
        {
            labels.Add(MonthLabel(m));
            water.Add(byMonth.GetValueOrDefault((MeterType.Water, m)));
            electric.Add(byMonth.GetValueOrDefault((MeterType.Electricity, m)));
        }

        LatestUsage? Latest(MeterType type)
        {
            var months = byMonth.Where(k => k.Key.MeterType == type).OrderByDescending(k => k.Key.Month).ToList();
            if (months.Count == 0) return null;
            var last = months[0];
            var prevMonth = last.Key.Month.AddMonths(-1);
            decimal? prev = byMonth.TryGetValue((type, prevMonth), out var p) ? p : null;
            return new LatestUsage(MonthLabel(last.Key.Month, full: true), last.Value, prev);
        }

        return new DashboardData
        {
            TotalMeters = meters.Count,
            WaterMeters = meters.Count(m => m.MeterType == MeterType.Water),
            ElectricMeters = meters.Count(m => m.MeterType == MeterType.Electricity),
            ActiveMeters = meters.Count(m => m.Status == MeterStatus.Active),
            TotalReadings = await db.MeterReadings.CountAsync(),
            LatestWater = Latest(MeterType.Water),
            LatestElectric = Latest(MeterType.Electricity),
            MonthLabels = labels,
            WaterSeries = water,
            ElectricSeries = electric,
            RecentReadings = await db.MeterReadings.AsNoTracking().Include(r => r.Meter)
                .OrderByDescending(r => r.ReadingDate).ThenByDescending(r => r.UpdatedAt)
                .Take(8).ToListAsync()
        };
    }

    public async Task<List<int>> GetAvailableYearsAsync()
    {
        var dates = await db.MeterReadings.AsNoTracking().Select(r => r.ReadingDate).ToListAsync();
        var years = dates.Select(d => d.Year).Distinct().OrderByDescending(y => y).ToList();
        if (years.Count == 0) years.Add(clock.GetLocalNow().Year);
        return years;
    }

    public async Task<ReportResult> GetReportAsync(ReportFilter f)
    {
        var years = await GetAvailableYearsAsync();
        var monthly = f.Period != "yearly";
        f.Period = monthly ? "monthly" : "yearly";
        if (monthly) f.Year ??= years.Contains(clock.GetLocalNow().Year) ? clock.GetLocalNow().Year : years[0];

        var q = db.MeterReadings.AsNoTracking().Include(r => r.Meter).AsQueryable();
        if (f.Type.HasValue) q = q.Where(r => r.Meter!.MeterType == f.Type.Value);
        if (f.MeterId.HasValue) q = q.Where(r => r.MeterId == f.MeterId.Value);
        if (monthly)
        {
            var start = new DateTime(f.Year!.Value, 1, 1);
            q = q.Where(r => r.ReadingDate >= start && r.ReadingDate < start.AddYears(1));
        }
        var details = await q.OrderBy(r => r.ReadingDate).ThenBy(r => r.Meter!.MeterCode).ToListAsync();

        string PeriodOf(DateTime d) => monthly ? MonthLabel(new DateTime(d.Year, d.Month, 1), full: true) : $"ปี {d.Year}";

        // Period keys: all 12 months for monthly; every year that has data for yearly.
        var periodKeys = monthly
            ? Enumerable.Range(1, 12).Select(m => new DateTime(f.Year!.Value, m, 1)).ToList()
            : details.Select(r => new DateTime(r.ReadingDate.Year, 1, 1)).Distinct().OrderBy(d => d).ToList();

        var periods = periodKeys.Select(k =>
        {
            var inPeriod = details.Where(r => monthly
                ? r.ReadingDate.Year == k.Year && r.ReadingDate.Month == k.Month
                : r.ReadingDate.Year == k.Year).ToList();
            return new PeriodRow(
                PeriodOf(k),
                inPeriod.Where(r => r.Meter!.MeterType == MeterType.Water).Sum(r => r.Consumption ?? 0),
                inPeriod.Where(r => r.Meter!.MeterType == MeterType.Electricity).Sum(r => r.Consumption ?? 0),
                inPeriod.Count);
        }).ToList();

        var meterRows = details.GroupBy(r => r.MeterId)
            .Select(g =>
            {
                var used = g.Where(r => r.Consumption.HasValue).Select(r => r.Consumption!.Value).ToList();
                return new MeterReportRow(
                    g.First().Meter!, g.Count(), used.Sum(),
                    used.Count > 0 ? Math.Round(used.Average(), 2) : null,
                    used.Count > 0 ? used.Max() : null,
                    g.OrderBy(r => r.ReadingDate).Last());
            })
            .OrderBy(r => r.Meter.MeterType).ThenBy(r => r.Meter.MeterCode)
            .ToList();

        var periodMeters = details
            .GroupBy(r => (Key: monthly ? new DateTime(r.ReadingDate.Year, r.ReadingDate.Month, 1) : new DateTime(r.ReadingDate.Year, 1, 1), r.MeterId))
            .OrderBy(g => g.Key.Key).ThenBy(g => g.First().Meter!.MeterCode)
            .Select(g => new PeriodMeterRow(PeriodOf(g.Key.Key), g.First().Meter!, g.Sum(r => r.Consumption ?? 0), g.Count()))
            .ToList();

        var typeText = f.Type switch
        {
            MeterType.Water => "รายงานการใช้น้ำ",
            MeterType.Electricity => "รายงานการใช้ไฟฟ้า",
            _ => "รายงานการใช้น้ำและไฟฟ้า"
        };
        var title = monthly ? $"{typeText} รายเดือน ปี {f.Year}" : $"{typeText} รายปี";
        if (f.MeterId.HasValue && meterRows.Count > 0) title += $" — มิเตอร์ {meterRows[0].Meter.MeterCode}";
        else if (f.MeterId.HasValue)
        {
            var code = await db.Meters.Where(m => m.Id == f.MeterId).Select(m => m.MeterCode).FirstOrDefaultAsync();
            if (code is not null) title += $" — มิเตอร์ {code}";
        }

        return new ReportResult
        {
            Filter = f,
            Title = title,
            AvailableYears = years,
            Periods = periods,
            Meters = meterRows,
            PeriodMeters = periodMeters,
            Details = details
        };
    }

    public static string MonthLabel(DateTime month, bool full = false) =>
        full ? $"{MeterLabels.ThaiMonthsFull[month.Month - 1]} {month.Year}"
             : $"{MeterLabels.ThaiMonthsShort[month.Month - 1]} {month.Year % 100:00}";
}
