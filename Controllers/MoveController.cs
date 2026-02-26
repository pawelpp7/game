using System.Security.Claims;
using BoardGameApi.DTOs;
using BoardGameApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameApi.Controllers;

[ApiController]
[Route("api/game/{gameId}/turn")]
[Authorize]
public class MoveController(GameService gameService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> ExecuteTurn(int gameId, TurnDto dto)
    {
        var (success, message, game) = await gameService.ExecuteTurnAsync(gameId, UserId, dto);
        if (!success) return BadRequest(new { error = message });
        return Ok(new
        {
            message,
            board = game!.BoardState,
            status = game.Status,
            winner = game.WinnerId
        });
    }
}