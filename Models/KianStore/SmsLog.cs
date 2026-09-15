using System.ComponentModel.DataAnnotations.Schema;

namespace KianStore.Api.Models.KianStore;

[Table("SmsLog")]
public sealed class SmsLog
{
    public long Id { get; set; }
    public int? PersonId { get; set; }
    public int? IdSal { get; set; }
    public string? IdSanad { get; set; }
    public string Mobile { get; set; } = null!;
    public string Message { get; set; } = null!;
    public int? TemplateId { get; set; }
    public int Status { get; set; } // 1=pending, 2=provider accepted, 3=failed
    public string? Provider { get; set; }
    public string? ProviderMessageId { get; set; }
    public int? ProviderStatus { get; set; }
    public string? ProviderStatusText { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? LastStatusCheckedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
