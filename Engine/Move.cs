namespace SigmaChess.Engine;

/// <summary>
/// Один шахматный ход: From → To. <see cref="Promotion"/> заполнен только для превращения пешки
/// (Queen/Rook/Bishop/Knight) — генератор выдаёт по одному Move на каждый вариант,
/// а UI потом подменяет фигуру через <see cref="WithPromotion" />.
/// </summary>
public class Move
{
    public Position From { get; }
    public Position To { get; }
    public PieceType? Promotion { get; }

    public Move(Position from, Position to, PieceType? promotion = null)
    {
        From = from;
        To = to;
        Promotion = promotion;
    }

    /// <summary>Копия хода с другой фигурой превращения (те же From/To).</summary>
    public Move WithPromotion(PieceType promotion) => new(From, To, promotion);
}
