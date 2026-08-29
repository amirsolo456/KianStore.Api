using Microsoft.EntityFrameworkCore;
using KianStore.Api.Models.KianStore;
using KianStore.Api.Models.Orders;

namespace KianStore.Api.Data;

public class KianStoreDbContext : DbContext
{
    public KianStoreDbContext(DbContextOptions<KianStoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<Kala> Kalas { get; set; }
    public DbSet<Taraf> Tarafs { get; set; }
    public DbSet<Sanad> Sanads { get; set; }
    public DbSet<SanadDetail> SanadDetails { get; set; }
    public DbSet<MobileOrder> MobileOrders { get; set; }
    public DbSet<MobileOrderItem> MobileOrderItems { get; set; }
    public DbSet<MobileOrderPayment> MobileOrderPayments { get; set; }
    public DbSet<StoreAnbarMojodi> StoreAnbarMojodis
    => Set<StoreAnbarMojodi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StoreAnbarMojodi>(entity =>
        {
            entity.HasNoKey();

            entity.ToView("StoreAnbarMojodi");

            entity.Property(x => x.IDSal)
                .HasColumnName("IDSal");

            entity.Property(x => x.IDAnbar)
                .HasColumnName("IDAnbar");

            entity.Property(x => x.IDKala)
                .HasColumnName("IDKala");

            entity.Property(x => x.KalaName)
                .HasColumnName("KalaName");

            entity.Property(x => x.Mojoodi)
                .HasColumnName("Mojoodi");
        });

        modelBuilder.Entity<Taraf>(entity =>
        {
            entity.HasKey(x => new { x.Id, x.IdType });
        });

        modelBuilder.Entity<Kala>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MabFrosh).HasPrecision(18, 3);
            entity.Property(x => x.MabKharid).HasPrecision(18, 3);
        });

        modelBuilder.Entity<Sanad>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 3);
        });

        modelBuilder.Entity<SanadDetail>(entity =>
        {
            entity.HasKey(x => new { x.Id, x.IdKala });
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 3);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 3);
        });

        modelBuilder.Entity<MobileOrder>(entity =>
        {
            entity.Property(x => x.PaymentAmount).HasPrecision(18, 3);
        });

        modelBuilder.Entity<MobileOrderItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 3);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 3);
        });

        modelBuilder.Entity<MobileOrderPayment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 3);
        });
    }
}
