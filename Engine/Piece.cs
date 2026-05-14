namespace SigmaChess.Engine;

/// <summary>Шесть стандартных типов шахматных фигур.</summary>
public enum PieceType
{
    Pawn,    // Пешка
    Knight,  // Конь
    Bishop,  // Слон
    Rook,    // Ладья
    Queen,   // Ферзь
    King     // Король
}

/// <summary>Цвет фигуры. По нему движок определяет, чей ход и направление пешек.</summary>
public enum PieceColor
{
    White,
    Black
}

/// <summary>
/// Фигура: тип + цвет. Превращение пешки делается через создание
/// нового <see cref="Piece" />(promo, color), а не мутацию.
/// </summary>
public class Piece
{
    public PieceType Type { get; }
    public PieceColor Color { get; }

    public Piece(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
    }
}
