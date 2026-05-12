using SigmaChess.Engine;

namespace SigmaChess.Services;

/// <summary>
/// Координаты движка: row 0 — верх доски (чёрные), col 0 — файл a.
/// В алгебре: rank = 8 - row, file = a + col.
/// </summary>
public static class AlgebraicNotation
{
    public static string ToSquare(SigmaChess.Engine.Position pos) =>
        $"{(char)('a' + pos.Col)}{8 - pos.Row}";

    /// <summary>Парсинг поля вроде <c>e4</c> в координаты движка.</summary>
    public static bool TryParseSquare(string? square, out Position pos)
    {
        pos = default;
        if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
        {
            return false;
        }

        var file = char.ToLowerInvariant(square[0]);
        if (file is < 'a' or > 'h')
        {
            return false;
        }

        if (!char.IsDigit(square[1]))
        {
            return false;
        }

        var rankDigit = square[1] - '0';
        if (rankDigit is < 1 or > 8)
        {
            return false;
        }

        var col = file - 'a';
        var row = 8 - rankDigit;
        pos = new Position(row, col);
        return true;
    }

    /// <summary>Краткая запись хода для списка (например <c>e2-e4</c>).</summary>
    public static string MoveToShortNotation(Move move) =>
        $"{ToSquare(move.From)}-{ToSquare(move.To)}";
}
