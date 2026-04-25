namespace SigmaChess.Engine;

public class Board
{
    // Основное хранилище состояния доски: 8x8 клеток.
    public Piece?[,] Squares { get; } = new Piece?[8, 8];

    public void Initialize()
    {
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                Squares[row, col] = null;
            }
        }

        // Черные фигуры.
        SetupBackRank(0, PieceColor.Black);
        SetupPawns(1, PieceColor.Black);

        // Белые фигуры.
        SetupPawns(6, PieceColor.White);
        SetupBackRank(7, PieceColor.White);
    }

    public Piece? GetPiece(Position position)
    {
        EnsureInsideBoard(position);
        return Squares[position.Row, position.Col];
    }

    public void SetPiece(Position position, Piece? piece)
    {
        EnsureInsideBoard(position);
        Squares[position.Row, position.Col] = piece;
    }

    public void MovePiece(Move move)
    {
        EnsureInsideBoard(move.From);
        EnsureInsideBoard(move.To);

        var movingPiece = GetPiece(move.From);
        if (movingPiece is null)
        {
            throw new InvalidOperationException("There is no piece on the source position.");
        }

        SetPiece(move.To, movingPiece);
        SetPiece(move.From, null);
    }

    public bool IsInsideBoard(Position position)
    {
        return position.Row >= 0 &&
               position.Row < 8 &&
               position.Col >= 0 &&
               position.Col < 8;
    }

    private void SetupPawns(int row, PieceColor color)
    {
        for (var col = 0; col < 8; col++)
        {
            Squares[row, col] = new Piece(PieceType.Pawn, color);
        }
    }

    private void SetupBackRank(int row, PieceColor color)
    {
        Squares[row, 0] = new Piece(PieceType.Rook, color);
        Squares[row, 1] = new Piece(PieceType.Knight, color);
        Squares[row, 2] = new Piece(PieceType.Bishop, color);
        Squares[row, 3] = new Piece(PieceType.Queen, color);
        Squares[row, 4] = new Piece(PieceType.King, color);
        Squares[row, 5] = new Piece(PieceType.Bishop, color);
        Squares[row, 6] = new Piece(PieceType.Knight, color);
        Squares[row, 7] = new Piece(PieceType.Rook, color);
    }

    private void EnsureInsideBoard(Position position)
    {
        if (!IsInsideBoard(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is outside the board.");
        }
    }
}
