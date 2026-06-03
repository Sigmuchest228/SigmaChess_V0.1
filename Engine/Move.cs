namespace SigmaChess.Engine;

// Один ход: откуда (From), куда (To) и, если это превращение пешки, во что
// превращается (Promotion). Для обычного хода Promotion равно null. Объект
// неизменяемый. Ходы создаёт MoveGenerator, проверяет GameRules, применяет Game,
// а история ходов лежит в Game.History.
public class Move
{
    public Position From { get; }
    public Position To { get; }
    public PieceType? Promotion { get; }

    // Конструктор: задаёт начальную и конечную клетки и (необязательно) фигуру
    // превращения. По умолчанию превращения нет (promotion = null).
    public Move(Position from, Position to, PieceType? promotion = null)
    {
        From = from;
        To = to;
        Promotion = promotion;
    }

    // Возвращает копию этого же хода, но с указанной фигурой превращения.
    // Нужно, когда ход уже выбран, а игрок только что выбрал, в кого превратить пешку.
    public Move WithPromotion(PieceType promotion) => new(From, To, promotion);
}
