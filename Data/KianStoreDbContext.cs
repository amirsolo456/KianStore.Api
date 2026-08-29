using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Data;

public class KianStoreDbContext : DbContext
{
    public KianStoreDbContext(DbContextOptions<KianStoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<Kala> Kalas => Set<Kala>();
    public DbSet<KalaDetail> KalaDetails => Set<KalaDetail>();
    public DbSet<Taraf> Tarafs => Set<Taraf>();
    public DbSet<Anbar> Anbars => Set<Anbar>();
    public DbSet<Users> Users => Set<Users>();
    public DbSet<Sanad> Sanads => Set<Sanad>();
    public DbSet<SanadDetail> SanadDetails => Set<SanadDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Kala>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MabFrosh).HasPrecision(18, 3);
            entity.Property(x => x.MabKharid).HasPrecision(18, 3);
        });

        modelBuilder.Entity<KalaDetail>(entity =>
        {
            entity.HasKey(x => new { x.IdKala, x.IdAnbar });
            entity.Property(x => x.LastMabKharid).HasPrecision(18, 3);
            entity.Property(x => x.MabFrosh).HasPrecision(18, 3);
            entity.Property(x => x.MabFrosh1).HasPrecision(18, 3);
        });

        modelBuilder.Entity<Taraf>(entity =>
        {
            entity.HasKey(x => new { x.Id, x.IdType });
        });

        modelBuilder.Entity<Anbar>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("ID");
            entity.Property(x => x.IdSandogh).HasColumnName("IDSandogh");
            entity.Property(x => x.IdSandoghType).HasColumnName("IDSandoghType");
        });

        modelBuilder.Entity<Sanad>(entity =>
        {
            entity.HasKey(x => new { x.IdSal, x.Id });
            entity.Property(x => x.MabKol).HasPrecision(18, 3);
            entity.Property(x => x.MabNaghd).HasPrecision(18, 3);
            entity.Property(x => x.MabFrosh).HasPrecision(18, 3);
            entity.Property(x => x.Takhfif).HasPrecision(18, 3);
            entity.Property(x => x.MabDarSad).HasPrecision(18, 3);
        });

        modelBuilder.Entity<SanadDetail>(entity =>
        {
            entity.HasKey(x => new { x.IdSal, x.IdSanad, x.Id2 });
            entity.Property(x => x.BedMab).HasPrecision(18, 5);
            entity.Property(x => x.BesMab).HasPrecision(18, 5);
            entity.Property(x => x.SumMab).HasPrecision(18, 3);
            entity.Property(x => x.BedMabKharid).HasPrecision(18, 3);
            entity.Property(x => x.Maliat).HasPrecision(18, 3);
            entity.Property(x => x.HazKala).HasPrecision(18, 3);
            entity.Property(x => x.HazKalaKharid).HasPrecision(18, 3);
            entity.Property(x => x.BedMab2).HasPrecision(18, 5);
            entity.Property(x => x.BesMab2).HasPrecision(18, 5);
            entity.Property(x => x.SumTakhfifKala).HasPrecision(18, 3);
        });
    }
}
