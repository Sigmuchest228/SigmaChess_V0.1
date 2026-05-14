namespace SigmaChess.Models;

/// <summary>Одна строка для списка сыгранных партий.</summary>
public class PastGame
{
    public string GameId { get; init; } = string.Empty;

    /// <summary>Исход партии: <c>White</c>, <c>Black</c> или <c>Draw</c>.</summary>
    public string GameWinner { get; init; } = string.Empty;

    public string EndReason { get; init; } = string.Empty;

    public DateTimeOffset? EndedAt { get; init; }
}
