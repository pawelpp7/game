namespace BoardGameApi.Models;

public enum PieceType { Pawn, Tower, King }
public enum PieceOwner { Player1, Player2 }

// Pionek stoi na narożniku (siatka 8x9)
public class Pawn
{
    public int Id { get; set; }
    public PieceOwner Owner { get; set; }
    public int Row { get; set; } // 0-8
    public int Col { get; set; } // 0-7
    public bool IsAlive { get; set; } = true;
    public bool IsDead { get; set; } = false; // zbity, można wskrzesić
}

// Wieża stoi na polu (siatka 7x8)
public class Tower
{
    public int Id { get; set; }
    public PieceOwner Owner { get; set; }
    public int Row { get; set; } // 0-7
    public int Col { get; set; } // 0-6
    public bool IsAlive { get; set; } = true;
}

// Król stoi na polu
public class King
{
    public int Id { get; set; }
    public PieceOwner Owner { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public bool IsAlive { get; set; } = true;
}

public class BoardState
{
    public List<Pawn> Pawns { get; set; } = [];
    public List<Tower> Towers { get; set; } = [];
    public List<King> Kings { get; set; } = [];
}