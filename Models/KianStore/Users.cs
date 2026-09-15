using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("Users")]
public sealed class Users
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string LName { get; set; } = string.Empty;
    public string UserFLName { get; set; } = string.Empty;
    public int Access { get; set; }
    public int IdSandogh { get; set; }
    public int IdSandoghType { get; set; }
    public int IdAnbar { get; set; }
    public bool LockByIdAnbar { get; set; }
}
