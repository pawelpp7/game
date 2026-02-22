using BoardGameApi.DTOs;

namespace BoardGameApi.Models;

public class Move
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int PlayerId { get; set; }
    public MoveType Type { get; set; }
    public int PieceId { get; set; }
    public int FromRow { get; set; }
    public int FromCol { get; set; }
    public int ToRow { get; set; }
    public int ToCol { get; set; }
    public DateTime MadeAt { get; set; } = DateTime.UtcNow;

    public Game Game { get; set; } = null!;
    public User Player { get; set; } = null!;
}