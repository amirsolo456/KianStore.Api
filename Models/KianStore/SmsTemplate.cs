using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("SmsTemplate")]
public sealed class SmsTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string TemplateText { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
