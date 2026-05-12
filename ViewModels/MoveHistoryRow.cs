namespace SigmaChess.ViewModels;

/// <summary>Одна строка записи ходов: полный номер хода и полуходы белых/чёрных.</summary>
public sealed class MoveHistoryRow
{
    public int FullMoveNumber { get; init; }

    public string WhiteMove { get; init; } = string.Empty;

    public string BlackMove { get; init; } = string.Empty;

    public string NumberLabel => $"{FullMoveNumber}.";
}
