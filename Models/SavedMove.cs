using Newtonsoft.Json;

namespace SigmaChess.Models;

// Модель одного хода так, как он сохраняется в Firebase. Хранит начальную и конечную
// клетки в виде строк (FromPos, ToPos, например «e2» и «e4»), номер хода (MoveNumber),
// id сходившего игрока (User), сколько секунд занял ход (TimePerMove) и пометку мата
// (IsCheckmate). Заметь: тип превращения пешки тут не хранится — он восстанавливается
// при реплее перебором (GameReplayMoveResolver). Атрибуты [JsonProperty] задают имена
// полей в базе.
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
