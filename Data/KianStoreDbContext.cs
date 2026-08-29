using Microsoft.EntityFrameworkCore;
using KianStore.Api.Models.KianStore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}
