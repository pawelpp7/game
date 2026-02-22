using System.Security.Claims;
using BoardGameApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameController(GameService gameService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("create")]
    public async Task<IActionResult> Create()
    {
        var game = await gameService.CreateGameAsync(UserId);
        return Ok(game);
    }

    [HttpPost("{gameId}/join")]
    public async Task<IActionResult> Join(int gameId)
    {
        var game = await gameService.JoinGameAsync(gameId, UserId);
        if (game is null) return BadRequest("Nie można dołączyć do tej gry");
        return Ok(game);
    }

    [HttpGet("{gameId}")]
    public async Task<IActionResult> GetGame(int gameId)
    {
        var game = await gameService.GetGameAsync(gameId);
        if (game is null) return NotFound();
        return Ok(game);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var games = await gameService.GetHistoryAsync(UserId);
        return Ok(games);
    }
}