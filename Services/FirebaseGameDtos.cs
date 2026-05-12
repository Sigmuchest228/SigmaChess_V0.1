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

/// <summary>Задача в ветке ChessPuzzles/{puzzleId}.</summary>
public sealed class FirebasePuzzleDto
{
    [JsonProperty("Fen")]
    public string Fen { get; set; } = string.Empty;

    [JsonProperty("Solution")]
    public FirebasePuzzleSolutionDto Solution { get; set; } = new();

    [JsonProperty("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("Difficulty")]
    public int Difficulty { get; set; }
}

/// <summary>Единственный целевой полуход для MVP задачи.</summary>
public sealed class FirebasePuzzleSolutionDto
{
    [JsonProperty("FromPos")]
    public string FromPos { get; set; } = string.Empty;

    [JsonProperty("ToPos")]
    public string ToPos { get; set; } = string.Empty;

    [JsonProperty("Promotion")]
    public string? Promotion { get; set; }
}

/// <summary>Сводка партии для списка «Played games».</summary>
public sealed class PlayedGameSummary
{
    public string GameId { get; init; } = string.Empty;

    /// <summary>Исход партии: <c>White</c>, <c>Black</c> или <c>Draw</c>.</summary>
    public string GameWinner { get; init; } = string.Empty;

    public string EndReason { get; init; } = string.Empty;

    public DateTimeOffset? EndedAt { get; init; }
}

public sealed class UserProfileRtdbDto
{
    [JsonProperty("UserName")]
    public string? UserName { get; set; }

    /// <summary>Имя в нижнем регистре для префикс-поиска в RTDB (<c>OrderByChild</c>).</summary>
    [JsonProperty("UserNameLower")]
    public string? UserNameLower { get; set; }

    /// <summary>Устаревшее поле рейтинга; только для чтения старых узлов.</summary>
    [JsonProperty("Elo")]
    public int? Elo { get; set; }

    /// <summary>Число решённых задач; ранг через <see cref="UserSigmaRank.GetRankTitle"/>.</summary>
    [JsonProperty("PuzzlesSolved")]
    public int? PuzzlesSolved { get; set; }

    [JsonProperty("RegisterDate")]
    public long? RegisterDate { get; set; }

    /// <summary>Download URL после загрузки в Firebase Storage, аватар.</summary>
    [JsonProperty("AvatarUrl")]
    public string? AvatarUrl { get; set; }

    /// <summary>Имя файла пресета из бандла (<c>WallpapersPresets/…</c>), если нет кастомного фона.</summary>
    [JsonProperty("WallpaperPreset")]
    public string? WallpaperPreset { get; set; }

    /// <summary>Кастомные обои из Storage; если задано — имеет приоритет над <see cref="WallpaperPreset"/>.</summary>
    [JsonProperty("WallpaperCustomUrl")]
    public string? WallpaperCustomUrl { get; set; }
}

/// <summary>Сводка профиля для списка подписок и поиска.</summary>
public sealed class FollowProfileSummary
{
    public string Uid { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Число решённых задач; титул — <see cref="UserSigmaRank.GetRankTitle"/>.</summary>
    public int PuzzlesSolved { get; init; }

    public string? AvatarUrl { get; init; }
}
