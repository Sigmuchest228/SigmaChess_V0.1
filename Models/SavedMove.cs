using Newtonsoft.Json;

namespace SigmaChess.Models;

/// <summary>Один записанный полуход для RTDB (PascalCase как в раннем экспорте).</summary>
public class SavedMove
{
    [JsonProperty("FromPos")]
    public string FromPos { get; set; } = string.Empty;

    [JsonProperty("ToPos")]
    public string ToPos { get; set; } = string.Empty;

    [JsonProperty("MoveNumber")]
    public int MoveNumber { get; set; }

    [JsonProperty("User")]
    public string User { get; set; } = string.Empty;

    [JsonProperty("TimePerMove")]
    public double? TimePerMove { get; set; }

    [JsonProperty("IsCheckmate", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsCheckmate { get; set; }
}
