using System.Security.Cryptography;
using System.Text;

namespace KianStore.Api.Services.Implementations;

public static class JwtKeyProvider
{
    public static string GetOrCreate(IConfiguration configuration)
    {
        var configured = configuration["Jwt:Key"];
        if (IsValid(configured))
            return configured!;

        var environment = Environment.GetEnvironmentVariable("Jwt__Key");
        if (IsValid(environment))
        {
            configuration["Jwt:Key"] = environment;
            return environment!;
        }

        var securityDirectory = Path.Combine(AppContext.BaseDirectory, ".security");
        var keyPath = Path.Combine(securityDirectory, "jwt.key");

        try
        {
            Directory.CreateDirectory(securityDirectory);
            if (File.Exists(keyPath))
            {
                var fileKey = File.ReadAllText(keyPath).Trim();
                if (IsValid(fileKey))
                {
                    configuration["Jwt:Key"] = fileKey;
                    return fileKey;
                }
            }

            var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            File.WriteAllText(keyPath, generated, Encoding.UTF8);
            configuration["Jwt:Key"] = generated;
            return generated;
        }
        catch
        {
            // Keep the API usable even when the executable folder is read-only.
            // The value lives for this process and is shared through IConfiguration.
            var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            configuration["Jwt:Key"] = generated;
            return generated;
        }
    }

    private static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && Encoding.UTF8.GetByteCount(value.Trim()) >= 32;
}
