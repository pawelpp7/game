namespace BoardGameApi.Models;

public enum GameStatus { Waiting, InProgress, Finished }

public class Game
{
    public int Id { get; set; }
    public int Player1Id { get; set; }
    public int? Player2Id { get; set; }
    public int? WinnerId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public int CurrentTurnPlayerId { get; set; }
    public string BoardState { get; set; } = string.Empty; // JSON planszy
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public User Player1 { get; set; } = null!;
    public User? Player2 { get; set; }
    public ICollection<Move> Moves { get; set; } = [];
}