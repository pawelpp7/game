using BoardGameApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BoardGameApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Move> Moves => Set<Move>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.Player1)
            .WithMany(u => u.GamesAsPlayer1)
            .HasForeignKey(g => g.Player1Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.Player2)
            .WithMany(u => u.GamesAsPlayer2)
            .HasForeignKey(g => g.Player2Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Move>()
            .HasOne(m => m.Player)
            .WithMany(u => u.Moves)
            .HasForeignKey(m => m.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .Property(g => g.BoardState)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                v => JsonSerializer.Deserialize<BoardState>(v, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!
            );
    }
}