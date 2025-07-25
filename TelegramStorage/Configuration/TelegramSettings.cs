namespace TelegramStorage.Configuration;

public class TelegramSettings
{
    public string BotToken { get; set; } = string.Empty;
    public string StorageChatId { get; set; } = string.Empty;
    public int MaxChunkSizeBytes { get; set; } = 40 * 1024 * 1024; // 40MB por chunk
    public string[] AllowedContentTypes { get; set; } = Array.Empty<string>();
}