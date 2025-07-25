using Telegram.Bot.Types;

namespace TelegramStorage.Services;

public interface ITelegramService
{
    Task<string?> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<string?> UploadChunkAsync(byte[] chunkData, string fileName, int chunkIndex);
    Task<Stream?> DownloadFileAsync(string telegramFileId);
    Task<bool> DeleteFileAsync(string telegramFileId, string? messageId);
    Task<TelegramService.FileInfo?> GetFileInfoAsync(string telegramFileId);
}