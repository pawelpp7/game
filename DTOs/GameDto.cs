namespace BoardGameApi.DTOs;

public enum MoveType { MovePawn, MoveTower, Resurrect, BonusMove }

public record MoveDto(
    MoveType Type,
    int PieceId,
    int ToRow,
    int ToCol
);

public record TurnDto(
    MoveDto PawnAction,
    MoveDto? BonusPawnAction,
    MoveDto? TowerAction
);