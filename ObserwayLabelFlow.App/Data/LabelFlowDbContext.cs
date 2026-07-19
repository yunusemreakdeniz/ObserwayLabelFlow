using Microsoft.EntityFrameworkCore;
using ObserwayLabelFlow.Core.History;

namespace ObserwayLabelFlow.App.Data;

public sealed class LabelFlowDbContext : DbContext
{
    public LabelFlowDbContext(DbContextOptions<LabelFlowDbContext> options) : base(options)
    {
    }

    public DbSet<PrintHistoryEntry> PrintHistory => Set<PrintHistoryEntry>();

    public DbSet<InboundHistoryEntry> InboundHistory => Set<InboundHistoryEntry>();

    public DbSet<UserSettingsRecord> UserSettings => Set<UserSettingsRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSettingsRecord>(e =>
        {
            e.ToTable("UserSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.PayloadJson).IsRequired();
            e.Property(x => x.UpdatedAtUtc).IsRequired();
        });

        var e = modelBuilder.Entity<PrintHistoryEntry>();
        e.ToTable("PrintHistory");
        e.HasKey(x => x.Id);
        e.Property(x => x.TrackingNumber).IsRequired().HasMaxLength(200);
        e.Property(x => x.PdfUrl).HasMaxLength(2000);
        e.Property(x => x.Notes).HasMaxLength(2000);
        e.Property(x => x.OrderNumber).HasMaxLength(100);
        e.Property(x => x.CustomerName).HasMaxLength(200);
        e.Property(x => x.OrderStatus).HasMaxLength(50);
        e.Property(x => x.CarrierName).HasMaxLength(50);
        e.Property(x => x.PrinterName).HasMaxLength(200);
        e.Property(x => x.ErrorMessage).HasMaxLength(2000);
        e.Property(x => x.PaperSize).HasMaxLength(50);
        e.Property(x => x.PrintedBy).HasMaxLength(200);
        e.Property(x => x.SnapshotJson);
        e.HasIndex(x => x.CreatedAtUtc);
        e.HasIndex(x => x.Success);
        e.Ignore(x => x.IsSelected);

        var inbound = modelBuilder.Entity<InboundHistoryEntry>();
        inbound.ToTable("InboundHistory");
        inbound.HasKey(x => x.Id);
        inbound.Property(x => x.Reference).IsRequired().HasMaxLength(200);
        inbound.Property(x => x.OrderNumber).HasMaxLength(100);
        inbound.Property(x => x.ErrorMessage).HasMaxLength(2000);
        inbound.Property(x => x.MarkedBy).HasMaxLength(200);
        inbound.HasIndex(x => x.CreatedAtUtc);
        inbound.HasIndex(x => x.Success);
    }
}
