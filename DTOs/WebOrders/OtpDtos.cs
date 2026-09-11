namespace KianStore.Api.DTOs.WebOrders;

public sealed class SendOrderOtpRequest
{
    public string Mobile { get; set; } = string.Empty;
}

public sealed class VerifyOrderOtpRequest
{
    public string Mobile { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
}

public sealed class OrderOtpResponse
{
    public string Challenge { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
}
