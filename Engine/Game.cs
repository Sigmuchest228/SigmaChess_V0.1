using System.Text;

namespace SigmaChess.Engine;

/// <summary>
/// Полное состояние партии: доска, чей сейчас ход, права на рокировку,
/// клетка-цель en passant, счётчик правила 50 ходов, история ходов и
/// словарь повторов позиций (для троекратного повторения).
/// <para>
/// Важно: <see cref="MakeMove"/> ДОВЕРЯЕТ, что ход уже проверен на легальность
/// снаружи — это делает <see cref="GameRules"/>. Game лишь применяет ход и
/// обновляет всё сопутствующее состояние.
/// </para>
/// </summary>
public class Game
{
    // Подсчёт повторов: ключ — FEN-подобная строка позиции, значение — сколько раз встретилась.
    private readonly Dictionary<string, int> _positionCounts = new(64);

    public Board Board { get; } = new();

    public PieceColor CurrentTurn { get; private set; } = PieceColor.White;

    // Права на рокировку. Один раз сброшенные — не восстанавливаются.
    public bool WhiteCanCastleKingside { get; private set; } = true;
    public bool WhiteCanCastleQueenside { get; private set; } = true;
    public bool BlackCanCastleKingside { get; private set; } = true;
    public bool BlackCanCastleQueenside { get; private set; } = true;

    /// <summary>
    /// Клетка, которую только что перепрыгнула пешка двойным ходом
    /// (или null, если предыдущий ход не был двойным шагом пешкой).
    /// Именно на эту клетку может сходить пешка-противник, выполняя en passant.
    /// </summary>
    public Position? EnPassantTarget { get; private set; }

    /// <summary>
    /// Счётчик полуходов без взятия и без хода пешкой.
    /// 100 полуходов = 50 полных ходов = ничья по правилу 50 ходов.
    /// </summary>
    public int HalfmoveClock { get; private set; }

    /// <summary>История всех сыгранных ходов в порядке возрастания.</summary>
    public List<Move> History { get; } = new();

    /// <summary>Только для чтения снаружи: для проверки троекратного повторения в GameRules.</summary>
    public IReadOnlyDictionary<string, int> PositionCounts => _positionCounts;

    public Game()
    {
        Board.Initialize();
        // Начальная позиция тоже считается «встреченной один раз», иначе троекратное повторение
        // считалось бы неверно (3 встречи после старта были бы 4 фактических, и т. п.).
        CountCurrentPosition();
    }

    /// <summary>
    /// Применяет ход к партии. Перед вызовом ход уже должен быть отфильтрован
    /// через <see cref="GameRules.IsMoveLegal"/> и (для рокировки) через
    /// <see cref="GameRules.GetLegalMovesFrom"/>.
    /// </summary>
    public bool MakeMove(Move move)
    {
        var piece = Board.GetPiece(move.From);
        if (piece is null || piece.Color != CurrentTurn)
        {
            // Защита: ход не той стороны или пустая клетка — отказ.
            return false;
        }

        // Сначала собираем все факты о ходе ДО его применения, потом меняем доску.
        var capturedDirect = Board.GetPiece(move.To);
        var isPawn = piece.Type == PieceType.Pawn;
        var isEnPassant = isPawn && EnPassantTarget == move.To && capturedDirect is null;
        var isCapture = capturedDirect is not null || isEnPassant;
        var isDoubleStep = isPawn && Math.Abs(move.To.Row - move.From.Row) == 2;

        // Применяем ход на доске. Здесь же обрабатываются EP/рокировка/превращение.
        ApplyMoveToBoard(Board, move, EnPassantTarget);

        // Корректируем права на рокировку (могли утратиться этим ходом).
        UpdateCastlingRightsAfter(move, piece, capturedDirect);

        // EP-цель: ставим только сразу после двойного шага пешки, иначе сбрасываем.
        EnPassantTarget = isDoubleStep
            ? new Position((move.From.Row + move.To.Row) / 2, move.From.Col)
            : null;

        // Правило 50 ходов: «пешка двинулась» или «было взятие» обнуляют счётчик.
        HalfmoveClock = (isPawn || isCapture) ? 0 : HalfmoveClock + 1;

        // Заносим ход в историю партии (потом пригодится для UI и тоже для трассировки).
        History.Add(move);

        // Меняем сторону, чей ход.
        CurrentTurn = CurrentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;

        // Подсчёт повторов после смены хода (хеш включает «чей ход»).
        CountCurrentPosition();

        return true;
    }

