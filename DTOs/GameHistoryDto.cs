namespace BoardGameApi.DTOs;

public record GameHistoryDto(
    int Id,
    int? Player1Id,
    int? Player2Id,
    string Status,
    DateTime CreatedAt
);