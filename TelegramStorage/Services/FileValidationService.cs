using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TelegramStorage.Configuration;
using TelegramStorage.Data;

namespace TelegramStorage.Services;

public class FileValidationService : IFileValidationService
{
    private readonly SecuritySettings _securitySettings;
    private readonly TelegramStorageContext _context;
    private readonly ILogger<FileValidationService> _logger;
    
    // Regex for dangerous characters in filenames
    private static readonly Regex FileNameSanitizer = new(@"[<>:""/\\|?*\x00-\x1f]", RegexOptions.Compiled);
    private static readonly Regex MultipleDotsPattern = new(@"\.{2,}", RegexOptions.Compiled);
    
    public FileValidationService(
        SecuritySettings securitySettings,
        TelegramStorageContext context,
        ILogger<FileValidationService> logger)
    {
        _securitySettings = securitySettings;
        _context = context;
        _logger = logger;
    }

    public async Task<FileValidationResult> ValidateFileAsync(IFormFile file, int userId)
    {
        var result = new FileValidationResult();
        var errors = new List<string>();

        // Basic null/empty checks
        if (file == null)
        {
            errors.Add("File is required");
            result.IsValid = false;
            result.Errors = errors;
            return result;
        }

        if (file.Length == 0)
        {
            errors.Add("File cannot be empty");
        }

        // File size validation
        if (file.Length > _securitySettings.FileUpload.MaxFileSizeBytes)
        {
            errors.Add($"File size exceeds maximum allowed size of {_securitySettings.FileUpload.MaxFileSizeBytes / (1024 * 1024)}MB");
        }

        // Filename validation
        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            errors.Add("Filename is required");
        }
        else
        {
            if (file.FileName.Length > _securitySettings.FileUpload.MaxFileNameLength)
            {
                errors.Add($"Filename exceeds maximum length of {_securitySettings.FileUpload.MaxFileNameLength} characters");
            }

            // Sanitize filename
            var sanitizedFileName = SanitizeFileName(file.FileName);
            result.SanitizedFileName = sanitizedFileName;

            // Extension validation
            if (!await IsFileExtensionAllowedAsync(sanitizedFileName))
            {
                errors.Add("File extension is not allowed");
            }
        }

        // Content type validation
        if (!await IsContentTypeAllowedAsync(file.ContentType))
        {
            errors.Add("File content type is not allowed");
        }

        // User quota validation
        if (!await CheckUserQuotaAsync(userId, file.Length))
        {
            errors.Add("Upload would exceed user quota limits");
        }

        // Check for file count limit
        var userFileCount = await _context.FileRecords
            .CountAsync(f => f.UserId == userId && !f.IsDeleted);
            
        if (userFileCount >= _securitySettings.FileUpload.MaxFilesPerUser)
        {
            errors.Add($"Maximum number of files ({_securitySettings.FileUpload.MaxFilesPerUser}) reached");
        }

        result.IsValid = !errors.Any();
        result.Errors = errors;

        if (!result.IsValid)
        {
            _logger.LogWarning("File validation failed for user {UserId}: {Errors}", 
                userId, string.Join(", ", errors));
        }

        return result;
    }

    public async Task<bool> CheckUserQuotaAsync(int userId, long fileSize)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var dailyUploadSize = await _context.FileRecords
            .Where(f => f.UserId == userId && 
                       f.UploadedAt >= today && 
                       f.UploadedAt < tomorrow &&
                       !f.IsDeleted)
            .SumAsync(f => f.FileSize);

        return (dailyUploadSize + fileSize) <= _securitySettings.FileUpload.MaxDailyUploadSizeBytes;
    }

    public async Task<bool> IsFileExtensionAllowedAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        // Check blocked extensions first
        if (_securitySettings.FileUpload.BlockedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Blocked file extension attempted: {Extension}", extension);
            return false;
        }

        // Check allowed extensions
        return _securitySettings.FileUpload.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsContentTypeAllowedAsync(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        // Get allowed content types from TelegramSettings for backward compatibility
        // In the future, this should be moved to SecuritySettings
        var telegramSettings = await _context.Database.SqlQueryRaw<string>(
            "SELECT unnest(string_to_array($1, ',')) as content_type", 
            string.Join(",", new[] { "image/jpeg", "image/png", "image/gif", "application/pdf", 
                                   "text/plain", "application/zip", "video/*", "application/octet-stream" }))
            .ToListAsync();

        // Handle wildcard content types
        if (contentType.StartsWith("video/") && telegramSettings.Contains("video/*"))
            return true;

        return telegramSettings.Contains(contentType, StringComparer.OrdinalIgnoreCase);
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        // Remove dangerous characters
        var sanitized = FileNameSanitizer.Replace(fileName, "_");
        
        // Replace multiple dots with single dot
        sanitized = MultipleDotsPattern.Replace(sanitized, ".");
        
        // Remove leading/trailing dots and spaces
        sanitized = sanitized.Trim('.', ' ');
        
        // Ensure filename is not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "file";
            
        // Ensure filename doesn't start with a dot (hidden file)
        if (sanitized.StartsWith("."))
            sanitized = "file" + sanitized;
            
        // Limit length
        if (sanitized.Length > _securitySettings.FileUpload.MaxFileNameLength)
        {
            var extension = Path.GetExtension(sanitized);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
            var maxNameLength = _securitySettings.FileUpload.MaxFileNameLength - extension.Length;
            sanitized = nameWithoutExtension.Substring(0, Math.Max(1, maxNameLength)) + extension;
        }

        return sanitized;
    }
}