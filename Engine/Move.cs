namespace SigmaChess.Engine;

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

    public Move WithPromotion(PieceType promotion) => new(From, To, promotion);
}
