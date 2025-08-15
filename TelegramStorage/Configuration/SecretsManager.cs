using System.Security.Cryptography;

namespace TelegramStorage.Configuration;

public static class SecretsManager
{
    /// <summary>
    /// Gets a secret value from environment variables with fallback to configuration
    /// </summary>
    public static string GetSecret(string environmentVariableName, string? configurationKey = null, IConfiguration? configuration = null)
    {
        // First try environment variable
        var envValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        // If not found and configuration key provided, try configuration
        if (!string.IsNullOrWhiteSpace(configurationKey) && configuration != null)
        {
            var configValue = configuration[configurationKey];
            if (!string.IsNullOrWhiteSpace(configValue))
            {
                return configValue;
            }
        }

        throw new InvalidOperationException($"Required secret '{environmentVariableName}' not found in environment variables or configuration.");
    }

    /// <summary>
    /// Validates that required secrets are present
    /// </summary>
    public static void ValidateRequiredSecrets(ILogger logger)
    {
        var requiredSecrets = new[]
        {
            "JWT_SECRET_KEY",
            "TELEGRAM_BOT_TOKEN", 
            "DATABASE_CONNECTION_STRING"
        };

        var missingSecrets = new List<string>();

        foreach (var secret in requiredSecrets)
        {
            var value = Environment.GetEnvironmentVariable(secret);
            if (string.IsNullOrWhiteSpace(value))
            {
                missingSecrets.Add(secret);
            }
        }

        if (missingSecrets.Any())
        {
            logger.LogCritical("Missing required environment variables: {MissingSecrets}. " +
                "Application may fall back to less secure configuration values.", 
                string.Join(", ", missingSecrets));
        }
        else
        {
            logger.LogInformation("All required environment variables are present.");
        }
    }

    /// <summary>
    /// Generates a cryptographically secure secret key
    /// </summary>
    public static string GenerateSecretKey(int lengthInBytes = 32)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[lengthInBytes];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Safely logs configuration status without exposing secrets
    /// </summary>
    public static void LogConfigurationStatus(ILogger logger, IConfiguration configuration)
    {
        var configSources = new Dictionary<string, bool>
        {
            ["JWT_SECRET_KEY"] = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_SECRET_KEY")),
            ["TELEGRAM_BOT_TOKEN"] = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")),
            ["DATABASE_CONNECTION_STRING"] = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")),
            ["TELEGRAM_STORAGE_CHAT_ID"] = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TELEGRAM_STORAGE_CHAT_ID"))
        };

        foreach (var (key, isPresent) in configSources)
        {
            if (isPresent)
            {
                logger.LogInformation("Environment variable {Key} is configured", key);
            }
            else
            {
                var configKey = key switch
                {
                    "JWT_SECRET_KEY" => "JwtSettings:SecretKey",
                    "TELEGRAM_BOT_TOKEN" => "TelegramSettings:BotToken",
                    "DATABASE_CONNECTION_STRING" => "ConnectionStrings:DefaultConnection",
                    "TELEGRAM_STORAGE_CHAT_ID" => "TelegramSettings:StorageChatId",
                    _ => null
                };

                var hasConfigFallback = configKey != null && !string.IsNullOrEmpty(configuration[configKey]);
                
                if (hasConfigFallback)
                {
                    logger.LogWarning("Environment variable {Key} not found, using configuration fallback (less secure)", key);
                }
                else
                {
                    logger.LogError("Required secret {Key} not found in environment or configuration", key);
                }
            }
        }
    }
}