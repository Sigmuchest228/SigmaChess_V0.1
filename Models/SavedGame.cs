using Newtonsoft.Json;

namespace SigmaChess.Models;

// Модель завершённой партии в Firebase. Хранит id игроков белыми и чёрными (WhiteUid,
// BlackUid), победителя (Winner), причину окончания (EndReason), дату (DateTime) и все
// ходы партии (Moves — словарь, ключ задаёт порядок хода). По этим данным строится
// просмотр партии (реплей). Атрибуты [JsonProperty] задают имена полей в базе.
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
