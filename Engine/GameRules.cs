namespace SigmaChess.Engine;

public enum GameResult
{

    Ongoing,

    Check,

    Checkmate,

    Stalemate,

    DrawFiftyMoveRule,

    DrawRepetition,

    DrawInsufficientMaterial,
}

public class GameRules
{
    private readonly MoveGenerator _moveGenerator;

    public GameRules(MoveGenerator moveGenerator)
    {
        _moveGenerator = moveGenerator;
    }

    public IReadOnlyList<Move> GetLegalMovesFrom(Board board, Position from, Game game)
    {
        var piece = board.GetPiece(from);
        if (piece is null)
        {
            return [];
        }

        var legal = new List<Move>();
        foreach (var move in _moveGenerator.GetPossibleMoves(board, from, game.EnPassantTarget))
        {
            if (IsMoveLegal(board, move, game.EnPassantTarget))
            {
                legal.Add(move);
            }
        }

        if (piece.Type == PieceType.King)
        {
            legal.AddRange(GetCastlingMovesFor(board, piece.Color, game));
        }

        return legal;
    }

    public List<Move> GetAllLegalMoves(Board board, PieceColor side, Game game)
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

                foreach (var move in _moveGenerator.GetPossibleMoves(board, from, game.EnPassantTarget))
                {
                    if (IsMoveLegal(board, move, game.EnPassantTarget))
                    {
                        result.Add(move);
                    }
                }
            }
        }

        result.AddRange(GetCastlingMovesFor(board, side, game));
        return result;
    }

    public bool IsKingInCheck(Board board, PieceColor color)
    {
        var king = FindKing(board, color);
        if (king is null)
        {

            return false;
        }

        var attacker = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(board, king.Value, attacker);
    }

    public bool IsMoveLegal(Board board, Move move, Position? epTarget)
    {
        var moving = board.GetPiece(move.From);
        if (moving is null)
        {
            return false;
        }

        var clone = CloneBoard(board);
        Game.ApplyMoveToBoard(clone, move, epTarget);
        return !IsKingInCheck(clone, moving.Color);
    }

    public GameResult GetGameResult(Board board, PieceColor side, Game game)
    {
        var legal = GetAllLegalMoves(board, side, game);
        var inCheck = IsKingInCheck(board, side);

        if (legal.Count == 0)
        {
            return inCheck ? GameResult.Checkmate : GameResult.Stalemate;
        }

        if (game.HalfmoveClock >= 100)
        {
            return GameResult.DrawFiftyMoveRule;
        }

        foreach (var count in game.PositionCounts.Values)
        {
            if (count >= 3)
            {
                return GameResult.DrawRepetition;
            }
        }

        if (IsInsufficientMaterial(board))
        {
            return GameResult.DrawInsufficientMaterial;
        }

        return inCheck ? GameResult.Check : GameResult.Ongoing;
    }

    private List<Move> GetCastlingMovesFor(Board board, PieceColor color, Game game)
    {
        var moves = new List<Move>();
        var row = color == PieceColor.White ? 7 : 0;
        var kingPos = new Position(row, 4);

        var king = board.GetPiece(kingPos);
        if (king is null || king.Type != PieceType.King || king.Color != color)
        {

            return moves;
        }

        if (IsKingInCheck(board, color))
        {
            return moves;
        }

        var (canKingside, canQueenside) = color == PieceColor.White
            ? (game.WhiteCanCastleKingside, game.WhiteCanCastleQueenside)
            : (game.BlackCanCastleKingside, game.BlackCanCastleQueenside);

        if (canKingside &&
            board.GetPiece(new Position(row, 5)) is null &&
            board.GetPiece(new Position(row, 6)) is null &&
            IsRookAt(board, new Position(row, 7), color) &&
            !KingSquareAttackedAfterMove(board, color, kingPos, new Position(row, 5)) &&
            !KingSquareAttackedAfterMove(board, color, kingPos, new Position(row, 6)))
        {
            moves.Add(new Move(kingPos, new Position(row, 6)));
        }

        if (canQueenside &&
            board.GetPiece(new Position(row, 1)) is null &&
            board.GetPiece(new Position(row, 2)) is null &&
            board.GetPiece(new Position(row, 3)) is null &&
            IsRookAt(board, new Position(row, 0), color) &&
            !KingSquareAttackedAfterMove(board, color, kingPos, new Position(row, 3)) &&
            !KingSquareAttackedAfterMove(board, color, kingPos, new Position(row, 2)))
        {
            moves.Add(new Move(kingPos, new Position(row, 2)));
        }

        return moves;
    }

    private static bool IsRookAt(Board board, Position pos, PieceColor color) =>
        board.GetPiece(pos) is { Type: PieceType.Rook } r && r.Color == color;

    private bool KingSquareAttackedAfterMove(Board board, PieceColor color, Position kingFrom, Position kingTo)
    {
        var clone = CloneBoard(board);
        var king = clone.GetPiece(kingFrom);
        clone.SetPiece(kingFrom, null);
        clone.SetPiece(kingTo, king);
        var enemy = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(clone, kingTo, enemy);
    }

    private bool IsSquareAttacked(Board board, Position square, PieceColor byAttacker)
    {

        var pawnDir = byAttacker == PieceColor.White ? -1 : 1;
        foreach (var dc in new[] { -1, 1 })
        {
            var pawnFrom = new Position(square.Row - pawnDir, square.Col - dc);
            if (board.IsInsideBoard(pawnFrom) &&
                board.GetPiece(pawnFrom) is { Type: PieceType.Pawn } p &&
                p.Color == byAttacker)
            {
                return true;
            }
        }

        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var from = new Position(r, c);
                var piece = board.GetPiece(from);
                if (piece is null || piece.Color != byAttacker || piece.Type == PieceType.Pawn)
                {
                    continue;
                }

                foreach (var move in _moveGenerator.GetPossibleMoves(board, from))
                {
                    if (move.To == square)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsInsufficientMaterial(Board board)
    {
        var whiteMinors = new List<(PieceType T, int SquareColor)>();
        var blackMinors = new List<(PieceType T, int SquareColor)>();

        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var p = board.GetPiece(new Position(r, c));
                if (p is null) continue;

                if (p.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen)
                {
                    return false;
                }

                if (p.Type == PieceType.King)
                {
                    continue;
                }

                var bucket = p.Color == PieceColor.White ? whiteMinors : blackMinors;
                bucket.Add((p.Type, (r + c) % 2));
            }
        }

        if (whiteMinors.Count == 0 && blackMinors.Count == 0) return true;

        if (whiteMinors.Count == 1 && blackMinors.Count == 0 &&
            whiteMinors[0].T is PieceType.Bishop or PieceType.Knight) return true;
        if (blackMinors.Count == 1 && whiteMinors.Count == 0 &&
            blackMinors[0].T is PieceType.Bishop or PieceType.Knight) return true;

        if (whiteMinors.Count == 1 && blackMinors.Count == 1 &&
            whiteMinors[0].T == PieceType.Bishop && blackMinors[0].T == PieceType.Bishop &&
            whiteMinors[0].SquareColor == blackMinors[0].SquareColor)
        {
            return true;
        }

        return false;
    }

    private static Board CloneBoard(Board source)
    {
        var board = new Board();
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var pos = new Position(r, c);
                var piece = source.GetPiece(pos);
                if (piece is not null)
                {
                    board.SetPiece(pos, piece);
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
}
