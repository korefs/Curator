using TelegramStorage.DTOs;
using TelegramStorage.Models;

namespace TelegramStorage.Services;

public interface IAuthService
{
    Task<string?> LoginAsync(LoginDto loginDto);
    Task<User?> RegisterAsync(RegisterDto registerDto);
    Task<User?> GetUserByIdAsync(int userId);
    Task<User?> GetUserByEmailAsync(string email);
}