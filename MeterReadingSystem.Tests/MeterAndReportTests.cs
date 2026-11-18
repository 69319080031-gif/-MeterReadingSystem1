using System.Text;
using MeterReadingSystem.Models;
using MeterReadingSystem.Services;

namespace MeterReadingSystem.Tests;

public class MeterServiceTests : IDisposable
{
    private readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    [Fact]
    public async Task Create_water_and_electricity_meters_sets_units()
    {
        var w = await t.AddMeterAsync("W-001", MeterType.Water);
        var e = await t.AddMeterAsync("E-001", MeterType.Electricity);
        Assert.Equal("m³", w.Unit);
        Assert.Equal("kWh", e.Unit);
    }

    [Fact]
    public async Task Duplicate_meter_code_is_rejected_case_insensitive()
    {
        await t.AddMeterAsync("W-001");
        var r = await t.Meters.CreateAsync(new MeterInput("w-001", MeterType.Water, "x", new DateTime(2025, 1, 1), MeterStatus.Active, null));
        Assert.False(r.Success);
        Assert.Contains("มีอยู่ในระบบแล้ว", r.Error);
    }

    [Fact]
    public async Task Update_meter_changes_fields()
    {
        var m = await t.AddMeterAsync();
        var r = await t.Meters.UpdateAsync(m.Id, new MeterInput("W-009", MeterType.Electricity, "อาคาร B", new DateTime(2025, 2, 1), MeterStatus.Maintenance, "หมายเหตุ"));
        Assert.True(r.Success, r.Error);
        var saved = await t.Meters.GetAsync(m.Id);
        Assert.Equal("W-009", saved!.MeterCode);
        Assert.Equal("kWh", saved.Unit);
        Assert.Equal(MeterStatus.Maintenance, saved.Status);
    }

    [Fact]
    public async Task Changing_type_of_meter_with_readings_is_rejected()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, new DateTime(2026, 1, 1), 10);
        var r = await t.Meters.UpdateAsync(m.Id, new MeterInput(m.MeterCode, MeterType.Electricity, m.Location, m.InstallationDate, m.Status, null));
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Installation_date_after_first_reading_is_rejected()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, new DateTime(2026, 1, 1), 10);
        var r = await t.Meters.UpdateAsync(m.Id, new MeterInput(m.MeterCode, m.MeterType, m.Location, new DateTime(2026, 2, 1), m.Status, null));
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Delete_meter_removes_its_readings()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, new DateTime(2026, 1, 1), 10);
        var r = await t.Meters.DeleteAsync(m.Id);
        Assert.True(r.Success);
        Assert.Empty(t.Series(m.Id));
        Assert.Null(await t.Meters.GetAsync(m.Id));
    }

    [Fact]
    public async Task List_filters_by_search_type_and_status()
    {
        await t.AddMeterAsync("W-001");
        await t.AddMeterAsync("E-001", MeterType.Electricity, MeterStatus.Inactive);
        Assert.Single(await t.Meters.ListAsync("e-0"));
        Assert.Single(await t.Meters.ListAsync(type: MeterType.Water));
        Assert.Single(await t.Meters.ListAsync(status: MeterStatus.Inactive));
        Assert.Single(await t.Meters.ListForSelectAsync(activeOnly: true));
    }
}

public class ReportServiceTests : IDisposable
{
    private readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    private async Task SeedAsync()
    {
        var w = await t.AddMeterAsync("W-001");
        var e = await t.AddMeterAsync("E-001", MeterType.Electricity);
        await t.AddReadingAsync(w.Id, new DateTime(2026, 1, 1), 1000);
        await t.AddReadingAsync(w.Id, new DateTime(2026, 2, 1), 1150);
        await t.AddReadingAsync(w.Id, new DateTime(2026, 3, 1), 1280);
        await t.AddReadingAsync(e.Id, new DateTime(2025, 12, 1), 5000);
        await t.AddReadingAsync(e.Id, new DateTime(2026, 1, 1), 5400);
        await t.AddReadingAsync(e.Id, new DateTime(2026, 2, 1), 5900);
    }

    [Fact]
    public async Task Monthly_report_sums_consumption_by_reading_month()
    {
        await SeedAsync();
        var r = await t.Reports.GetReportAsync(new ReportFilter { Period = "monthly", Year = 2026 });

        Assert.Equal(12, r.Periods.Count);
        Assert.Equal(0, r.Periods[0].Water);       // Jan: first water reading
        Assert.Equal(400, r.Periods[0].Electric);  // Jan: 5400-5000
        Assert.Equal(150, r.Periods[1].Water);
        Assert.Equal(500, r.Periods[1].Electric);
        Assert.Equal(130, r.Periods[2].Water);
        Assert.Equal(280, r.TotalWater);
        Assert.Equal(900, r.TotalElectric);
    }

    [Fact]
    public async Task Yearly_report_groups_by_year()
    {
        await SeedAsync();
        var r = await t.Reports.GetReportAsync(new ReportFilter { Period = "yearly" });
        Assert.Equal(["ปี 2025", "ปี 2026"], r.Periods.Select(p => p.Label));
        Assert.Equal(900, r.Periods[1].Electric);
    }

    [Fact]
    public async Task Report_filters_by_type_and_meter()
    {
        await SeedAsync();
        var water = await t.Reports.GetReportAsync(new ReportFilter { Type = MeterType.Water, Year = 2026 });
        Assert.Equal(0, water.TotalElectric);
        Assert.Equal(280, water.TotalWater);
        Assert.Single(water.Meters);
        Assert.Equal(140, water.Meters[0].Average);
        Assert.Equal(150, water.Meters[0].Max);

        var meterId = water.Meters[0].Meter.Id;
        var one = await t.Reports.GetReportAsync(new ReportFilter { MeterId = meterId, Year = 2026 });
        Assert.Contains("W-001", one.Title);
    }

    [Fact]
    public async Task Dashboard_counts_and_latest_usage()
    {
        await SeedAsync();
        var d = await t.Reports.GetDashboardAsync();
        Assert.Equal(2, d.TotalMeters);
        Assert.Equal(1, d.WaterMeters);
        Assert.Equal(1, d.ElectricMeters);
        Assert.Equal(6, d.TotalReadings);
        Assert.Equal(130, d.LatestWater!.Total);       // March 2026
        Assert.Equal(500, d.LatestElectric!.Total);    // February 2026
        Assert.Equal(25, d.LatestElectric.ChangePercent);
        Assert.Equal(12, d.MonthLabels.Count);
    }

    [Fact]
    public async Task Exports_produce_csv_with_bom_and_valid_xlsx()
    {
        await SeedAsync();
        var r = await t.Reports.GetReportAsync(new ReportFilter { Year = 2026 });

        var csv = ExportService.ReportCsv(r);
        Assert.Equal(Encoding.UTF8.GetPreamble(), csv.Take(3));
        Assert.Contains("W-001", Encoding.UTF8.GetString(csv));

        var xlsx = ExportService.ReportExcel(r);
        Assert.Equal((byte)'P', xlsx[0]); // zip signature "PK"
        Assert.Equal((byte)'K', xlsx[1]);
    }

    [Fact]
    public async Task Csv_neutralises_formula_injection()
    {
        var m = await t.AddMeterAsync();
        await t.Readings.CreateAsync(new ReadingInput(m.Id, new DateTime(2026, 1, 1), 1, "=HYPERLINK(\"x\")"));
        var list = await t.Readings.ListAllAsync(new ReadingFilter());
        var text = Encoding.UTF8.GetString(ExportService.ReadingsCsv(list));
        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", text);
    }
}
