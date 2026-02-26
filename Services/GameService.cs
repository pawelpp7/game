using System.Text.Json;
using BoardGameApi.Data;
using BoardGameApi.DTOs;
using BoardGameApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardGameApi.Services;

public class GameService(AppDbContext db)
{
    // Plansza: 7 kolumn x 8 wierszy pól, narożniki 8x9
    private const int FieldCols = 7;
    private const int FieldRows = 8;
    private const int CornerCols = 8; // FieldCols + 1
    private const int CornerRows = 9; // FieldRows + 1

    private static BoardState CreateInitialBoard()
    {
        var board = new BoardState();

        int tId = 0, kId = 0, pId = 0;

        foreach (var (owner, fieldRow) in new[] {
            (PieceOwner.Player2, 0),
            (PieceOwner.Player1, 7)
        })
        {
            board.Towers.Add(new Tower { Id = tId++, Owner = owner, Row = fieldRow, Col = 0 });
            board.Towers.Add(new Tower { Id = tId++, Owner = owner, Row = fieldRow, Col = 2 });
            board.Towers.Add(new Tower { Id = tId++, Owner = owner, Row = fieldRow, Col = 4 });
            board.Towers.Add(new Tower { Id = tId++, Owner = owner, Row = fieldRow, Col = 6 });

            board.Kings.Add(new King { Id = kId++, Owner = owner, Row = fieldRow, Col = 1 });
            board.Kings.Add(new King { Id = kId++, Owner = owner, Row = fieldRow, Col = 5 });
        }

        foreach (var (owner, rows) in new[] {
            (PieceOwner.Player2, new[] { 1, 2 }),
            (PieceOwner.Player1, new[] { 6, 7 })
        })
        {
            foreach (var row in rows)
            {
                for (int col = 0; col < 8; col++)
                {
                    var pawn = new Pawn { Id = pId++, Owner = owner, Row = row, Col = col };
                    board.Pawns.Add(pawn);
                    // <-- Debugowanie
                    Console.WriteLine($"Pawn {pawn.Id} owner: {pawn.Owner}, row: {pawn.Row}, col: {pawn.Col}");
                }
            }
        }

        return board;
    }

public async Task<Game> CreateGameAsync(int player1Id)
{
    var board = CreateInitialBoard();
    var game = new Game
    {
        Player1Id = player1Id,
        CurrentTurnPlayerId = player1Id,
        BoardState = board 
    };

    db.Games.Add(game);
    await db.SaveChangesAsync();
    return game;
}

    public async Task<Game?> JoinGameAsync(int gameId, int player2Id)
    {
        var game = await db.Games.FindAsync(gameId);
        if (game is null || game.Status != GameStatus.Waiting || game.Player1Id == player2Id)
            return null;

        game.Player2Id = player2Id;
        game.Status = GameStatus.InProgress;
        await db.SaveChangesAsync();
        return game;
    }

