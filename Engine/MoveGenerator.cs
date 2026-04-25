namespace SigmaChess.Engine;

public class MoveGenerator
{
    // Генерирует базовые ходы выбранной фигуры без проверки шаха.
    public List<Move> GetPossibleMoves(Board board, Position position)
    {
        var piece = board.GetPiece(position);
        if (piece is null)
        {
            return [];
        }

        return piece.Type switch
        {
            PieceType.Pawn => GetPawnMoves(board, position, piece.Color),
            PieceType.Knight => GetKnightMoves(board, position, piece.Color),
            PieceType.Bishop => GetSlidingMoves(board, position, piece.Color, [(-1, -1), (-1, 1), (1, -1), (1, 1)]),
            PieceType.Rook => GetSlidingMoves(board, position, piece.Color, [(-1, 0), (1, 0), (0, -1), (0, 1)]),
            PieceType.Queen => GetSlidingMoves(board, position, piece.Color, [(-1, -1), (-1, 1), (1, -1), (1, 1), (-1, 0), (1, 0), (0, -1), (0, 1)]),
            PieceType.King => GetKingMoves(board, position, piece.Color),
            _ => []
        };
    }

    private List<Move> GetPawnMoves(Board board, Position from, PieceColor color)
    {
        var moves = new List<Move>();
        var direction = color == PieceColor.White ? -1 : 1;
        var startRow = color == PieceColor.White ? 6 : 1;

        var oneStep = new Position(from.Row + direction, from.Col);
        if (board.IsInsideBoard(oneStep) && board.GetPiece(oneStep) is null)
        {
            moves.Add(new Move(from, oneStep));

            var twoStep = new Position(from.Row + (2 * direction), from.Col);
            if (from.Row == startRow && board.IsInsideBoard(twoStep) && board.GetPiece(twoStep) is null)
            {
                moves.Add(new Move(from, twoStep));
            }
        }

        AddPawnCaptureIfValid(board, from, moves, color, direction, -1);
        AddPawnCaptureIfValid(board, from, moves, color, direction, 1);

        return moves;
    }

    private List<Move> GetKnightMoves(Board board, Position from, PieceColor color)
    {
        var moves = new List<Move>();
        (int dRow, int dCol)[] offsets =
        [
            (-2, -1), (-2, 1),
            (-1, -2), (-1, 2),
            (1, -2), (1, 2),
            (2, -1), (2, 1)
        ];

        foreach (var (dRow, dCol) in offsets)
        {
            var to = new Position(from.Row + dRow, from.Col + dCol);
            AddMoveIfEmptyOrEnemy(board, moves, from, to, color);
        }

        return moves;
    }

    private List<Move> GetKingMoves(Board board, Position from, PieceColor color)
    {
        var moves = new List<Move>();

        for (var dRow = -1; dRow <= 1; dRow++)
        {
            for (var dCol = -1; dCol <= 1; dCol++)
            {
                if (dRow == 0 && dCol == 0)
                {
                    continue;
                }

                var to = new Position(from.Row + dRow, from.Col + dCol);
                AddMoveIfEmptyOrEnemy(board, moves, from, to, color);
            }
        }

        return moves;
    }

    private List<Move> GetSlidingMoves(Board board, Position from, PieceColor color, (int dRow, int dCol)[] directions)
    {
        var moves = new List<Move>();

        foreach (var (dRow, dCol) in directions)
        {
            var row = from.Row + dRow;
            var col = from.Col + dCol;

            while (board.IsInsideBoard(new Position(row, col)))
            {
                var to = new Position(row, col);
                var targetPiece = board.GetPiece(to);

                if (targetPiece is null)
                {
                    moves.Add(new Move(from, to));
                }
                else
                {
                    if (targetPiece.Color != color)
                    {
                        moves.Add(new Move(from, to));
                    }

                    break;
                }

                row += dRow;
                col += dCol;
            }
        }

        return moves;
    }

    private void AddPawnCaptureIfValid(
        Board board,
        Position from,
        List<Move> moves,
        PieceColor movingColor,
        int direction,
        int captureOffset)
    {
        var to = new Position(from.Row + direction, from.Col + captureOffset);
        if (!board.IsInsideBoard(to))
        {
            return;
        }

        var targetPiece = board.GetPiece(to);
        if (targetPiece is not null && targetPiece.Color != movingColor)
        {
            moves.Add(new Move(from, to));
        }
    }

    private void AddMoveIfEmptyOrEnemy(Board board, List<Move> moves, Position from, Position to, PieceColor movingColor)
    {
        if (!board.IsInsideBoard(to))
        {
            return;
        }

        var targetPiece = board.GetPiece(to);
        if (targetPiece is null || targetPiece.Color != movingColor)
        {
            moves.Add(new Move(from, to));
        }
    }
}
