using System.ComponentModel.DataAnnotations;

namespace TelegramStorage.Models;

public class FileRecord
{
    public int Id { get; set; }
    
    [Required]
    public string OriginalFileName { get; set; } = string.Empty;
    
    [Required]
    public string ContentType { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    [Required]
    public string TelegramFileId { get; set; } = string.Empty;
    
    public string? TelegramMessageId { get; set; }
    
    public string? TelegramChatId { get; set; }
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsDeleted { get; set; } = false;
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public bool IsChunked { get; set; } = false;
    public int TotalChunks { get; set; } = 1;
    
    public ICollection<FileChunk> Chunks { get; set; } = new List<FileChunk>();
}