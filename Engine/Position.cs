namespace SigmaChess.Engine;

// Координата одной клетки доски. Row — ряд сверху вниз (0 это верхний, 7 нижний),
// Col — столбец слева направо (0..7). Это readonly record struct, то есть значение
// неизменяемо, а сравнение двух Position идёт по содержимому (две клетки равны, если
// совпали Row и Col). Используется почти везде: Board, Move, MoveGenerator, GameRules.
public readonly record struct Position(int Row, int Col);
