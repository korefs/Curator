namespace TelegramStorage.Configuration;

public class SecuritySettings
{
    public FileUploadSettings FileUpload { get; set; } = new();
    public RateLimitSettings RateLimit { get; set; } = new();
    public SecurityHeaders Headers { get; set; } = new();
}

public class FileUploadSettings
{
    /// <summary>
    /// Maximum file size in bytes (default: 100MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    
    /// <summary>
    /// Maximum total upload size per user per day in bytes (default: 1GB)
    /// </summary>
    public long MaxDailyUploadSizeBytes { get; set; } = 1024 * 1024 * 1024; // 1GB
    
    /// <summary>
    /// Maximum number of files per user (default: 1000)
    /// </summary>
    public int MaxFilesPerUser { get; set; } = 1000;
    
    /// <summary>
    /// Allowed file extensions (case-insensitive)
    /// </summary>
    public string[] AllowedExtensions { get; set; } = 
    {
        ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".txt", ".zip", 
        ".mp4", ".avi", ".mov", ".doc", ".docx", ".xls", ".xlsx"
    };
    
    /// <summary>
    /// Blocked file extensions for security (case-insensitive)
    /// </summary>
    public string[] BlockedExtensions { get; set; } = 
    {
        ".exe", ".bat", ".cmd", ".com", ".scr", ".pif", ".vbs", 
        ".js", ".jar", ".app", ".deb", ".pkg", ".dmg", ".sh"
    };
    
    /// <summary>
    /// Maximum filename length
    /// </summary>
    public int MaxFileNameLength { get; set; } = 255;
    
    /// <summary>
    /// Whether to scan files for viruses (requires antivirus integration)
    /// </summary>
    public bool EnableVirusScanning { get; set; } = false;
}

public class RateLimitSettings
{
    /// <summary>
    /// Maximum requests per minute per IP
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;
    
    /// <summary>
    /// Maximum upload requests per minute per user
    /// </summary>
    public int UploadRequestsPerMinute { get; set; } = 10;
    
    /// <summary>
    /// Maximum authentication attempts per IP per hour
    /// </summary>
    public int AuthAttemptsPerHour { get; set; } = 5;
}

public class SecurityHeaders
{
    public bool EnableHsts { get; set; } = true;
    public bool EnableXFrameOptions { get; set; } = true;
    public bool EnableXContentTypeOptions { get; set; } = true;
    public bool EnableReferrerPolicy { get; set; } = true;
    public bool EnableCsp { get; set; } = true;
    public string CspPolicy { get; set; } = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';";
}