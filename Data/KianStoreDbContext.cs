using KianStore.Api.Models.KianStore;
using Microsoft.EntityFrameworkCore;

namespace KianStore.Api.Data;

public class KianStoreDbContext : DbContext
{
    public KianStoreDbContext(DbContextOptions<KianStoreDbContext> options) : base(options) { }

    public DbSet<Kala> Kalas => Set<Kala>();
    public DbSet<KalaDetail> KalaDetails => Set<KalaDetail>();
    public DbSet<Taraf> Tarafs => Set<Taraf>();
    public DbSet<Anbar> Anbars => Set<Anbar>();
    public DbSet<Users> Users => Set<Users>();
    public DbSet<CheckDef> CheckDefs => Set<CheckDef>();
    public DbSet<Sanad> Sanads => Set<Sanad>();
    public DbSet<SanadDetail> SanadDetails => Set<SanadDetail>();
    public DbSet<Takhfif> Takhfifs => Set<Takhfif>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<DiscountCodeUsage> DiscountCodeUsages => Set<DiscountCodeUsage>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Kala>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.MabFrosh).HasPrecision(18, 3); entity.Property(x => x.MabKharid).HasPrecision(18, 3); });
        modelBuilder.Entity<KalaDetail>(entity => { entity.HasKey(x => new { x.IdKala, x.IdAnbar }); entity.Property(x => x.LastMabKharid).HasPrecision(18, 3); entity.Property(x => x.MabFrosh).HasPrecision(18, 3); entity.Property(x => x.MabFrosh1).HasPrecision(18, 3); });
        modelBuilder.Entity<Taraf>(entity => entity.HasKey(x => new { x.Id, x.IdType }));
        modelBuilder.Entity<Anbar>(entity => entity.HasKey(x => x.Id));
        modelBuilder.Entity<Users>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("ID"); entity.Property(x => x.IdSandogh).HasColumnName("IDSandogh"); entity.Property(x => x.IdSandoghType).HasColumnName("IDSandoghType"); });
        modelBuilder.Entity<CheckDef>(entity => { entity.HasKey(x => new { x.Id, x.Type }); entity.Property(x => x.Mojodi).HasPrecision(18, 0); });
        modelBuilder.Entity<Sanad>(entity => { entity.HasKey(x => new { x.IdSal, x.Id }); entity.Property(x => x.MabKol).HasPrecision(18, 3); entity.Property(x => x.MabNaghd).HasPrecision(18, 3); entity.Property(x => x.MabFrosh).HasPrecision(18, 3); entity.Property(x => x.Takhfif).HasPrecision(18, 3); entity.Property(x => x.MabDarSad).HasPrecision(18, 3); });
        modelBuilder.Entity<SanadDetail>(entity => { entity.HasKey(x => new { x.IdSal, x.IdSanad, x.Id2 }); entity.Property(x => x.BedMab).HasPrecision(18, 5); entity.Property(x => x.BesMab).HasPrecision(18, 5); entity.Property(x => x.SumMab).HasPrecision(18, 3); entity.Property(x => x.BedMabKharid).HasPrecision(18, 3); entity.Property(x => x.Maliat).HasPrecision(18, 3); entity.Property(x => x.HazKala).HasPrecision(18, 3); entity.Property(x => x.HazKalaKharid).HasPrecision(18, 3); entity.Property(x => x.BedMab2).HasPrecision(18, 5); entity.Property(x => x.BesMab2).HasPrecision(18, 5); entity.Property(x => x.SumTakhfifKala).HasPrecision(18, 3); });
        modelBuilder.Entity<Takhfif>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.TakhfifName).HasMaxLength(20).IsRequired(); entity.Property(x => x.TakhfifDarsad).HasColumnType("float"); entity.Property(x => x.ToMab1).HasPrecision(18, 3); });
        modelBuilder.Entity<DiscountCode>(entity => { entity.HasKey(x => x.Id); entity.HasIndex(x => x.Code).IsUnique(); entity.HasIndex(x => new { x.Scope, x.PersonId, x.IsActive }); entity.Property(x => x.Code).HasMaxLength(50).IsRequired(); entity.Property(x => x.Title).HasMaxLength(200); entity.Property(x => x.Value).HasPrecision(18, 3); entity.Property(x => x.MaxDiscountAmount).HasPrecision(18, 3); entity.Property(x => x.StartDate).HasColumnType("datetime2"); entity.Property(x => x.EndDate).HasColumnType("datetime2"); entity.Property(x => x.Description).HasMaxLength(1000); entity.Property(x => x.CreatedAt).HasColumnType("datetime2"); });
        modelBuilder.Entity<DiscountCodeUsage>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.OrderAmount).HasPrecision(18, 3); entity.Property(x => x.DiscountAmount).HasPrecision(18, 3); entity.Property(x => x.UsedAt).HasColumnType("datetime2"); entity.HasIndex(x => new { x.DiscountCodeId, x.PersonId }); entity.HasOne<DiscountCode>().WithMany().HasForeignKey(x => x.DiscountCodeId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<SmsTemplate>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Name).HasMaxLength(100).IsRequired(); entity.Property(x => x.TemplateText).HasMaxLength(1000).IsRequired(); entity.Property(x => x.CreatedAt).HasColumnType("datetime2"); entity.Property(x => x.UpdatedAt).HasColumnType("datetime2"); entity.HasIndex(x => x.IsActive); });
        modelBuilder.Entity<SmsLog>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Mobile).HasMaxLength(70).IsRequired(); entity.Property(x => x.Message).HasMaxLength(1000).IsRequired(); entity.Property(x => x.Status).HasColumnType("int"); entity.Property(x => x.Provider).HasMaxLength(100); entity.Property(x => x.ProviderMessageId).HasMaxLength(100); entity.Property(x => x.ErrorMessage).HasMaxLength(500); entity.Property(x => x.CreatedAt).HasColumnType("datetime2"); entity.HasOne<SmsTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull); entity.HasIndex(x => new { x.PersonId, x.CreatedAt }); entity.HasIndex(x => x.CreatedAt); });
    }
}
