using Microsoft.AspNetCore.Mvc;
using TelegramStorage.DTOs;
using TelegramStorage.Services;

namespace TelegramStorage.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var token = await _authService.LoginAsync(loginDto);
        if (token == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        return Ok(new { token, message = "Login successful" });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _authService.RegisterAsync(registerDto);
        if (user == null)
        {
            return BadRequest(new { message = "User already exists or invalid data" });
        }

        return Ok(new { message = "Registration successful", userId = user.Id });
    }
}