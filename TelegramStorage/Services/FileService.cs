using Microsoft.EntityFrameworkCore;
using TelegramStorage.Data;
using TelegramStorage.DTOs;
using TelegramStorage.Models;

namespace TelegramStorage.Services;

public class FileService : IFileService
{
    private readonly TelegramStorageContext _context;
    private readonly ITelegramService _telegramService;
    private readonly ILogger<FileService> _logger;

    public FileService(
        TelegramStorageContext context, 
        ITelegramService telegramService, 
        ILogger<FileService> logger)
    {
        _context = context;
        _telegramService = telegramService;
        _logger = logger;
    }

    public async Task<FileResponseDto?> UploadFileAsync(IFormFile file, int userId)
    {
        try
        {
            using var stream = file.OpenReadStream();
            var telegramFileId = await _telegramService.UploadFileAsync(
                stream, file.FileName, file.ContentType);

            if (telegramFileId == null)
            {
                _logger.LogError("Failed to upload file to Telegram: {FileName}", file.FileName);
                return null;
            }

            var fileRecord = new FileRecord
            {
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                TelegramFileId = telegramFileId,
                UserId = userId,
                UploadedAt = DateTime.UtcNow
            };

            _context.FileRecords.Add(fileRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation("File uploaded successfully: {FileName}, Id: {Id}", 
                file.FileName, fileRecord.Id);

            return new FileResponseDto
            {
                Id = fileRecord.Id,
                OriginalFileName = fileRecord.OriginalFileName,
                ContentType = fileRecord.ContentType,
                FileSize = fileRecord.FileSize,
                UploadedAt = fileRecord.UploadedAt,
                DownloadUrl = $"/api/files/{fileRecord.Id}/download"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            return null;
        }
    }

    public async Task<(Stream? fileStream, string fileName, string contentType)?> DownloadFileAsync(int fileId, int userId)
    {
        try
        {
            var fileRecord = await _context.FileRecords
                .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

            if (fileRecord == null)
            {
                _logger.LogWarning("File not found or access denied: {FileId}, User: {UserId}", fileId, userId);
                return null;
            }

            var stream = await _telegramService.DownloadFileAsync(fileRecord.TelegramFileId);
            if (stream == null)
            {
                _logger.LogError("Failed to download file from Telegram: {FileId}", fileId);
                return null;
            }

            return (stream, fileRecord.OriginalFileName, fileRecord.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file: {FileId}", fileId);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(int fileId, int userId)
    {
        try
        {
            var fileRecord = await _context.FileRecords
                .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

            if (fileRecord == null)
            {
                _logger.LogWarning("File not found or access denied: {FileId}, User: {UserId}", fileId, userId);
                return false;
            }

            fileRecord.IsDeleted = true;
            await _context.SaveChangesAsync();

            await _telegramService.DeleteFileAsync(fileRecord.TelegramFileId, fileRecord.TelegramMessageId);

            _logger.LogInformation("File deleted successfully: {FileId}", fileId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FileId}", fileId);
            return false;
        }
    }

    public async Task<IEnumerable<FileResponseDto>> GetUserFilesAsync(int userId)
    {
        var files = await _context.FileRecords
            .Where(f => f.UserId == userId && !f.IsDeleted)
            .OrderByDescending(f => f.UploadedAt)
            .Select(f => new FileResponseDto
            {
                Id = f.Id,
                OriginalFileName = f.OriginalFileName,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
                UploadedAt = f.UploadedAt,
                DownloadUrl = $"/api/files/{f.Id}/download"
            })
            .ToListAsync();

        return files;
    }

    public async Task<FileResponseDto?> GetFileByIdAsync(int fileId, int userId)
    {
        var fileRecord = await _context.FileRecords
            .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

        if (fileRecord == null)
        {
            return null;
        }

        return new FileResponseDto
        {
            Id = fileRecord.Id,
            OriginalFileName = fileRecord.OriginalFileName,
            ContentType = fileRecord.ContentType,
            FileSize = fileRecord.FileSize,
            UploadedAt = fileRecord.UploadedAt,
            DownloadUrl = $"/api/files/{fileRecord.Id}/download"
        };
    }
}