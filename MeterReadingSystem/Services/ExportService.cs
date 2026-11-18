using System.Text;
using ClosedXML.Excel;
using MeterReadingSystem.Models;

namespace MeterReadingSystem.Services;

/// <summary>Builds CSV (UTF-8 with BOM so Excel shows Thai correctly) and .xlsx files.</summary>
public static class ExportService
{
    public static byte[] ReportCsv(ReportResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Csv(r.Title));
        sb.AppendLine(Csv("ช่วงเวลา", "รหัสมิเตอร์", "ประเภท", "สถานที่ติดตั้ง", "หน่วย", "ปริมาณการใช้", "จำนวนรายการอ่าน"));
        foreach (var p in r.PeriodMeters)
            sb.AppendLine(Csv(p.Period, p.Meter.MeterCode, MeterLabels.TypeName(p.Meter.MeterType), p.Meter.Location,
                p.Meter.Unit, Plain(p.Total), p.ReadingCount.ToString()));
        return WithBom(sb);
    }

    public static byte[] ReadingsCsv(IEnumerable<MeterReading> readings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Csv("วันที่อ่าน", "รหัสมิเตอร์", "ประเภท", "สถานที่ติดตั้ง", "ค่าอ่านสะสม", "ปริมาณการใช้", "หน่วย", "หมายเหตุ"));
        foreach (var x in readings)
            sb.AppendLine(Csv(Fmt.Iso(x.ReadingDate), x.Meter!.MeterCode, MeterLabels.TypeName(x.Meter.MeterType),
                x.Meter.Location, Plain(x.CumulativeReading), x.Consumption.HasValue ? Plain(x.Consumption.Value) : "",
                x.Meter.Unit, x.Notes ?? ""));
        return WithBom(sb);
    }

    public static byte[] ReportExcel(ReportResult r)
    {
        using var wb = new XLWorkbook();

        var s1 = wb.AddWorksheet("สรุปตามช่วงเวลา");
        s1.Cell(1, 1).Value = r.Title;
        s1.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        var headers = new List<string> { "ช่วงเวลา" };
        if (r.ShowWater) headers.Add("ปริมาณการใช้น้ำ (m³)");
        if (r.ShowElectric) headers.Add("ปริมาณการใช้ไฟฟ้า (kWh)");
        headers.Add("จำนวนรายการอ่าน");
        WriteHeader(s1, 3, headers);
        var row = 4;
        foreach (var p in r.Periods)
        {
            var c = 1;
            s1.Cell(row, c++).Value = p.Label;
            if (r.ShowWater) s1.Cell(row, c++).Value = p.Water;
            if (r.ShowElectric) s1.Cell(row, c++).Value = p.Electric;
            s1.Cell(row, c).Value = p.ReadingCount;
            row++;
        }
        var t = 1;
        s1.Cell(row, t++).Value = "รวม";
        if (r.ShowWater) s1.Cell(row, t++).Value = r.TotalWater;
        if (r.ShowElectric) s1.Cell(row, t++).Value = r.TotalElectric;
        s1.Cell(row, t).Value = r.TotalReadings;
        s1.Row(row).Style.Font.SetBold();
        Finish(s1, 4, row, headers.Count);

        var s2 = wb.AddWorksheet("สรุปตามมิเตอร์");
        var h2 = new List<string> { "รหัสมิเตอร์", "ประเภท", "สถานที่ติดตั้ง", "หน่วย", "จำนวนรายการอ่าน", "ปริมาณการใช้รวม", "เฉลี่ยต่อครั้ง", "สูงสุดต่อครั้ง", "ค่าอ่านล่าสุด", "วันที่อ่านล่าสุด" };
        WriteHeader(s2, 1, h2);
        row = 2;
        foreach (var m in r.Meters)
        {
            s2.Cell(row, 1).Value = m.Meter.MeterCode;
            s2.Cell(row, 2).Value = MeterLabels.TypeName(m.Meter.MeterType);
            s2.Cell(row, 3).Value = m.Meter.Location;
            s2.Cell(row, 4).Value = m.Meter.Unit;
            s2.Cell(row, 5).Value = m.ReadingCount;
            s2.Cell(row, 6).Value = m.Total;
            if (m.Average.HasValue) s2.Cell(row, 7).Value = m.Average.Value;
            if (m.Max.HasValue) s2.Cell(row, 8).Value = m.Max.Value;
            if (m.Latest is not null)
            {
                s2.Cell(row, 9).Value = m.Latest.CumulativeReading;
                s2.Cell(row, 10).Value = m.Latest.ReadingDate;
                s2.Cell(row, 10).Style.DateFormat.Format = "dd/MM/yyyy";
            }
            row++;
        }
        Finish(s2, 2, row - 1, h2.Count);

        var s3 = wb.AddWorksheet("รายละเอียดค่าอ่าน");
        var h3 = new List<string> { "วันที่อ่าน", "รหัสมิเตอร์", "ประเภท", "สถานที่ติดตั้ง", "ค่าอ่านสะสม", "ปริมาณการใช้", "หน่วย", "หมายเหตุ" };
        WriteHeader(s3, 1, h3);
        row = 2;
        foreach (var d in r.Details)
        {
            s3.Cell(row, 1).Value = d.ReadingDate;
            s3.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            s3.Cell(row, 2).Value = d.Meter!.MeterCode;
            s3.Cell(row, 3).Value = MeterLabels.TypeName(d.Meter.MeterType);
            s3.Cell(row, 4).Value = d.Meter.Location;
            s3.Cell(row, 5).Value = d.CumulativeReading;
            if (d.Consumption.HasValue) s3.Cell(row, 6).Value = d.Consumption.Value;
            s3.Cell(row, 7).Value = d.Meter.Unit;
            s3.Cell(row, 8).Value = d.Notes ?? "";
            row++;
        }
        Finish(s3, 2, row - 1, h3.Count);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteHeader(IXLWorksheet ws, int row, List<string> headers)
    {
        for (var i = 0; i < headers.Count; i++) ws.Cell(row, i + 1).Value = headers[i];
        var range = ws.Range(row, 1, row, headers.Count);
        range.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#2563EB"));
    }

    private static void Finish(IXLWorksheet ws, int firstDataRow, int lastRow, int cols)
    {
        if (lastRow >= firstDataRow)
            ws.Range(firstDataRow, 1, lastRow, cols).CellsUsed(c => c.DataType == XLDataType.Number)
              .Style.NumberFormat.Format = "#,##0.###";
        ws.Columns(1, cols).AdjustToContents();
    }

    private static string Plain(decimal v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string Csv(params string[] fields) => string.Join(",", fields.Select(CsvField));

    private static string CsvField(string f)
    {
        // Neutralise spreadsheet formula injection (=, +, -, @ at the start of user text).
        if (f.Length > 0 && "=+-@\t\r".Contains(f[0])) f = "'" + f;
        return f.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{f.Replace("\"", "\"\"")}\"" : f;
    }

    private static byte[] WithBom(StringBuilder sb) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
}
