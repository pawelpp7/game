using BoardGameApi.DTOs;
using BoardGameApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var result = await authService.RegisterAsync(dto);
        if (result is null) return Conflict(new { error = "Nazwa użytkownika zajęta" });
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result = await authService.LoginAsync(dto);
        if (result is null) return Unauthorized(new { error = "Błędne dane logowania" });
        return Ok(result);
    }
}