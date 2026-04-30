namespace SigmaChess.Engine;

/// <summary>
/// Координаты клетки на доске. Row = 0..7 (0 — верхний край = чёрные),
/// Col = 0..7 (0 — крайний левый файл «a»).
/// Сделана как readonly record struct: value-семантика, бесплатное равенство по полям
/// (можно сравнивать через ==), нулевая аллокация в горячем цикле генерации ходов.
/// </summary>
public readonly record struct Position(int Row, int Col);
