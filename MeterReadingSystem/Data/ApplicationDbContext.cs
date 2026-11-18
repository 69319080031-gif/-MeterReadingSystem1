using MeterReadingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace MeterReadingSystem.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Meter>(e =>
        {
            e.HasIndex(m => m.MeterCode).IsUnique();
            e.Property(m => m.MeterType).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            e.HasMany(m => m.Readings)
             .WithOne(r => r.Meter)
             .HasForeignKey(r => r.MeterId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MeterReading>(e =>
        {
            // One reading per meter per day.
            e.HasIndex(r => new { r.MeterId, r.ReadingDate }).IsUnique();
            e.Property(r => r.CumulativeReading).HasPrecision(18, 3);
            e.Property(r => r.Consumption).HasPrecision(18, 3);
        });
    }
}
