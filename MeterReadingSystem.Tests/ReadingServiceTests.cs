using MeterReadingSystem.Models;
using MeterReadingSystem.Services;

namespace MeterReadingSystem.Tests;

public class ReadingServiceTests : IDisposable
{
    private readonly TestDb t = new();
    public void Dispose() => t.Dispose();

    private static DateTime D(int month, int day = 1) => new(2026, month, day);

    [Fact]
    public async Task First_reading_has_no_consumption()
    {
        var m = await t.AddMeterAsync();
        var r = await t.AddReadingAsync(m.Id, D(1), 1000);
        Assert.Null(r.Consumption);
    }

    [Fact]
    public async Task Consumption_is_current_minus_previous_spec_example()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        await t.AddReadingAsync(m.Id, D(2), 1150);
        await t.AddReadingAsync(m.Id, D(3), 1280);
        await t.AddReadingAsync(m.Id, D(4), 1400);

        Assert.Equal(new decimal?[] { null, 150, 130, 120 }, t.Series(m.Id).Select(s => s.Consumption));
    }

    [Fact]
    public async Task Decimal_readings_are_exact()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000.1m);
        var r = await t.AddReadingAsync(m.Id, D(2), 1150.3m);
        Assert.Equal(150.2m, r.Consumption);
    }

    [Fact]
    public async Task Backdated_insert_between_valid_readings_is_saved_and_neighbours_recalculated()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        await t.AddReadingAsync(m.Id, D(3), 1200);

        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, D(2), 1100, null));

        Assert.True(r.Success, r.Error);
        Assert.Equal(new decimal?[] { null, 100, 100 }, t.Series(m.Id).Select(s => s.Consumption));
    }

    [Fact]
    public async Task Backdated_insert_higher_than_next_reading_is_rejected()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        await t.AddReadingAsync(m.Id, D(3), 1200);

        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, D(2), 1300, null));

        Assert.False(r.Success);
        Assert.Contains("สูงกว่าค่าอ่านครั้งถัดไป", r.Error);
        Assert.Contains("01/03/2026", r.Error);
        Assert.Equal(2, t.Series(m.Id).Count);
    }

    [Fact]
    public async Task Reading_lower_than_previous_is_rejected()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);

        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, D(2), 999, null));

        Assert.False(r.Success);
        Assert.Contains("ต่ำกว่าค่าอ่านครั้งก่อนหน้า", r.Error);
    }

    [Fact]
    public async Task Reading_equal_to_previous_is_allowed_with_zero_consumption()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        var r = await t.AddReadingAsync(m.Id, D(2), 1000);
        Assert.Equal(0, r.Consumption);
    }

    [Fact]
    public async Task Duplicate_reading_on_same_day_is_rejected()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);

        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, D(1).AddHours(15), 1000, null));

        Assert.False(r.Success);
        Assert.Contains("ไม่สามารถบันทึกซ้ำในวันเดียวกันได้", r.Error);
    }

    [Fact]
    public async Task Same_day_on_different_meters_is_allowed()
    {
        var a = await t.AddMeterAsync("W-001");
        var b = await t.AddMeterAsync("E-001", MeterType.Electricity);
        await t.AddReadingAsync(a.Id, D(1), 1000);
        await t.AddReadingAsync(b.Id, D(1), 5000);
    }

    [Fact]
    public async Task Negative_reading_is_rejected()
    {
        var m = await t.AddMeterAsync();
        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, D(1), -1, null));
        Assert.False(r.Success);
        Assert.Contains("ติดลบ", r.Error);
    }

    [Fact]
    public async Task Future_date_is_rejected()
    {
        var m = await t.AddMeterAsync();
        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, TestDb.Today.AddDays(1), 10, null));
        Assert.False(r.Success);
        Assert.Contains("อนาคต", r.Error);
    }

    [Fact]
    public async Task Date_before_installation_is_rejected()
    {
        var m = await t.AddMeterAsync(installed: D(3));
        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, D(2), 10, null));
        Assert.False(r.Success);
        Assert.Contains("วันที่ติดตั้ง", r.Error);
    }

    [Fact]
    public async Task Inactive_meter_cannot_take_new_readings()
    {
        var m = await t.AddMeterAsync(status: MeterStatus.Inactive);
        var r = await t.Readings.CreateAsync(new ReadingInput(m.Id, D(1), 10, null));
        Assert.False(r.Success);
        Assert.Contains("ยกเลิกใช้งาน", r.Error);
    }

    [Fact]
    public async Task Order_is_by_ReadingDate_not_CreatedAt()
    {
        var m = await t.AddMeterAsync();
        // Entered in reverse chronological order.
        await t.AddReadingAsync(m.Id, D(4), 1400);
        await t.AddReadingAsync(m.Id, D(2), 1150);
        await t.AddReadingAsync(m.Id, D(1), 1000);
        await t.AddReadingAsync(m.Id, D(3), 1280);

        Assert.Equal(new decimal?[] { null, 150, 130, 120 }, t.Series(m.Id).Select(s => s.Consumption));
    }

    [Fact]
    public async Task Editing_backdated_reading_recalculates_itself_and_next()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        var feb = await t.AddReadingAsync(m.Id, D(2), 1150);
        await t.AddReadingAsync(m.Id, D(3), 1280);

        var r = await t.Readings.UpdateAsync(feb.Id, new ReadingInput(m.Id, D(2), 1100, "แก้ไข"));

        Assert.True(r.Success, r.Error);
        Assert.Equal(new decimal?[] { null, 100, 180 }, t.Series(m.Id).Select(s => s.Consumption));
    }

    [Fact]
    public async Task Editing_to_value_above_next_is_rejected_and_data_unchanged()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        var feb = await t.AddReadingAsync(m.Id, D(2), 1150);
        await t.AddReadingAsync(m.Id, D(3), 1280);

        var r = await t.Readings.UpdateAsync(feb.Id, new ReadingInput(m.Id, D(2), 1300, null));

        Assert.False(r.Success);
        var s = t.Series(m.Id);
        Assert.Equal(1150, s[1].Value);
        Assert.Equal(new decimal?[] { null, 150, 130 }, s.Select(x => x.Consumption));
    }

    [Fact]
    public async Task Editing_the_date_moves_reading_and_recalculates_whole_series()
    {
        var m = await t.AddMeterAsync();
        var jan = await t.AddReadingAsync(m.Id, D(1), 1000);
        await t.AddReadingAsync(m.Id, D(3), 1300);
        var x = await t.AddReadingAsync(m.Id, D(5), 1500);

        // Move the May reading (1500) to April: still valid.
        var ok = await t.Readings.UpdateAsync(x.Id, new ReadingInput(m.Id, D(4), 1500, null));
        Assert.True(ok.Success, ok.Error);

        // Move the January reading (1000) after March: 1000 < 1300 → rejected.
        var bad = await t.Readings.UpdateAsync(jan.Id, new ReadingInput(m.Id, D(3, 20), 1000, null));
        Assert.False(bad.Success);

        var s = t.Series(m.Id);
        Assert.Equal(D(4), s[2].Date);
        Assert.Equal(new decimal?[] { null, 300, 200 }, s.Select(v => v.Consumption));
    }

    [Fact]
    public async Task Editing_onto_another_readings_date_is_rejected_as_duplicate()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        var feb = await t.AddReadingAsync(m.Id, D(2), 1100);

        var r = await t.Readings.UpdateAsync(feb.Id, new ReadingInput(m.Id, D(1), 1100, null));
        Assert.False(r.Success);
        Assert.Contains("ซ้ำ", r.Error);
    }

    [Fact]
    public async Task Deleting_middle_reading_recalculates_next()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        var feb = await t.AddReadingAsync(m.Id, D(2), 1150);
        await t.AddReadingAsync(m.Id, D(3), 1280);

        var r = await t.Readings.DeleteAsync(feb.Id);

        Assert.True(r.Success);
        Assert.Equal(new decimal?[] { null, 280 }, t.Series(m.Id).Select(s => s.Consumption));
    }

    [Fact]
    public async Task Deleting_first_reading_makes_next_the_first_without_consumption()
    {
        var m = await t.AddMeterAsync();
        var jan = await t.AddReadingAsync(m.Id, D(1), 1000);
        await t.AddReadingAsync(m.Id, D(2), 1150);
        await t.AddReadingAsync(m.Id, D(3), 1280);

        await t.Readings.DeleteAsync(jan.Id);

        Assert.Equal(new decimal?[] { null, 130 }, t.Series(m.Id).Select(s => s.Consumption));
    }

    [Fact]
    public async Task Deleting_missing_reading_fails_gracefully()
    {
        var r = await t.Readings.DeleteAsync(999);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Neighbours_are_found_by_date()
    {
        var m = await t.AddMeterAsync();
        await t.AddReadingAsync(m.Id, D(1), 1000);
        await t.AddReadingAsync(m.Id, D(3), 1200);

        var n = await t.Readings.GetNeighboursAsync(m.Id, D(2));

        Assert.Equal(1000, n.Previous!.CumulativeReading);
        Assert.Equal(1200, n.Next!.CumulativeReading);
    }

    [Fact]
    public async Task Search_filters_by_code_type_and_date_range_and_sorts()
    {
        var w = await t.AddMeterAsync("W-001");
        var e = await t.AddMeterAsync("E-001", MeterType.Electricity);
        await t.AddReadingAsync(w.Id, D(1), 10);
        await t.AddReadingAsync(w.Id, D(2), 20);
        await t.AddReadingAsync(e.Id, D(2), 100);
        await t.AddReadingAsync(e.Id, D(3), 200);

        Assert.Equal(2, (await t.Readings.SearchAsync(new ReadingFilter { Search = "w-0" })).TotalCount);
        Assert.Equal(2, (await t.Readings.SearchAsync(new ReadingFilter { Type = MeterType.Electricity })).TotalCount);
        Assert.Equal(2, (await t.Readings.SearchAsync(new ReadingFilter { From = D(2), To = D(2) })).TotalCount);

        var asc = await t.Readings.SearchAsync(new ReadingFilter { Sort = "asc" });
        Assert.Equal(D(1), asc.Items.First().ReadingDate);
        var desc = await t.Readings.SearchAsync(new ReadingFilter { Sort = "desc" });
        Assert.Equal(D(3), desc.Items.First().ReadingDate);
    }
}
