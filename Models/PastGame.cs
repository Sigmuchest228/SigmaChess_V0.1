namespace SigmaChess.Models;

public class PastGame
{
    public string GameId { get; init; } = string.Empty;

    public string GameWinner { get; init; } = string.Empty;

    public string EndReason { get; init; } = string.Empty;

    public DateTimeOffset? EndedAt { get; init; }
}
