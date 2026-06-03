namespace SigmaChess.Engine;

// Перечисление типов фигур: пешка, конь, слон, ладья, ферзь, король.
// Используется фигурой Piece и везде, где надо понять, как фигура ходит.
public enum PieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}

// Перечисление цвета фигуры: белые или чёрные. Также обозначает сторону,
// чей сейчас ход (см. CurrentTurn в Game).
public enum PieceColor
{
    White,
    Black
}

// Одна шахматная фигура. Хранит только две вещи: тип (какая фигура) и цвет (чья).
// Фигура не знает, где она стоит — её положение хранит Board. Объект неизменяемый:
// создали с нужными типом и цветом и больше не меняем (при превращении пешки
// создаётся новая Piece).
public class Piece
{
    public PieceType Type { get; }
    public PieceColor Color { get; }

    // Конструктор: задаёт тип и цвет фигуры при создании.
    public Piece(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
    }
}
