using System.Text;

namespace SigmaChess.Engine;

public partial class Game
{

    private readonly Dictionary<string, int> _positionCounts = new(64);

    public Board Board { get; } = new();

    public PieceColor CurrentTurn { get; private set; } = PieceColor.White;

    public bool WhiteCanCastleKingside { get; private set; } = true;
    public bool WhiteCanCastleQueenside { get; private set; } = true;
    public bool BlackCanCastleKingside { get; private set; } = true;
    public bool BlackCanCastleQueenside { get; private set; } = true;

    public Position? EnPassantTarget { get; private set; }

    public int HalfmoveClock { get; private set; }

    public List<Move> History { get; } = new();

    public IReadOnlyDictionary<string, int> PositionCounts => _positionCounts;

    public Game()
        : this(emptyForFen: false)
    {
    }

    public bool MakeMove(Move move)
    {
        var piece = Board.GetPiece(move.From);
        if (piece is null || piece.Color != CurrentTurn)
        {

            return false;
        }

        var capturedDirect = Board.GetPiece(move.To);
        var isPawn = piece.Type == PieceType.Pawn;
        var isEnPassant = isPawn && EnPassantTarget == move.To && capturedDirect is null;
        var isCapture = capturedDirect is not null || isEnPassant;
        var isDoubleStep = isPawn && Math.Abs(move.To.Row - move.From.Row) == 2;

        ApplyMoveToBoard(Board, move, EnPassantTarget);

        UpdateCastlingRightsAfter(move, piece, capturedDirect);

        EnPassantTarget = isDoubleStep
            ? new Position((move.From.Row + move.To.Row) / 2, move.From.Col)
            : null;

        HalfmoveClock = (isPawn || isCapture) ? 0 : HalfmoveClock + 1;

        History.Add(move);

        CurrentTurn = CurrentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;

        CountCurrentPosition();

        return true;
    }

    public static void ApplyMoveToBoard(Board board, Move move, Position? enPassantTarget)
    {
        var piece = board.GetPiece(move.From)
            ?? throw new InvalidOperationException("ApplyMoveToBoard: source square is empty.");

        var isEp = piece.Type == PieceType.Pawn
                   && enPassantTarget == move.To
                   && board.GetPiece(move.To) is null;
        var isCastle = piece.Type == PieceType.King && Math.Abs(move.To.Col - move.From.Col) == 2;

        board.MovePiece(move);

        if (isEp)
        {

            var dir = piece.Color == PieceColor.White ? -1 : 1;
            board.SetPiece(new Position(move.To.Row - dir, move.To.Col), null);
        }

        if (isCastle)
        {

            var row = piece.Color == PieceColor.White ? 7 : 0;
            if (move.To.Col == 6)
            {
                var rook = board.GetPiece(new Position(row, 7));
                board.SetPiece(new Position(row, 7), null);
                board.SetPiece(new Position(row, 5), rook);
            }
            else
            {
                var rook = board.GetPiece(new Position(row, 0));
                board.SetPiece(new Position(row, 0), null);
                board.SetPiece(new Position(row, 3), rook);
            }
        }

        if (move.Promotion is { } promo)
        {

            board.SetPiece(move.To, new Piece(promo, piece.Color));
        }
    }

    private void UpdateCastlingRightsAfter(Move move, Piece moved, Piece? captured)
    {
        if (moved.Type == PieceType.King)
        {
            if (moved.Color == PieceColor.White)
            {
                WhiteCanCastleKingside = false;
                WhiteCanCastleQueenside = false;
            }
            else
            {
                BlackCanCastleKingside = false;
                BlackCanCastleQueenside = false;
            }
        }
        else if (moved.Type == PieceType.Rook)
        {
            if (moved.Color == PieceColor.White)
            {
                if (move.From == new Position(7, 0)) WhiteCanCastleQueenside = false;
                if (move.From == new Position(7, 7)) WhiteCanCastleKingside = false;
            }
            else
            {
                if (move.From == new Position(0, 0)) BlackCanCastleQueenside = false;
                if (move.From == new Position(0, 7)) BlackCanCastleKingside = false;
            }
        }

        if (captured is { Type: PieceType.Rook })
        {
            if (move.To == new Position(7, 0)) WhiteCanCastleQueenside = false;
            if (move.To == new Position(7, 7)) WhiteCanCastleKingside = false;
            if (move.To == new Position(0, 0)) BlackCanCastleQueenside = false;
            if (move.To == new Position(0, 7)) BlackCanCastleKingside = false;
        }
    }

    private void CountCurrentPosition()
    {
        var key = HashPosition();
        _positionCounts[key] = _positionCounts.GetValueOrDefault(key) + 1;
    }

    private string HashPosition()
    {
        var sb = new StringBuilder(80);
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var p = Board.GetPiece(new Position(r, c));
                sb.Append(p is null ? '.' : Symbol(p));
            }
        }

        sb.Append(CurrentTurn == PieceColor.White ? 'w' : 'b');
        sb.Append(WhiteCanCastleKingside ? 'K' : '-');
        sb.Append(WhiteCanCastleQueenside ? 'Q' : '-');
        sb.Append(BlackCanCastleKingside ? 'k' : '-');
        sb.Append(BlackCanCastleQueenside ? 'q' : '-');
        if (EnPassantTarget is { } ep)
        {
            sb.Append(ep.Row);
            sb.Append(',');
            sb.Append(ep.Col);
        }
        else
        {
            sb.Append('-');
        }

        return sb.ToString();
    }

    private static char Symbol(Piece p)
    {
        var ch = p.Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => '?',
        };

        return p.Color == PieceColor.White ? char.ToUpper(ch) : ch;
    }
}
