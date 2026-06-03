using System.Text;

namespace SigmaChess.Engine;

// Класс «вся партия целиком». Board хранит только расстановку фигур, а Game хранит
// всё остальное состояние партии: чей ход, права на рокировку, поле для взятия на
// проходе, счётчик правила 50 ходов, историю ходов и счётчик повторений позиций.
// Единственный «официальный» способ сыграть ход — MakeMove (он обновляет всё это).
// GameRules только читает состояние Game, а меняет его именно Game.
public class Game
{
    // Счётчик повторений: ключ — отпечаток позиции (строка), значение — сколько раз
    // позиция встречалась. Нужен для ничьей троекратным повторением.
    private readonly Dictionary<string, int> _positionCounts = new(64);

    // Сама доска (где какие фигуры стоят).
    public Board Board { get; } = new();

    // Чей сейчас ход. Партия всегда начинается с белых.
    public PieceColor CurrentTurn { get; private set; } = PieceColor.White;

    // Права на рокировку для каждой стороны и каждого фланга. Снимаются навсегда,
    // когда ходит король или соответствующая ладья (см. UpdateCastlingRightsAfter).
    public bool WhiteCanCastleKingside { get; private set; } = true;
    public bool WhiteCanCastleQueenside { get; private set; } = true;
    public bool BlackCanCastleKingside { get; private set; } = true;
    public bool BlackCanCastleQueenside { get; private set; } = true;

    // Клетка, на которую можно взять пешкой «на проходе» прямо сейчас. Появляется
    // только сразу после двойного хода пешки соперника, иначе null.
    public Position? EnPassantTarget { get; private set; }

    // Счётчик полуходов без взятий и ходов пешкой. Когда дойдёт до 100 (50 полных
    // ходов) — ничья по правилу 50 ходов.
    public int HalfmoveClock { get; private set; }

    // История всех сыгранных ходов по порядку. Используется для реплея и нотации.
    public List<Move> History { get; } = new();

    // Доступ к счётчику повторений только для чтения (для GameRules).
    public IReadOnlyDictionary<string, int> PositionCounts => _positionCounts;

    // Конструктор новой партии: ставит стартовую позицию на доске и сразу запоминает
    // её в счётчике повторений.
    public Game()
    {
        Board.Initialize();
        CountCurrentPosition();
    }

    // Делает ход и обновляет ВСЁ состояние партии. По шагам: проверяет, что на From
    // стоит фигура чьего хода; определяет, было ли это взятие/двойной ход; меняет
    // доску (ApplyMoveToBoard); обновляет права рокировки; задаёт или сбрасывает поле
    // взятия на проходе; обновляет счётчик 50 ходов; пишет ход в историю; передаёт
    // ход сопернику; учитывает позицию для повторений. Возвращает false, если ход
    // невозможен (нет фигуры или не та сторона). Легальность по шахматным правилам
    // тут НЕ проверяется — это уже сделал GameRules до вызова.
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

    // Применяет ход к доске, включая «особые» случаи: взятие на проходе (убирает
    // пешку соперника сбоку), рокировку (переставляет ладью вслед за королём) и
    // превращение (заменяет пешку на выбранную фигуру). Это статический метод и
    // работает с любой доской — поэтому его же использует GameRules для пробных ходов
    // на копии. Состояние партии (ход, права и т.п.) тут не трогается — только доска.
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

    // Обновляет права на рокировку после хода. Логика: если ходил король — сторона
    // теряет обе рокировки; если ходила ладья со своего угла — теряется рокировка на
    // этом фланге; если съели ладью соперника в её углу — соперник теряет рокировку
    // на том фланге.
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

    // Считает текущую позицию: берёт её отпечаток и увеличивает счётчик в словаре.
    // Когда какой-то отпечаток наберёт 3 — будет ничья троекратным повторением.
    private void CountCurrentPosition()
    {
        var key = HashPosition();
        _positionCounts[key] = _positionCounts.GetValueOrDefault(key) + 1;
    }

    // Строит «отпечаток» позиции — короткую строку, однозначно описывающую позицию:
    // все 64 клетки + чей ход + права рокировки + поле взятия на проходе. Две позиции
    // считаются одинаковыми (для правила повторения), если их отпечатки совпали.
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

    // Помощник для отпечатка: возвращает букву фигуры. Заглавная — белые, строчная —
    // чёрные (p пешка, n конь, b слон, r ладья, q ферзь, k король).
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
