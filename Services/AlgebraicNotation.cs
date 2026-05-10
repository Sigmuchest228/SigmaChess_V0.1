namespace SigmaChess.Services;

/// <summary>
/// Координаты движка: row 0 — верх доски (чёрные), col 0 — файл a.
/// В алгебре: rank = 8 - row, file = a + col.
/// </summary>
public static class AlgebraicNotation
{
    public static string ToSquare(SigmaChess.Engine.Position pos) =>
        $"{(char)('a' + pos.Col)}{8 - pos.Row}";
}
