namespace BoardGameApi.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Game> GamesAsPlayer1 { get; set; } = [];
    public ICollection<Game> GamesAsPlayer2 { get; set; } = [];
    public ICollection<Move> Moves { get; set; } = [];
}