using System.ComponentModel.DataAnnotations;

namespace TelegramStorage.Models;

public class FileChunk
{
    public int Id { get; set; }
    
    public int FileRecordId { get; set; }
    public FileRecord FileRecord { get; set; } = null!;
    
    public int ChunkIndex { get; set; }
    
    [Required]
    public string TelegramFileId { get; set; } = string.Empty;
    
    public string? TelegramMessageId { get; set; }
    
    public long ChunkSize { get; set; }
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}