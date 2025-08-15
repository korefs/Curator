namespace TelegramStorage.Services;

public interface IInputSanitizationService
{
    string SanitizeString(string input, int maxLength = 1000);
    string SanitizeEmail(string email);
    string SanitizeUsername(string username);
    string SanitizeFileName(string fileName);
    bool IsValidEmail(string email);
    bool IsValidUsername(string username);
    bool ContainsSqlInjectionPatterns(string input);
    bool ContainsXssPatterns(string input);
    string RemoveHtmlTags(string input);
    string EscapeSpecialCharacters(string input);
}