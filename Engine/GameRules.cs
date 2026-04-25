namespace SigmaChess.Engine;

public enum GameResult
{
    Ongoing,
    Check,
    Checkmate,
    Stalemate
}

/// <summary>Шахматные правила поверх <see cref="MoveGenerator"/> (легальные ходы, шах, мат, пат).</summary>
public class GameRules
{
    private readonly MoveGenerator _moveGenerator;

    public GameRules(MoveGenerator moveGenerator)
    {
        _moveGenerator = moveGenerator;
    }

    public IReadOnlyList<Move> GetLegalMovesFrom(Board board, Position from)
    {
        if (board.GetPiece(from) is not { } piece)
        {
            return [];
        }

        return _moveGenerator
            .GetPossibleMoves(board, from)
            .Where(m => IsMoveLegal(board, m))
            .ToList();
    }

    public bool IsKingInCheck(Board board, PieceColor color)
    {
        var king = FindKing(board, color);
        if (king is null)
        {
            return false;
        }

        var attacker = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(board, king, attacker);
    }

    public bool IsMoveLegal(Board board, Move move)
    {
        var moving = board.GetPiece(move.From);
        if (moving is null)
        {
            return false;
        }

        var clone = CloneBoard(board);
        clone.MovePiece(move);
        return !IsKingInCheck(clone, moving.Color);
    }

    public List<Move> GetAllLegalMoves(Board board, PieceColor side)
    {
        var result = new List<Move>();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var from = new Position(r, c);
                var piece = board.GetPiece(from);
                if (piece is null || piece.Color != side)
                {
                    continue;
                }

                foreach (var move in _moveGenerator.GetPossibleMoves(board, from))
                {
                    if (IsMoveLegal(board, move))
                    {
                        result.Add(move);
                    }
                }
            }
        }

        return result;
    }

    public GameResult GetGameResult(Board board, PieceColor side)
    {
        var legal = GetAllLegalMoves(board, side);
        var inCheck = IsKingInCheck(board, side);

        if (legal.Count == 0)
        {
            return inCheck ? GameResult.Checkmate : GameResult.Stalemate;
        }

        return inCheck ? GameResult.Check : GameResult.Ongoing;
    }

    private static Board CloneBoard(Board source)
    {
        var board = new Board();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var piece = source.GetPiece(new Position(r, c));
                if (piece is not null)
                {
                    board.SetPiece(new Position(r, c), new Piece(piece.Type, piece.Color));
                }
            }
        }

        return board;
    }

    private static Position? FindKing(Board board, PieceColor color)
    {
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var pos = new Position(r, c);
                var p = board.GetPiece(pos);
                if (p?.Type == PieceType.King && p.Color == color)
                {
                    return pos;
                }
            }
        }

        return null;
    }

    private bool IsSquareAttacked(Board board, Position square, PieceColor byAttacker)
    {
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var from = new Position(r, c);
                var piece = board.GetPiece(from);
                if (piece is null || piece.Color != byAttacker)
                {
                    continue;
                }

                var moves = _moveGenerator.GetPossibleMoves(board, from);
                if (moves.Any(m => m.To.Row == square.Row && m.To.Col == square.Col))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
