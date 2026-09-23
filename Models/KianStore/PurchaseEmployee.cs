using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("PurchaseEmployees")]
public sealed class PurchaseEmployee
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(70)]
    public string? Mobile { get; set; }

    public bool IsActive { get; set; } = true;
}
