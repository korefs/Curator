using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramStorage.Configuration;

namespace TelegramStorage.Services;

public class TelegramService : ITelegramService
{
    private readonly TelegramBotClient _botClient;
    private readonly TelegramSettings _telegramSettings;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(TelegramSettings telegramSettings, ILogger<TelegramService> logger)
    {
        _telegramSettings = telegramSettings;
        _logger = logger;
        _botClient = new TelegramBotClient(_telegramSettings.BotToken);
    }

    public async Task<string?> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            if (fileStream.Length > _telegramSettings.MaxFileSizeBytes)
            {
                _logger.LogWarning("File size exceeds limit: {Size} bytes", fileStream.Length);
                return null;
            }

            if (_telegramSettings.AllowedContentTypes.Length > 0 && 
                !_telegramSettings.AllowedContentTypes.Contains(contentType))
            {
                _logger.LogWarning("Content type not allowed: {ContentType}", contentType);
                return null;
            }

            var inputFile = InputFile.FromStream(fileStream, fileName);
            var message = await _botClient.SendDocumentAsync(
                chatId: _telegramSettings.StorageChatId,
                document: inputFile,
                caption: $"📁 {fileName}\n🕒 {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
            );

            if (message.Document != null)
            {
                _logger.LogInformation("File uploaded successfully: {FileName}, FileId: {FileId}", 
                    fileName, message.Document.FileId);
                return message.Document.FileId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Telegram: {FileName}", fileName);
            return null;
        }
    }

    public async Task<Stream?> DownloadFileAsync(string telegramFileId)
    {
        try
        {
            var file = await _botClient.GetFileAsync(telegramFileId);
            
            if (file.FilePath == null)
            {
                _logger.LogWarning("File path is null for FileId: {FileId}", telegramFileId);
                return null;
            }

            var memoryStream = new MemoryStream();
            await _botClient.DownloadFileAsync(file.FilePath, memoryStream);
            memoryStream.Position = 0;

            _logger.LogInformation("File downloaded successfully: {FileId}", telegramFileId);
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from Telegram: {FileId}", telegramFileId);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(string telegramFileId, string? messageId)
    {
        try
        {
            if (int.TryParse(messageId, out var msgId))
            {
                await _botClient.DeleteMessageAsync(_telegramSettings.StorageChatId, msgId);
                _logger.LogInformation("File deleted successfully: {FileId}, MessageId: {MessageId}", 
                    telegramFileId, messageId);
                return true;
            }

            _logger.LogWarning("Invalid message ID for deletion: {MessageId}", messageId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from Telegram: {FileId}", telegramFileId);
            return false;
        }
    }

    public async Task<FileInfo?> GetFileInfoAsync(string telegramFileId)
    {
        try
        {
            var file = await _botClient.GetFileAsync(telegramFileId);
            
            if (file.FilePath == null)
            {
                return null;
            }

            return new FileInfo
            {
                FileId = file.FileId,
                FilePath = file.FilePath,
                FileSize = file.FileSize ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info from Telegram: {FileId}", telegramFileId);
            return null;
        }
    }

    public class FileInfo
    {
        public string FileId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}