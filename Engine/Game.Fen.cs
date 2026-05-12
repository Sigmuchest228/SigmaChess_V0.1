namespace SigmaChess.Engine;

/// <summary>
/// Загрузка позиции из FEN (placement + сторона хода + рокировка + EP + halfmove).
/// </summary>
public partial class Game
{
    private Game(bool emptyForFen)
    {
        if (emptyForFen)
        {
            Board.ClearAllPieces();
            History.Clear();
            _positionCounts.Clear();
            CurrentTurn = PieceColor.White;
            WhiteCanCastleKingside = false;
            WhiteCanCastleQueenside = false;
            BlackCanCastleKingside = false;
            BlackCanCastleQueenside = false;
            EnPassantTarget = null;
            HalfmoveClock = 0;
        }
        else
        {
            Board.Initialize();
            CountCurrentPosition();
        }
    }

    /// <summary>Возвращает партию из FEN или <c>null</c> при ошибке разбора.</summary>
    public static Game? TryFromFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            return null;
        }

        var g = new Game(emptyForFen: true);
        var parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
        {
            return null;
        }

        if (!TryParsePlacement(g.Board, parts[0]))
        {
            return null;
        }

        if (string.Equals(parts[1], "w", StringComparison.OrdinalIgnoreCase))
        {
            g.CurrentTurn = PieceColor.White;
        }
        else if (string.Equals(parts[1], "b", StringComparison.OrdinalIgnoreCase))
        {
            g.CurrentTurn = PieceColor.Black;
        }
        else
        {
            return null;
        }

        if (!TryParseCastlingRights(g, parts[2]))
        {
            return null;
        }

        if (!TryParseEnPassantTarget(g, parts[3]))
        {
            return null;
        }

        if (parts.Length > 4 && int.TryParse(parts[4], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var hm))
        {
            g.HalfmoveClock = Math.Max(0, hm);
        }

        g.CountCurrentPosition();
        return g;
    }

    private static bool TryParsePlacement(Board board, string placement)
    {
        var ranks = placement.Split('/');
        if (ranks.Length != 8)
        {
            return false;
        }

        for (var ri = 0; ri < 8; ri++)
        {
            var col = 0;
            foreach (var ch in ranks[ri])
            {
                if (char.IsDigit(ch))
                {
                    var skip = ch - '0';
                    if (skip is < 1 or > 8)
                    {
                        return false;
                    }

                    col += skip;
                    continue;
                }

                if (!TryPieceFromFenChar(ch, out var piece))
                {
                    return false;
                }

                if (col >= 8)
                {
                    return false;
                }

                board.SetPiece(new Position(ri, col), piece);
                col++;
            }

            if (col != 8)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryPieceFromFenChar(char ch, out Piece piece)
    {
        var lower = char.ToLowerInvariant(ch);
        if (!PieceTypeFromChar(lower, out var type))
        {
            piece = null!;
            return false;
        }

        var color = char.IsUpper(ch) ? PieceColor.White : PieceColor.Black;
        piece = new Piece(type, color);
        return true;
    }

    private static bool PieceTypeFromChar(char lower, out PieceType type)
    {
        switch (lower)
        {
            case 'p':
                type = PieceType.Pawn;
                return true;
            case 'r':
                type = PieceType.Rook;
                return true;
            case 'n':
                type = PieceType.Knight;
                return true;
            case 'b':
                type = PieceType.Bishop;
                return true;
            case 'q':
                type = PieceType.Queen;
                return true;
            case 'k':
                type = PieceType.King;
                return true;
            default:
                type = default;
                return false;
        }
    }

    private static bool TryParseCastlingRights(Game g, string castling)
    {
        g.WhiteCanCastleKingside = false;
        g.WhiteCanCastleQueenside = false;
        g.BlackCanCastleKingside = false;
        g.BlackCanCastleQueenside = false;

        if (string.Equals(castling, "-", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var c in castling)
        {
            switch (c)
            {
                case 'K':
                    g.WhiteCanCastleKingside = true;
                    break;
                case 'Q':
                    g.WhiteCanCastleQueenside = true;
                    break;
                case 'k':
                    g.BlackCanCastleKingside = true;
                    break;
                case 'q':
                    g.BlackCanCastleQueenside = true;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryParseEnPassantTarget(Game g, string ep)
    {
        if (string.Equals(ep, "-", StringComparison.Ordinal))
        {
            g.EnPassantTarget = null;
            return true;
        }

        if (ep.Length != 2)
        {
            return false;
        }

        if (!TryParseAlgebraicSquare(ep, out var pos))
        {
            return false;
        }

        g.EnPassantTarget = pos;
        return true;
    }

    /// <summary>Клетка в нотации «e4», координаты как в движке (row 0 = чёрные сверху).</summary>
    private static bool TryParseAlgebraicSquare(string square, out Position pos)
    {
        pos = default;
        if (square.Length != 2)
        {
            return false;
        }

        var file = char.ToLowerInvariant(square[0]);
        if (file is < 'a' or > 'h')
        {
            return false;
        }

        if (!char.IsDigit(square[1]))
        {
            return false;
        }

        var rankDigit = square[1] - '0';
        if (rankDigit is < 1 or > 8)
        {
            return false;
        }

        var col = file - 'a';
        var row = 8 - rankDigit;
        pos = new Position(row, col);
        return true;
    }
}
