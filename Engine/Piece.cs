namespace SigmaChess.Engine;

/// <summary>
/// Иммутабельная фигура: тип + цвет. Превращение пешки делается через создание
/// нового Piece(promo, color), а не мутацию — поэтому setter'ов нет.
/// </summary>
public sealed record Piece(PieceType Type, PieceColor Color);