 public async Task<(bool success, string message, Game? game)> ExecuteTurnAsync(
    int gameId, int playerId, TurnDto turn)
{
    var game = await db.Games.FindAsync(gameId);
    if (game is null) return (false, "Gra nie istnieje", null);
    if (game.Status != GameStatus.InProgress) return (false, "Gra nie jest aktywna", null);
    if (game.CurrentTurnPlayerId != playerId) return (false, "Nie twoja tura", null);

    var board = game.BoardState;
    var owner = playerId == game.Player1Id ? PieceOwner.Player1 : PieceOwner.Player2;
    var enemy = owner == PieceOwner.Player1 ? PieceOwner.Player2 : PieceOwner.Player1;

    // --- GŁÓWNY RUCH PIONKIEM ---
    var (success, message, bonusEarnedForPawnId) = ExecutePawnAction(board, owner, enemy, turn.PawnAction);
    if (!success) return (false, message, null);

    // --- BONUS RUCH ---
    if (turn.BonusPawnAction is not null)
    {
        // Musi być bonus (zbito sojusznika) i musi dotyczyć tego samego sojusznika
        if (bonusEarnedForPawnId is null)
            return (false, "Nie zdobyto bonusowego ruchu — nie zbito sojusznika", null);

        if (turn.BonusPawnAction.PieceId != bonusEarnedForPawnId)
            return (false, "Bonus ruch musi wykonać pionek który otrzymał bonus", null);

        var (bonusSuccess, bonusMessage, _) = ExecutePawnAction(board, owner, enemy, turn.BonusPawnAction);
        if (!bonusSuccess) return (false, $"Błąd bonusowego ruchu: {bonusMessage}", null);
    }
    else if (bonusEarnedForPawnId is not null)
    {
        // Gracz może zrezygnować z bonusu — to jest ok
    }

    // --- RUCH WIEŻĄ ---
    if (turn.TowerAction is not null)
    {
        var (towerSuccess, towerMessage) = ExecuteTowerAction(board, owner, enemy, turn.TowerAction, game);
        if (!towerSuccess) return (false, towerMessage, null);
    }

    // --- SPRAWDŹ ŚMIERĆ KRÓLA ---
    CheckKingDeath(board, enemy, game);

    // Zmiana tury
    game.CurrentTurnPlayerId = playerId == game.Player1Id
        ? game.Player2Id!.Value
        : game.Player1Id;

    // EF Core nie śledzi zmian wewnątrz obiektów z HasConversion automatycznie.
    // Wymuszamy re-serializację przez przypisanie nowej instancji.
    var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    game.BoardState = JsonSerializer.Deserialize<BoardState>(JsonSerializer.Serialize(board, opts), opts)!;
    db.Entry(game).Property(g => g.BoardState).IsModified = true;

    db.Moves.Add(new Move
    {
        GameId = gameId,
        PlayerId = playerId, 
        PieceId = turn.PawnAction.PieceId,
        FromRow = 0,
        FromCol = 0,
        ToRow = turn.PawnAction.ToRow,
        ToCol = turn.PawnAction.ToCol
    });

    await db.SaveChangesAsync();
    return (true, "Tura wykonana", game);
}

// Zmieniony zwracany typ — teraz zwraca id pionka który dostał bonus
private (bool success, string message, int? bonusEarnedForPawnId) ExecutePawnAction(
    BoardState board, PieceOwner owner, PieceOwner enemy, MoveDto action)
{
    var pawn = board.Pawns.FirstOrDefault(p =>
        p.Id == action.PieceId && p.Owner == owner && p.IsAlive);
    if (pawn is null) return (false, "Pionek nie istnieje", null);

    if (!IsValidCornerMove(pawn.Row, pawn.Col, action.ToRow, action.ToCol))
        return (false, "Niedozwolony ruch pionka", null);

    if (IsBlockedByTower(board, enemy, action.ToRow, action.ToCol))
        return (false, "Pole zablokowane przez wieżę wroga", null);

    var targetEnemy = board.Pawns.FirstOrDefault(p =>
        p.Row == action.ToRow && p.Col == action.ToCol &&
        p.Owner == enemy && p.IsAlive);

    var targetAlly = board.Pawns.FirstOrDefault(p =>
        p.Row == action.ToRow && p.Col == action.ToCol &&
        p.Owner == owner && p.IsAlive &&
        p.Id != pawn.Id);

    int? bonusPawnId = null;

    if (targetEnemy is not null)
    {
        // Zbicie wroga
        targetEnemy.IsAlive = false;
        targetEnemy.IsDead = true;
    }
    else if (targetAlly is not null)
    {
        // Bicie sojusznika — sojusznik dostaje bonus ruch, nie ginie
        bonusPawnId = targetAlly.Id;
    }

    pawn.Row = action.ToRow;
    pawn.Col = action.ToCol;

    return (true, "Ruch pionkiem wykonany", bonusPawnId);
}

    private (bool success, string message) ExecuteTowerAction(
        BoardState board, PieceOwner owner, PieceOwner enemy, MoveDto action, Game game)
    {
        if (action.Type == MoveType.Resurrect)
        {
            // Wskrzeszenie — pionek pojawia się przy wieży
            var deadPawn = board.Pawns.FirstOrDefault(p =>
                p.Id == action.PieceId && p.Owner == owner && p.IsDead);
            if (deadPawn is null) return (false, "Nie ma takiego zbitego pionka");

            // Znajdź dowolną żywą wieżę gracza
            var tower = board.Towers.FirstOrDefault(t => t.Owner == owner && t.IsAlive);
            if (tower is null) return (false, "Brak wieży do wskrzeszenia");

            // Umieść pionka na narożniku przy wieży (lewy górny narożnik pola wieży)
            var freeCorner = FindFreeCornerNearTower(board, tower);
            if (freeCorner is null) return (false, "Brak wolnego narożnika przy wieży");

            deadPawn.IsAlive = true;
            deadPawn.IsDead = false;
            deadPawn.Row = freeCorner.Value.row;
            deadPawn.Col = freeCorner.Value.col;
            return (true, "Pionek wskrzeszony");
        }
        else
        {
            // Normalny ruch wieżą
            var tower = board.Towers.FirstOrDefault(t => t.Id == action.PieceId && t.Owner == owner && t.IsAlive);
            if (tower is null) return (false, "Wieża nie istnieje");

            if (!IsValidTowerMove(board, owner, enemy, tower, action.ToRow, action.ToCol))
                return (false, "Niedozwolony ruch wieży");

            tower.Row = action.ToRow;
            tower.Col = action.ToCol;
            return (true, "Ruch wieżą wykonany");
        }
    }

    private static bool IsValidCornerMove(int fromRow, int fromCol, int toRow, int toCol)
    {
        int dRow = Math.Abs(toRow - fromRow);
        int dCol = Math.Abs(toCol - fromCol);
        // Narożnik do sąsiedniego narożnika: tylko jeden krok ortogonalnie
        return (dRow == 1 && dCol == 0) || (dRow == 0 && dCol == 1);
    }

