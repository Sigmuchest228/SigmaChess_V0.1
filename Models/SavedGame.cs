using Newtonsoft.Json;

namespace SigmaChess.Models;

public class SavedGame
{
    [JsonProperty("WhiteUid")]
    public string WhiteUid { get; set; } = string.Empty;

    [JsonProperty("BlackUid")]
    public string BlackUid { get; set; } = string.Empty;

    [JsonProperty("Winner")]
    public string Winner { get; set; } = string.Empty;

    [JsonProperty("EndReason")]
    public string EndReason { get; set; } = string.Empty;

    [JsonProperty("DateTime")]
    public string DateTime { get; set; } = string.Empty;

    [JsonProperty("Moves")]
    public Dictionary<string, SavedMove> Moves { get; set; } = new();
}
