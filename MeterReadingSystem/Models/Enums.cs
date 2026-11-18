namespace MeterReadingSystem.Models;

public enum MeterType
{
    Water = 1,
    Electricity = 2
}

public enum MeterStatus
{
    Active = 1,
    Maintenance = 2,
    Inactive = 3
}

/// <summary>Thai labels and units for the enums, used by views, reports and exports.</summary>
public static class MeterLabels
{
    public static string TypeName(MeterType type) => type switch
    {
        MeterType.Water => "มิเตอร์น้ำ",
        MeterType.Electricity => "มิเตอร์ไฟฟ้า",
        _ => type.ToString()
    };

    public static string UnitOf(MeterType type) => type == MeterType.Water ? "m³" : "kWh";

    public static string StatusName(MeterStatus status) => status switch
    {
        MeterStatus.Active => "ใช้งาน",
        MeterStatus.Maintenance => "ซ่อมบำรุง",
        MeterStatus.Inactive => "ยกเลิกใช้งาน",
        _ => status.ToString()
    };

    public static readonly string[] ThaiMonthsShort =
        ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."];

    public static readonly string[] ThaiMonthsFull =
        ["มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
         "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"];
}
