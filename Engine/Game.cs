namespace SigmaChess.Engine;

public class Game
{
    private readonly MoveGenerator _moveGenerator = new();

    public Board Board { get; } = new();

    public PieceColor CurrentTurn { get; private set; } = PieceColor.White;

    public Game()
    {
        Board.Initialize();
    }

    // Выполняет ход, если он допустим для текущей стороны.
    public bool MakeMove(Move move)
    {
        var piece = Board.GetPiece(move.From);
        if (piece is null || piece.Color != CurrentTurn)
        {
            return false;
        }

        var possibleMoves = _moveGenerator.GetPossibleMoves(Board, move.From);
        var isLegalMove = possibleMoves.Any(m =>
            m.To.Row == move.To.Row &&
            m.To.Col == move.To.Col);

        if (!isLegalMove)
        {
            return false;
        }

        Board.MovePiece(move);
        CurrentTurn = CurrentTurn == PieceColor.White
            ? PieceColor.Black
            : PieceColor.White;

        return true;
    }
}
