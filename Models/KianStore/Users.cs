using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Users")]
public sealed class Users
{
    public int Id { get; set; }
    public int IdSandogh { get; set; }
    public int IdSandoghType { get; set; }
}