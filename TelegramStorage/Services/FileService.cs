using Microsoft.EntityFrameworkCore;
using TelegramStorage.Data;
using TelegramStorage.DTOs;
using TelegramStorage.Models;

namespace TelegramStorage.Services;

public class FileService : IFileService
{
    private readonly TelegramStorageContext _context;
    private readonly ITelegramService _telegramService;
    private readonly IFileValidationService _fileValidationService;
    private readonly ILogger<FileService> _logger;

    public FileService(
        TelegramStorageContext context, 
        ITelegramService telegramService,
        IFileValidationService fileValidationService,
        ILogger<FileService> logger)
    {
        _context = context;
        _telegramService = telegramService;
        _fileValidationService = fileValidationService;
        _logger = logger;
    }

    public async Task<FileResponseDto?> UploadFileAsync(IFormFile file, int userId)
    {
        try
        {
            // Validate file first
            var validationResult = await _fileValidationService.ValidateFileAsync(file, userId);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("File validation failed for user {UserId}: {Errors}", 
                    userId, string.Join(", ", validationResult.Errors));
                return null;
            }

            using var stream = file.OpenReadStream();
            
            // Use sanitized filename
            var sanitizedFileName = validationResult.SanitizedFileName ?? file.FileName;
            
            // Verifica se precisa fazer chunking
            if (FileChunkingService.ShouldChunk(file.Length))
            {
                return await UploadLargeFileAsync(file, stream, userId, sanitizedFileName);
            }
            else
            {
                return await UploadSmallFileAsync(file, stream, userId, sanitizedFileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            return null;
        }
    }

    private async Task<FileResponseDto?> UploadSmallFileAsync(IFormFile file, Stream stream, int userId, string sanitizedFileName)
    {
        var telegramFileId = await _telegramService.UploadFileAsync(
            stream, sanitizedFileName, file.ContentType);

        if (telegramFileId == null)
        {
            _logger.LogError("Failed to upload file to Telegram: {FileName}", file.FileName);
            return null;
        }

        var fileRecord = new FileRecord
        {
            OriginalFileName = sanitizedFileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            TelegramFileId = telegramFileId,
            UserId = userId,
            UploadedAt = DateTime.UtcNow,
            IsChunked = false,
            TotalChunks = 1
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

    private async Task<FileResponseDto?> UploadLargeFileAsync(IFormFile file, Stream stream, int userId, string sanitizedFileName)
    {
        var totalChunks = FileChunkingService.CalculateChunkCount(file.Length);
        
        // Cria o registro do arquivo
        var fileRecord = new FileRecord
        {
            OriginalFileName = sanitizedFileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            TelegramFileId = string.Empty, // Será preenchido depois
            UserId = userId,
            UploadedAt = DateTime.UtcNow,
            IsChunked = true,
            TotalChunks = totalChunks
        };

        _context.FileRecords.Add(fileRecord);
        await _context.SaveChangesAsync();

        var chunks = new List<FileChunk>();
        
        try
        {
            // Upload dos chunks
            await foreach (var (index, chunkData) in FileChunkingService.SplitStreamAsync(stream))
            {
                var chunkFileId = await _telegramService.UploadChunkAsync(
                    chunkData, file.FileName, index);

                if (chunkFileId == null)
                {
                    _logger.LogError("Failed to upload chunk {Index} for file: {FileName}", 
                        index, file.FileName);
                    return null;
                }

                var chunk = new FileChunk
                {
                    FileRecordId = fileRecord.Id,
                    ChunkIndex = index,
                    TelegramFileId = chunkFileId,
                    ChunkSize = chunkData.Length,
                    UploadedAt = DateTime.UtcNow
                };

                chunks.Add(chunk);
                _context.FileChunks.Add(chunk);
            }

            // Usa o FileId do primeiro chunk como referência principal
            fileRecord.TelegramFileId = chunks.First().TelegramFileId;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Large file uploaded successfully: {FileName}, Id: {Id}, Chunks: {ChunkCount}", 
                file.FileName, fileRecord.Id, chunks.Count);

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
            _logger.LogError(ex, "Error uploading large file chunks: {FileName}", file.FileName);
            
            // Cleanup em caso de erro
            _context.FileRecords.Remove(fileRecord);
            await _context.SaveChangesAsync();
            
            return null;
        }
    }

    public async Task<(Stream? fileStream, string fileName, string contentType)?> DownloadFileAsync(int fileId, int userId)
    {
        try
        {
            var fileRecord = await _context.FileRecords
                .Include(f => f.Chunks)
                .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

            if (fileRecord == null)
            {
                _logger.LogWarning("File not found or access denied: {FileId}, User: {UserId}", fileId, userId);
                return null;
            }

            if (!fileRecord.IsChunked)
            {
                // Arquivo pequeno - download direto
                var stream = await _telegramService.DownloadFileAsync(fileRecord.TelegramFileId);
                if (stream == null)
                {
                    _logger.LogError("Failed to download file from Telegram: {FileId}", fileId);
                    return null;
                }
                return (stream, fileRecord.OriginalFileName, fileRecord.ContentType);
            }
            else
            {
                // Arquivo grande - download e reassembly dos chunks
                return await DownloadLargeFileAsync(fileRecord);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file: {FileId}", fileId);
            return null;
        }
    }

    private async Task<(Stream? fileStream, string fileName, string contentType)?> DownloadLargeFileAsync(FileRecord fileRecord)
    {
        try
        {
            var orderedChunks = fileRecord.Chunks.OrderBy(c => c.ChunkIndex).ToList();
            
            if (orderedChunks.Count != fileRecord.TotalChunks)
            {
                _logger.LogError("Missing chunks for file: {FileId}. Expected: {Expected}, Found: {Found}", 
                    fileRecord.Id, fileRecord.TotalChunks, orderedChunks.Count);
                return null;
            }

            var chunkStreams = new List<Stream>();
            
            try
            {
                // Download de todos os chunks
                foreach (var chunk in orderedChunks)
                {
                    var chunkStream = await _telegramService.DownloadFileAsync(chunk.TelegramFileId);
                    if (chunkStream == null)
                    {
                        _logger.LogError("Failed to download chunk {Index} for file: {FileId}", 
                            chunk.ChunkIndex, fileRecord.Id);
                        
                        // Cleanup streams já baixados
                        foreach (var s in chunkStreams)
                            s.Dispose();
                        
                        return null;
                    }
                    chunkStreams.Add(chunkStream);
                }

                // Reassemble dos chunks
                var reassembledStream = await FileChunkingService.ReassembleChunksAsync(chunkStreams);
                
                _logger.LogInformation("Large file reassembled successfully: {FileId}, Chunks: {ChunkCount}", 
                    fileRecord.Id, chunkStreams.Count);

                return (reassembledStream, fileRecord.OriginalFileName, fileRecord.ContentType);
            }
            finally
            {
                // Cleanup dos chunk streams
                foreach (var stream in chunkStreams)
                    stream.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading large file: {FileId}", fileRecord.Id);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(int fileId, int userId)
    {
        try
        {
            var fileRecord = await _context.FileRecords
                .Include(f => f.Chunks)
                .FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId && !f.IsDeleted);

            if (fileRecord == null)
            {
                _logger.LogWarning("File not found or access denied: {FileId}, User: {UserId}", fileId, userId);
                return false;
            }

            fileRecord.IsDeleted = true;
            await _context.SaveChangesAsync();

            // Delete do arquivo principal
            await _telegramService.DeleteFileAsync(fileRecord.TelegramFileId, fileRecord.TelegramMessageId);

            // Delete dos chunks se existirem
            if (fileRecord.IsChunked && fileRecord.Chunks.Any())
            {
                foreach (var chunk in fileRecord.Chunks)
                {
                    await _telegramService.DeleteFileAsync(chunk.TelegramFileId, chunk.TelegramMessageId);
                }
                _logger.LogInformation("File and {ChunkCount} chunks deleted successfully: {FileId}", 
                    fileRecord.Chunks.Count, fileId);
            }
            else
            {
                _logger.LogInformation("File deleted successfully: {FileId}", fileId);
            }
            
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