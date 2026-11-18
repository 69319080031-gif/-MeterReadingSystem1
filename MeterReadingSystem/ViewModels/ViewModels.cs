using System.ComponentModel.DataAnnotations;
using MeterReadingSystem.Models;
using MeterReadingSystem.Services;

namespace MeterReadingSystem.ViewModels;

public class LoginViewModel
{
    [Display(Name = "ชื่อผู้ใช้")]
    [Required(ErrorMessage = "กรุณากรอกชื่อผู้ใช้")]
    public string Username { get; set; } = "";

    [Display(Name = "รหัสผ่าน")]
    [Required(ErrorMessage = "กรุณากรอกรหัสผ่าน")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class MeterFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "รหัสมิเตอร์")]
    [Required(ErrorMessage = "กรุณากรอกรหัสมิเตอร์")]
    [StringLength(50, ErrorMessage = "รหัสมิเตอร์ต้องไม่เกิน 50 ตัวอักษร")]
    [RegularExpression(@"^[A-Za-z0-9ก-๙\-_/\.]+$", ErrorMessage = "รหัสมิเตอร์ใช้ได้เฉพาะตัวอักษร ตัวเลข และ - _ / . (ห้ามเว้นวรรค)")]
    public string MeterCode { get; set; } = "";

    [Display(Name = "ประเภทมิเตอร์")]
    [Required(ErrorMessage = "กรุณาเลือกประเภทมิเตอร์")]
    public MeterType? MeterType { get; set; }

    [Display(Name = "สถานที่ติดตั้ง")]
    [Required(ErrorMessage = "กรุณากรอกสถานที่ติดตั้ง")]
    [StringLength(200, ErrorMessage = "สถานที่ติดตั้งต้องไม่เกิน 200 ตัวอักษร")]
    public string Location { get; set; } = "";

    [Display(Name = "วันที่ติดตั้ง")]
    [Required(ErrorMessage = "กรุณาเลือกวันที่ติดตั้ง")]
    [DataType(DataType.Date)]
    public DateTime? InstallationDate { get; set; }

    [Display(Name = "สถานะการใช้งาน")]
    public MeterStatus Status { get; set; } = MeterStatus.Active;

    [Display(Name = "หมายเหตุ")]
    [StringLength(500, ErrorMessage = "หมายเหตุต้องไม่เกิน 500 ตัวอักษร")]
    public string? Notes { get; set; }

    public bool HasReadings { get; set; }
}

public class MeterIndexViewModel
{
    public List<MeterListItem> Items { get; set; } = [];
    public string? Search { get; set; }
    public MeterType? Type { get; set; }
    public MeterStatus? Status { get; set; }
}

public class ReadingFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "มิเตอร์")]
    [Required(ErrorMessage = "กรุณาเลือกมิเตอร์")]
    [Range(1, int.MaxValue, ErrorMessage = "กรุณาเลือกมิเตอร์")]
    public int? MeterId { get; set; }

    [Display(Name = "วันที่อ่านมิเตอร์")]
    [Required(ErrorMessage = "กรุณาเลือกวันที่อ่านมิเตอร์")]
    [DataType(DataType.Date)]
    public DateTime? ReadingDate { get; set; }

    [Display(Name = "ค่าอ่านสะสม")]
    [Required(ErrorMessage = "กรุณากรอกค่าอ่านสะสม")]
    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "ค่าอ่านสะสมต้องเป็นตัวเลขตั้งแต่ 0 ขึ้นไป")]
    public decimal? CumulativeReading { get; set; }

    [Display(Name = "หมายเหตุ")]
    [StringLength(500, ErrorMessage = "หมายเหตุต้องไม่เกิน 500 ตัวอักษร")]
    public string? Notes { get; set; }

    public List<Meter> Meters { get; set; } = [];
    /// <summary>For edit: the fixed meter of the reading.</summary>
    public Meter? Meter { get; set; }
    public bool IsEdit => Id.HasValue;
}

public class ReadingHistoryViewModel
{
    public ReadingFilter Filter { get; set; } = new();
    public PagedResult<MeterReading> Result { get; set; } = new();
    public List<Meter> Meters { get; set; } = [];
}

public class ReportViewModel
{
    public ReportResult Report { get; set; } = new();
    public List<Meter> Meters { get; set; } = [];
}