    private static bool IsBlockedByTower(BoardState board, PieceOwner towerOwner, int cornerRow, int cornerCol)
    {
        // Narożnik (r,c) sąsiaduje z polami: (r-1,c-1),(r-1,c),(r,c-1),(r,c)
        // Wieża na polu blokuje narożniki wokół siebie
        var adjacentFields = new[]
        {
            (cornerRow - 1, cornerCol - 1),
            (cornerRow - 1, cornerCol),
            (cornerRow,     cornerCol - 1),
            (cornerRow,     cornerCol)
        };

        foreach (var (fr, fc) in adjacentFields)
        {
            if (fr < 0 || fc < 0 || fr >= FieldRows || fc >= FieldCols) continue;

            // Sprawdź czy wieża wroga stoi na tym polu i czy narożnik jest jej sąsiednim
            var enemyTower = board.Towers.FirstOrDefault(t =>
                t.Owner == towerOwner && t.IsAlive && t.Row == fr && t.Col == fc);

            if (enemyTower is not null)
            {
                // Narożniki sąsiadujące z polem (fr,fc): (fr,fc),(fr,fc+1),(fr+1,fc),(fr+1,fc+1)
                var blockedCorners = new[]
                {
                    (fr,     fc),
                    (fr,     fc + 1),
                    (fr + 1, fc),
                    (fr + 1, fc + 1)
                };
                if (blockedCorners.Any(bc => bc.Item1 == cornerRow && bc.Item2 == cornerCol))
                    return true;
            }
        }
        return false;
    }

    private static bool IsValidTowerMove(BoardState board, PieceOwner owner, PieceOwner enemy,
        Tower tower, int toRow, int toCol)
    {
        int dRow = Math.Abs(toRow - tower.Row);
        int dCol = Math.Abs(toCol - tower.Col);

        // Wieża rusza się na jedno z 4 sąsiednich pól
        if (!((dRow == 1 && dCol == 0) || (dRow == 0 && dCol == 1)))
            return false;

        // Nie może wejść na pole zajęte przez inną figurę
        if (board.Towers.Any(t => t.IsAlive && t.Row == toRow && t.Col == toCol))
            return false;
        if (board.Kings.Any(k => k.IsAlive && k.Row == toRow && k.Col == toCol))
            return false;


        return true;
    }

    private static (int row, int col)? FindFreeCornerNearTower(BoardState board, Tower tower)
    {
        // Narożniki pola wieży: (r,c),(r,c+1),(r+1,c),(r+1,c+1)
        var corners = new[]
        {
            (tower.Row, tower.Col),
            (tower.Row, tower.Col + 1),
            (tower.Row + 1, tower.Col),
            (tower.Row + 1, tower.Col + 1)
        };

        foreach (var (r, c) in corners)
        {
            bool occupied = board.Pawns.Any(p => p.IsAlive && p.Row == r && p.Col == c);
            if (!occupied) return (r, c);
        }
        return null;
    }

    private static void CheckKingDeath(BoardState board, PieceOwner defeatedOwner, Game game)
    {
        foreach (var king in board.Kings.Where(k => k.Owner == defeatedOwner && k.IsAlive))
        {
            // Narożniki wokół pola króla
            var corners = new[]
            {
                (king.Row,     king.Col),
                (king.Row,     king.Col + 1),
                (king.Row + 1, king.Col),
                (king.Row + 1, king.Col + 1)
            };

            int enemyCount = corners.Count(c =>
                board.Pawns.Any(p =>
                    p.Owner != defeatedOwner && p.IsAlive &&
                    p.Row == c.Item1 && p.Col == c.Item2));

            if (enemyCount >= 3)
            {
                king.IsAlive = false;

                // Wszyscy pionki i wieże tego gracza giną
                foreach (var p in board.Pawns.Where(p => p.Owner == defeatedOwner))
                {
                    p.IsAlive = false;
                    p.IsDead = false; // nie można wskrzesić po śmierci króla
                }
                foreach (var t in board.Towers.Where(t => t.Owner == defeatedOwner))
                    t.IsAlive = false;
            }
        }

        // Sprawdź czy gra skończona (oba królowie jednego gracza martwi)
        bool p2Lost = board.Kings.Where(k => k.Owner == defeatedOwner).All(k => !k.IsAlive);
        if (p2Lost)
        {
            game.Status = GameStatus.Finished;
            game.FinishedAt = DateTime.UtcNow;
            game.WinnerId = defeatedOwner == PieceOwner.Player1
                ? game.Player2Id
                : game.Player1Id;
        }
    }

    public async Task<List<object>> GetHistoryAsync(int playerId) =>
        (await db.Games
            .Include(g => g.Player1)
            .Include(g => g.Player2)
            .Where(g => g.Player1Id == playerId || g.Player2Id == playerId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync())
        .Select(g => (object)new
        {
            g.Id,
            g.Player1Id,
            g.Player2Id,
            g.WinnerId,
            g.Status,   // enum jako liczba: 0/1/2
            g.CreatedAt,
            player1 = new { g.Player1.Username },
            player2 = g.Player2 == null ? null : new { g.Player2.Username }
        })
        .ToList();

    public async Task<Game?> GetGameAsync(int gameId) =>
        await db.Games
            .Include(g => g.Player1)
            .Include(g => g.Player2)
            .FirstOrDefaultAsync(g => g.Id == gameId);
}