using System.Text.RegularExpressions;
using System.Web;

namespace TelegramStorage.Services;

public class InputSanitizationService : IInputSanitizationService
{
    private readonly ILogger<InputSanitizationService> _logger;
    
    // Common dangerous patterns
    private static readonly string[] SqlInjectionPatterns = {
        @"(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE)?|INSERT( +INTO)?|MERGE|SELECT|UPDATE|UNION( +ALL)?)\b)",
        @"(\b(AND|OR)\b.{1,6}?(=|>|<|\!=|<>|<=|>=))",
        @"(\bDATABASE\b)",
        @"(\bTABLE\b)",
        @"(\bCOLUMN\b)",
        @"(-{2}|\#|\/\*|\*\/)",
        @"(\bXP_\w+)",
        @"(\bSP_\w+)",
        @"(\b0x[0-9A-Fa-f]+)"
    };

    private static readonly string[] XssPatterns = {
        @"<script[^>]*>.*?</script>",
        @"javascript:",
        @"vbscript:",
        @"onload\s*=",
        @"onerror\s*=",
        @"onclick\s*=",
        @"onmouseover\s*=",
        @"<iframe[^>]*>",
        @"<object[^>]*>",
        @"<embed[^>]*>",
        @"<link[^>]*>",
        @"<meta[^>]*>",
        @"expression\s*\(",
        @"url\s*\(",
        @"@import"
    };

    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UsernameRegex = new(
        @"^[a-zA-Z0-9_-]{3,50}$", 
        RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FileNameSanitizer = new(
        @"[<>:""/\\|?*\x00-\x1f]", 
        RegexOptions.Compiled);

    public InputSanitizationService(ILogger<InputSanitizationService> logger)
    {
        _logger = logger;
    }

    public string SanitizeString(string input, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Remove null characters and control characters
        input = Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        // Truncate to max length
        if (input.Length > maxLength)
        {
            input = input.Substring(0, maxLength);
            _logger.LogWarning("Input truncated to {MaxLength} characters", maxLength);
        }

        // Check for injection patterns
        if (ContainsSqlInjectionPatterns(input))
        {
            _logger.LogWarning("Potential SQL injection detected in input: {Input}", input.Length > 50 ? input.Substring(0, 50) + "..." : input);
        }

        if (ContainsXssPatterns(input))
        {
            _logger.LogWarning("Potential XSS detected in input: {Input}", input.Length > 50 ? input.Substring(0, 50) + "..." : input);
        }

        return input.Trim();
    }

    public string SanitizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        // Basic cleanup
        email = email.Trim().ToLowerInvariant();
        
        // Remove potential dangerous characters
        email = Regex.Replace(email, @"[^\w@.-]", "");
        
        // Validate format
        if (!IsValidEmail(email))
        {
            _logger.LogWarning("Invalid email format detected: {Email}", email);
            return string.Empty;
        }

        return email;
    }

    public string SanitizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return string.Empty;

        // Remove whitespace and convert to lowercase
        username = username.Trim().ToLowerInvariant();
        
        // Remove invalid characters (keep only alphanumeric, underscore, hyphen)
        username = Regex.Replace(username, @"[^a-z0-9_-]", "");
        
        // Validate format
        if (!IsValidUsername(username))
        {
            _logger.LogWarning("Invalid username format detected: {Username}", username);
            return string.Empty;
        }

        return username;
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        // Remove dangerous characters
        var sanitized = FileNameSanitizer.Replace(fileName, "_");
        
        // Replace multiple dots with single dot
        sanitized = Regex.Replace(sanitized, @"\.{2,}", ".");
        
        // Remove leading/trailing dots and spaces
        sanitized = sanitized.Trim('.', ' ');
        
        // Ensure filename is not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "file";
            
        // Ensure filename doesn't start with a dot (hidden file)
        if (sanitized.StartsWith("."))
            sanitized = "file" + sanitized;

        // Limit length
        if (sanitized.Length > 255)
        {
            var extension = Path.GetExtension(sanitized);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
            var maxNameLength = 255 - extension.Length;
            sanitized = nameWithoutExtension.Substring(0, Math.Max(1, maxNameLength)) + extension;
        }

        return sanitized;
    }

    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            // Additional validation using .NET's MailAddress
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email && EmailRegex.IsMatch(email);
        }
        catch
        {
            return false;
        }
    }

    public bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return UsernameRegex.IsMatch(username);
    }

    public bool ContainsSqlInjectionPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var upperInput = input.ToUpperInvariant();
        
        return SqlInjectionPatterns.Any(pattern => 
            Regex.IsMatch(upperInput, pattern, RegexOptions.IgnoreCase));
    }

    public bool ContainsXssPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var lowerInput = input.ToLowerInvariant();
        
        return XssPatterns.Any(pattern => 
            Regex.IsMatch(lowerInput, pattern, RegexOptions.IgnoreCase));
    }

    public string RemoveHtmlTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return HtmlTagRegex.Replace(input, "");
    }

    public string EscapeSpecialCharacters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return HttpUtility.HtmlEncode(input);
    }
}