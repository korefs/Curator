using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TelegramStorage.Services;

namespace TelegramStorage.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(long.MaxValue)]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file provided" });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _fileService.UploadFileAsync(file, userId.Value);
        if (result == null)
        {
            return BadRequest(new { message = "Failed to upload file" });
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserFiles()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var files = await _fileService.GetUserFilesAsync(userId.Value);
        return Ok(files);
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> GetFile(int fileId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var file = await _fileService.GetFileByIdAsync(fileId, userId.Value);
        if (file == null)
        {
            return NotFound(new { message = "File not found" });
        }

        return Ok(file);
    }

    [HttpGet("{fileId}/download")]
    public async Task<IActionResult> DownloadFile(int fileId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _fileService.DownloadFileAsync(fileId, userId.Value);
        if (result == null)
        {
            return NotFound(new { message = "File not found" });
        }

        var (fileStream, fileName, contentType) = result.Value;
        return File(fileStream!, contentType, fileName);
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(int fileId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var success = await _fileService.DeleteFileAsync(fileId, userId.Value);
        if (!success)
        {
            return NotFound(new { message = "File not found or failed to delete" });
        }

        return Ok(new { message = "File deleted successfully" });
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}