using Newtonsoft.Json;

namespace SigmaChess.Models;

/// <summary>Партия в узле ChessGames/{gameId} (как хранится в базе).</summary>
public class SavedGame
{
    [JsonProperty("WhiteUid")]
    public string WhiteUid { get; set; } = string.Empty;

    [JsonProperty("BlackUid")]
    public string BlackUid { get; set; } = string.Empty;

    /// <summary><c>White</c>, <c>Black</c> или <c>Draw</c>.</summary>
    [JsonProperty("Winner")]
    public string Winner { get; set; } = string.Empty;

    [JsonProperty("EndReason")]
    public string EndReason { get; set; } = string.Empty;

    /// <summary>Время окончания партии, ISO 8601 UTC.</summary>
    [JsonProperty("DateTime")]
    public string DateTime { get; set; } = string.Empty;

    [JsonProperty("Moves")]
    public Dictionary<string, SavedMove> Moves { get; set; } = new();
}
