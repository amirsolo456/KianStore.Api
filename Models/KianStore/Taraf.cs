using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Taraf")]
public class Taraf
{
    [Key]
    [Column("ID")]
    public int Id { get; set; }

    [Column("IDType")]
    public int IdType { get; set; }

    [Column("Name")]
    [StringLength(50)]
    public string Name { get; set; } = null!;

    [Column("Addr")]
    [StringLength(200)]
    public string? Address { get; set; }

    [Column("Tel")]
    [StringLength(50)]
    public string? Phone { get; set; }

    [Column("Mobile")]
    [StringLength(70)]
    public string? Mobile { get; set; }

    [Column("Kharidar")]
    public bool IsBuyer { get; set; }

    [Column("IsDisabled")]
    public bool IsDisabled { get; set; }
}

 
