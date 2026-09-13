using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("SanadChangeLog")]
public sealed class SanadChangeLog
{
    public long Id { get; set; }
    public int IdSal { get; set; }
    public string IdSanad { get; set; } = null!;
    public int SanadType { get; set; }
    public string Action { get; set; } = null!;
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserFullName { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Description { get; set; }
}
