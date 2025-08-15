using TelegramStorage.Models;

namespace TelegramStorage.Services;

public interface IJwtService
{
    string GenerateToken(User user);
    bool ValidateToken(string token);
    string GenerateRefreshToken();
    Task<string> GetOrCreateSecretKeyAsync();
}