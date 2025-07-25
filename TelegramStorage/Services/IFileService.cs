using TelegramStorage.DTOs;
using TelegramStorage.Models;

namespace TelegramStorage.Services;

public interface IFileService
{
    Task<FileResponseDto?> UploadFileAsync(IFormFile file, int userId);
    Task<(Stream? fileStream, string fileName, string contentType)?> DownloadFileAsync(int fileId, int userId);
    Task<bool> DeleteFileAsync(int fileId, int userId);
    Task<IEnumerable<FileResponseDto>> GetUserFilesAsync(int userId);
    Task<FileResponseDto?> GetFileByIdAsync(int fileId, int userId);
}