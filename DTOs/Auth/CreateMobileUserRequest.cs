namespace KianStore.Api.DTOs.Auth;

public sealed class CreateMobileUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Post { get; set; } = "کاربر موبایل";
    public int Access { get; set; } = 2;
}