using System.ComponentModel.DataAnnotations;

namespace MeterReadingSystem.Models;

public class MeterReading
{
    public int Id { get; set; }

    public int MeterId { get; set; }
    public Meter? Meter { get; set; }

    /// <summary>Date only (time part is always 00:00). Used for ordering — never CreatedAt.</summary>
    public DateTime ReadingDate { get; set; }

    public decimal CumulativeReading { get; set; }

    /// <summary>Current minus previous reading; null for a meter's first reading.</summary>
    public decimal? Consumption { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
