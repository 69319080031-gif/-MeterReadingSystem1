using System.ComponentModel.DataAnnotations;

namespace MeterReadingSystem.Models;

public class Meter
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string MeterCode { get; set; } = "";

    public MeterType MeterType { get; set; }

    [Required, StringLength(200)]
    public string Location { get; set; } = "";

    public DateTime InstallationDate { get; set; }

    [Required, StringLength(10)]
    public string Unit { get; set; } = "";

    [StringLength(500)]
    public string? Notes { get; set; }

    public MeterStatus Status { get; set; } = MeterStatus.Active;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<MeterReading> Readings { get; set; } = [];
}
