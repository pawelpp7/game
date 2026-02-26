using System.Security.Claims;
using System.Text.Json;
using BoardGameApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly GameService gameService;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GameController(GameService gameService)
    {
        this.gameService = gameService;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("create")]
    public async Task<IActionResult> Create()
    {
        var game = await gameService.CreateGameAsync(UserId);
        return Ok(new { game.Id, game.Status });
    }

    [HttpPost("{gameId}/join")]
    public async Task<IActionResult> Join(int gameId)
    {
        var game = await gameService.JoinGameAsync(gameId, UserId);
        if (game is null) return BadRequest(new { error = "Nie można dołączyć do tej gry" });
        return Ok(new { game.Id, game.Status });
    }

    [HttpGet("{gameId}")]
    public async Task<IActionResult> GetGame(int gameId)
    {
        var game = await gameService.GetGameAsync(gameId);
        if (game is null) return NotFound();

        // boardState musi być STRINGIEM JSON — frontend robi JSON.parse() na nim
        var boardStateJson = JsonSerializer.Serialize(game.BoardState, JsonOpts);

        return Ok(new
        {
            game.Id,
            game.Player1Id,
            game.Player2Id,
            game.Status,       // 0=Waiting, 1=InProgress, 2=Finished
            game.WinnerId,
            game.CurrentTurnPlayerId,
            game.CreatedAt,
            boardState = boardStateJson  // string JSON, nie obiekt
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var history = await gameService.GetHistoryAsync(UserId);
        return Ok(history);
    }
}