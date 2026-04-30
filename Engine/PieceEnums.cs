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
