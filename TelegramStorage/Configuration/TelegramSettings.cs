namespace TelegramStorage.Configuration;

public class TelegramSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string StorageChatId { get; set; } = string.Empty;
    public int MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB
    public string[] AllowedContentTypes { get; set; } = Array.Empty<string>();
}