using MeterReadingSystem.Data;
using MeterReadingSystem.Models;
using MeterReadingSystem.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MeterReadingSystem.Tests;

/// <summary>Fixed "today" so date rules are deterministic.</summary>
public class FixedClock(DateTime today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.AddHours(10), TimeSpan.Zero);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Real SQLite (in-memory) with the real migrations — same engine as production.</summary>
public sealed class TestDb : IDisposable
{
    public static readonly DateTime Today = new(2026, 6, 15);

    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    public ApplicationDbContext Db { get; }
    public TimeProvider Clock { get; } = new FixedClock(Today);
    public MeterService Meters { get; }
    public ReadingService Readings { get; }
    public ReportService Reports { get; }

    public TestDb()
    {
        _conn.Open();
        Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conn).Options);
        Db.Database.Migrate();
        Meters = new MeterService(Db, Clock);
        Readings = new ReadingService(Db, Clock);
        Reports = new ReportService(Db, Clock);
    }

    public async Task<Meter> AddMeterAsync(string code = "W-001", MeterType type = MeterType.Water,
        MeterStatus status = MeterStatus.Active, DateTime? installed = null)
    {
        var r = await Meters.CreateAsync(new MeterInput(code, type, "อาคาร A", installed ?? new DateTime(2025, 1, 1), status, null));
        Assert.True(r.Success, r.Error);
        return r.Value!;
    }

    public async Task<MeterReading> AddReadingAsync(int meterId, DateTime date, decimal value)
    {
        var r = await Readings.CreateAsync(new ReadingInput(meterId, date, value, null));
        Assert.True(r.Success, r.Error);
        return r.Value!;
    }

    /// <summary>Fresh read from the database, ordered by ReadingDate.</summary>
    public List<(DateTime Date, decimal Value, decimal? Consumption)> Series(int meterId)
    {
        Db.ChangeTracker.Clear();
        return Db.MeterReadings.Where(r => r.MeterId == meterId).OrderBy(r => r.ReadingDate)
            .AsEnumerable().Select(r => (r.ReadingDate, r.CumulativeReading, r.Consumption)).ToList();
    }

    public void Dispose()
    {
        Db.Dispose();
        _conn.Dispose();
    }
}
