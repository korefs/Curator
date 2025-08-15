using TelegramStorage.Models;

namespace TelegramStorage.Services;

public interface IFileValidationService
{
    Task<FileValidationResult> ValidateFileAsync(IFormFile file, int userId);
    Task<bool> CheckUserQuotaAsync(int userId, long fileSize);
    Task<bool> IsFileExtensionAllowedAsync(string fileName);
    Task<bool> IsContentTypeAllowedAsync(string contentType);
    string SanitizeFileName(string fileName);
}

public class FileValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? SanitizedFileName { get; set; }
}