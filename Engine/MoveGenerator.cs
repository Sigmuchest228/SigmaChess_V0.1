namespace SigmaChess.Engine;

public class MoveGenerator
{

    private static readonly (int dRow, int dCol)[] BishopDirections =
        { (-1, -1), (-1, 1), (1, -1), (1, 1) };

    private static readonly (int dRow, int dCol)[] RookDirections =
        { (-1, 0), (1, 0), (0, -1), (0, 1) };

    private static readonly (int dRow, int dCol)[] QueenDirections =
        { (-1, -1), (-1, 1), (1, -1), (1, 1), (-1, 0), (1, 0), (0, -1), (0, 1) };

    private static readonly (int dRow, int dCol)[] KnightOffsets =
        { (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1) };

    public List<Move> GetPossibleMoves(Board board, Position position, Position? enPassantTarget = null)
    {
        var piece = board.GetPiece(position);
        if (piece is null)
        {
            return [];
        }

        return piece.Type switch
        {
            PieceType.Pawn => GetPawnMoves(board, position, piece.Color, enPassantTarget),
            PieceType.Knight => GetKnightMoves(board, position, piece.Color),
            PieceType.Bishop => GetSlidingMoves(board, position, piece.Color, BishopDirections),
            PieceType.Rook => GetSlidingMoves(board, position, piece.Color, RookDirections),
            PieceType.Queen => GetSlidingMoves(board, position, piece.Color, QueenDirections),
            PieceType.King => GetKingMoves(board, position, piece.Color),
            _ => []
        };
    }

    private static List<Move> GetPawnMoves(Board board, Position from, PieceColor color, Position? epTarget)
    {
        var moves = new List<Move>();
        var direction = color == PieceColor.White ? -1 : 1;
        var startRow = color == PieceColor.White ? 6 : 1;
        var lastRow = color == PieceColor.White ? 0 : 7;

        var oneStep = new Position(from.Row + direction, from.Col);
        if (board.IsInsideBoard(oneStep) && board.GetPiece(oneStep) is null)
        {
            AddPawnMoveOrPromotions(moves, from, oneStep, lastRow);

            var twoStep = new Position(from.Row + (2 * direction), from.Col);
            if (from.Row == startRow && board.GetPiece(twoStep) is null)
            {
                moves.Add(new Move(from, twoStep));
            }
        }

        TryAddPawnCapture(board, moves, from, color, direction, -1, lastRow, epTarget);
        TryAddPawnCapture(board, moves, from, color, direction, +1, lastRow, epTarget);

        return moves;
    }

    private static void TryAddPawnCapture(
        Board board,
        List<Move> moves,
        Position from,
        PieceColor color,
        int direction,
        int captureOffset,
        int lastRow,
        Position? epTarget)
    {
        var to = new Position(from.Row + direction, from.Col + captureOffset);
        if (!board.IsInsideBoard(to))
        {
            return;
        }

        var target = board.GetPiece(to);

        if (target is not null && target.Color != color)
        {
            AddPawnMoveOrPromotions(moves, from, to, lastRow);
            return;
        }

        if (target is null && epTarget == to)
        {
            moves.Add(new Move(from, to));
        }
    }

    private static void AddPawnMoveOrPromotions(List<Move> moves, Position from, Position to, int lastRow)
    {
        if (to.Row != lastRow)
        {
            moves.Add(new Move(from, to));
            return;
        }

        moves.Add(new Move(from, to, PieceType.Queen));
        moves.Add(new Move(from, to, PieceType.Rook));
        moves.Add(new Move(from, to, PieceType.Bishop));
        moves.Add(new Move(from, to, PieceType.Knight));
    }

    private static List<Move> GetKnightMoves(Board board, Position from, PieceColor color)
    {
        var moves = new List<Move>();
        foreach (var (dRow, dCol) in KnightOffsets)
        {
            AddIfEmptyOrEnemy(board, moves, from, new Position(from.Row + dRow, from.Col + dCol), color);
        }

        return moves;
    }

    private static List<Move> GetKingMoves(Board board, Position from, PieceColor color)
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

                AddIfEmptyOrEnemy(board, moves, from, new Position(from.Row + dRow, from.Col + dCol), color);
            }
        }

        return moves;
    }

    private static List<Move> GetSlidingMoves(Board board, Position from, PieceColor color, (int dRow, int dCol)[] directions)
    {
        var moves = new List<Move>();
        foreach (var (dRow, dCol) in directions)
        {
            var row = from.Row + dRow;
            var col = from.Col + dCol;

            while (board.IsInsideBoard(new Position(row, col)))
            {
                var to = new Position(row, col);
                var target = board.GetPiece(to);
                if (target is null)
                {
                    moves.Add(new Move(from, to));
                }
                else
                {
                    if (target.Color != color)
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

    private static void AddIfEmptyOrEnemy(Board board, List<Move> moves, Position from, Position to, PieceColor movingColor)
    {
        if (!board.IsInsideBoard(to))
        {
            return;
        }

        var target = board.GetPiece(to);
        if (target is null || target.Color != movingColor)
        {
            moves.Add(new Move(from, to));
        }
    }
}
