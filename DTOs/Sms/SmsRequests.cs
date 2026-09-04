namespace KianStore.Api.DTOs.Sms;

public sealed class SendSmsRequest
{
    public string Mobile { get; init; } = null!;
    public string Message { get; init; } = null!;
    public int? PersonId { get; init; }
    public int? TemplateId { get; init; }
}

public sealed class CreateSmsTemplateRequest
{
    public string Name { get; init; } = null!;
    public string TemplateText { get; init; } = null!;
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateSmsTemplateRequest
{
    public string Name { get; init; } = null!;
    public string TemplateText { get; init; } = null!;
    public bool IsActive { get; init; } = true;
}