    /// <summary>
    /// Применяет ход к произвольной доске, корректно обрабатывая en passant,
    /// рокировку и превращение. Вынесено в static, чтобы переиспользовать в
    /// <see cref="GameRules.IsMoveLegal"/> на клонированной доске — там нет
    /// объекта Game, нужны просто механика «как двигаются фигуры в спецслучаях».
    /// </summary>
    public static void ApplyMoveToBoard(Board board, Move move, Position? enPassantTarget)
    {
        var piece = board.GetPiece(move.From)
            ?? throw new InvalidOperationException("ApplyMoveToBoard: source square is empty.");

        // Распознаём спецслучаи ДО физического переноса — после переноса пешка уже окажется
        // на чужой клетке и определить «было ли это EP» станет нельзя.
        var isEp = piece.Type == PieceType.Pawn
                   && enPassantTarget == move.To
                   && board.GetPiece(move.To) is null;
        var isCastle = piece.Type == PieceType.King && Math.Abs(move.To.Col - move.From.Col) == 2;

        // Базовый перенос From → To (это уберёт обычно-взятую фигуру с To, если она там была).
        board.MovePiece(move);

        if (isEp)
        {
            // En passant: пешка ушла на пустую клетку по диагонали, но реально «сбила» пешку
            // противника, стоящую СЗАДИ от EP-цели (т. е. на той же колонке, что и To,
            // но на той строке, с которой только что прыгнула пешка-жертва двойным шагом).
            var dir = piece.Color == PieceColor.White ? -1 : 1;
            board.SetPiece(new Position(move.To.Row - dir, move.To.Col), null);
        }

        if (isCastle)
        {
            // Рокировка: король уже сдвинут (на g или c). Дополнительно переносим ладью.
            // Короткая (To.Col=6): h→f. Длинная (To.Col=2): a→d.
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
            // Превращение: на месте To стоит пешка, заменяем её на выбранную фигуру того же цвета.
            board.SetPiece(move.To, new Piece(promo, piece.Color));
        }
    }

    // Обновляет права на рокировку. Три случая:
    //   1. Сходил король     => обе рокировки этой стороны утрачены навсегда.
    //   2. Сходила ладья со стартовой клетки => эта одна сторона утрачена.
    //   3. Взяли чужую ладью на её стартовой клетке => у соперника утрачена та сторона.
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
                if (move.From == new Position(7, 0)) WhiteCanCastleQueenside = false; // a1
                if (move.From == new Position(7, 7)) WhiteCanCastleKingside = false;  // h1
            }
            else
            {
                if (move.From == new Position(0, 0)) BlackCanCastleQueenside = false; // a8
                if (move.From == new Position(0, 7)) BlackCanCastleKingside = false;  // h8
            }
        }

        // Взятие чужой ладьи на её стартовой клетке тоже отбирает право на эту рокировку.
        // Например: чёрная ладья ушла, потом была побита на h1 — белые теряют короткую рокировку.
        if (captured is { Type: PieceType.Rook })
        {
            if (move.To == new Position(7, 0)) WhiteCanCastleQueenside = false;
            if (move.To == new Position(7, 7)) WhiteCanCastleKingside = false;
            if (move.To == new Position(0, 0)) BlackCanCastleQueenside = false;
            if (move.To == new Position(0, 7)) BlackCanCastleKingside = false;
        }
    }

    // Прибавляет 1 к счётчику текущей позиции.
    private void CountCurrentPosition()
    {
        var key = HashPosition();
        _positionCounts[key] = _positionCounts.GetValueOrDefault(key) + 1;
    }

    // FEN-подобный хеш позиции: 64 символа доски + сторона хода + права рокировки + EP-цель.
    // Этого набора достаточно, чтобы две позиции считались «одинаковыми» по правилам FIDE
    // для проверки троекратного повторения.
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

    // Один символ для записи фигуры в хеш: строчная — чёрные, заглавная — белые.
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
