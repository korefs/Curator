using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TelegramStorage.Configuration;
using TelegramStorage.Models;

namespace TelegramStorage.Services;

public class JwtService : IJwtService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<JwtService> _logger;
    private readonly IConfiguration _configuration;
    
    public JwtService(
        JwtSettings jwtSettings, 
        ILogger<JwtService> logger,
        IConfiguration configuration)
    {
        _jwtSettings = jwtSettings;
        _logger = logger;
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var securityKey = GetSecurityKey();
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("jti", Guid.NewGuid().ToString()), // JWT ID for revocation
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwtSettings.ExpirationInHours),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        
        _logger.LogInformation("JWT token generated for user {UserId}", user.Id);
        return tokenString;
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityKey = GetSecurityKey();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };

            tokenHandler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Token validation failed: {Error}", ex.Message);
            return false;
        }
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<string> GetOrCreateSecretKeyAsync()
    {
        // Try to get from environment variable first
        var envKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        if (!string.IsNullOrEmpty(envKey) && envKey.Length >= 32)
        {
            return envKey;
        }

        // Try to get from configuration
        var configKey = _configuration["JwtSettings:SecretKey"];
        if (!string.IsNullOrEmpty(configKey) && configKey.Length >= 32)
        {
            // Log warning about using config instead of environment variable
            _logger.LogWarning("Using JWT secret from configuration. Consider using JWT_SECRET_KEY environment variable for better security.");
            return configKey;
        }

        // Generate a new secure key if none exists
        _logger.LogWarning("No secure JWT secret found. Generating a new one. This should be moved to environment variables in production.");
        return GenerateSecureKey();
    }

    private SymmetricSecurityKey GetSecurityKey()
    {
        var key = GetOrCreateSecretKeyAsync().Result;
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    private string GenerateSecureKey()
    {
        // Generate a cryptographically secure 256-bit (32 byte) key
        var keyBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(keyBytes);
        
        // Convert to base64 for easy storage
        var key = Convert.ToBase64String(keyBytes);
        
        _logger.LogWarning("Generated new JWT secret key. Store this securely: {Key}", key);
        return key;
    }
}