namespace SigmaChess.Models;

// Краткая сводка о сыгранной партии для списков (не вся партия, а только то, что нужно
// показать строкой): id партии (GameId), победитель (GameWinner), причина окончания
// (EndReason) и когда закончилась (EndedAt). Свойства init-only — задаются при создании
// и дальше не меняются. Из этой сводки строится PlayedGameRowViewModel.
public class PastGame
{
    public string GameId { get; init; } = string.Empty;

    public string GameWinner { get; init; } = string.Empty;

    public string EndReason { get; init; } = string.Empty;

    public DateTimeOffset? EndedAt { get; init; }
}
