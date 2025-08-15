using Microsoft.EntityFrameworkCore;
using TelegramStorage.Data;
using TelegramStorage.DTOs;
using TelegramStorage.Models;
using BCrypt.Net;

namespace TelegramStorage.Services;

public class AuthService : IAuthService
{
    private readonly TelegramStorageContext _context;
    private readonly IJwtService _jwtService;
    private readonly IInputSanitizationService _inputSanitizationService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        TelegramStorageContext context, 
        IJwtService jwtService,
        IInputSanitizationService inputSanitizationService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _inputSanitizationService = inputSanitizationService;
        _logger = logger;
    }

    public async Task<string?> LoginAsync(LoginDto loginDto)
    {
        try
        {
            // Sanitize and validate input
            var sanitizedEmail = _inputSanitizationService.SanitizeEmail(loginDto.Email);
            
            if (string.IsNullOrEmpty(sanitizedEmail) || !_inputSanitizationService.IsValidEmail(sanitizedEmail))
            {
                _logger.LogWarning("Invalid email format in login attempt: {Email}", loginDto.Email);
                return null;
            }

            // Check for injection patterns
            if (_inputSanitizationService.ContainsSqlInjectionPatterns(loginDto.Email) ||
                _inputSanitizationService.ContainsSqlInjectionPatterns(loginDto.Password))
            {
                _logger.LogWarning("Potential injection attack detected in login attempt for email: {Email}", sanitizedEmail);
                return null;
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == sanitizedEmail && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                // Log failed login attempt
                _logger.LogWarning("Failed login attempt for email: {Email}", sanitizedEmail);
                return null;
            }

            _logger.LogInformation("Successful login for user: {UserId}", user.Id);
            return _jwtService.GenerateToken(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email: {Email}", loginDto.Email);
            return null;
        }
    }

    public async Task<User?> RegisterAsync(RegisterDto registerDto)
    {
        try
        {
            // Sanitize and validate input
            var sanitizedEmail = _inputSanitizationService.SanitizeEmail(registerDto.Email);
            var sanitizedUsername = _inputSanitizationService.SanitizeUsername(registerDto.Username);
            
            if (string.IsNullOrEmpty(sanitizedEmail) || !_inputSanitizationService.IsValidEmail(sanitizedEmail))
            {
                _logger.LogWarning("Invalid email format in registration attempt: {Email}", registerDto.Email);
                return null;
            }

            if (string.IsNullOrEmpty(sanitizedUsername) || !_inputSanitizationService.IsValidUsername(sanitizedUsername))
            {
                _logger.LogWarning("Invalid username format in registration attempt: {Username}", registerDto.Username);
                return null;
            }

            // Check for injection patterns
            if (_inputSanitizationService.ContainsSqlInjectionPatterns(registerDto.Email) ||
                _inputSanitizationService.ContainsSqlInjectionPatterns(registerDto.Username) ||
                _inputSanitizationService.ContainsSqlInjectionPatterns(registerDto.Password))
            {
                _logger.LogWarning("Potential injection attack detected in registration attempt for email: {Email}", sanitizedEmail);
                return null;
            }

            // Check for existing users
            if (await _context.Users.AnyAsync(u => u.Email == sanitizedEmail))
            {
                _logger.LogInformation("Registration attempt with existing email: {Email}", sanitizedEmail);
                return null;
            }

            if (await _context.Users.AnyAsync(u => u.Username == sanitizedUsername))
            {
                _logger.LogInformation("Registration attempt with existing username: {Username}", sanitizedUsername);
                return null;
            }

            var user = new User
            {
                Username = sanitizedUsername,
                Email = sanitizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user registered successfully: {UserId}, Username: {Username}", user.Id, sanitizedUsername);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration for email: {Email}", registerDto.Email);
            return null;
        }
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
    }

}