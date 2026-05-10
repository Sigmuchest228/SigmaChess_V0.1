using Newtonsoft.Json;

namespace SigmaChess.Services;

/// <summary>Один записанный полуход для RTDB (PascalCase как в раннем экспорте).</summary>
public sealed class FirebaseMoveRecord
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

/// <summary>Корень партии в ветке ChessGames/{gameId}.</summary>
public sealed class FirebaseGameRecord
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
    public Dictionary<string, FirebaseMoveRecord> Moves { get; set; } = new();
}

public sealed class UserProfileRtdbDto
{
    [JsonProperty("UserName")]
    public string? UserName { get; set; }

    [JsonProperty("Elo")]
    public int? Elo { get; set; }

    [JsonProperty("RegisterDate")]
    public long? RegisterDate { get; set; }
}
