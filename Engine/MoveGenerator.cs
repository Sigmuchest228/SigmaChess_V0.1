namespace SigmaChess.Engine;

/// <summary>
/// Генератор «псевдо-легальных» ходов выбранной фигуры.
/// «Псевдо» — значит проверены геометрия и собственные/чужие фигуры, но НЕ проверено,
/// не оставит ли ход своего короля под шахом. Эту проверку делает уже
/// <see cref="GameRules.IsMoveLegal"/>.
/// <para>
/// Рокировка специально не выдаётся здесь (она требует знания прав на рокировку и
/// проверок «не через шах»), её добавляет <see cref="GameRules.GetLegalMovesFrom"/>.
/// </para>
/// </summary>
public class MoveGenerator
{
    // Восемь направлений, по которым «скользят» дальнобойные фигуры.
    // Слон ходит по диагоналям, ладья — по линиям, ферзь — по обеим группам.
    private static readonly (int dRow, int dCol)[] BishopDirections =
        { (-1, -1), (-1, 1), (1, -1), (1, 1) };

    private static readonly (int dRow, int dCol)[] RookDirections =
        { (-1, 0), (1, 0), (0, -1), (0, 1) };

    private static readonly (int dRow, int dCol)[] QueenDirections =
        { (-1, -1), (-1, 1), (1, -1), (1, 1), (-1, 0), (1, 0), (0, -1), (0, 1) };

    // Все восемь возможных «L-прыжков» коня.
    private static readonly (int dRow, int dCol)[] KnightOffsets =
        { (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1) };

    /// <summary>
    /// Главная точка входа: вернёт все псевдо-легальные ходы фигуры на клетке.
    /// <paramref name="enPassantTarget"/> приходит из <see cref="Game.EnPassantTarget"/>
    /// и нужен только пешкам.
    /// </summary>
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

    // Пешка — самая капризная фигура: разное направление по цвету, два шага только из стартового
    // ряда, бьёт диагонально, плюс en passant и превращение на последней горизонтали.
    private static List<Move> GetPawnMoves(Board board, Position from, PieceColor color, Position? epTarget)
    {
        var moves = new List<Move>();
        var direction = color == PieceColor.White ? -1 : 1;   // белые идут вверх (к меньшему Row).
        var startRow = color == PieceColor.White ? 6 : 1;     // ряд, с которого разрешён двойной шаг.
        var lastRow = color == PieceColor.White ? 0 : 7;      // ряд, на котором происходит превращение.

        // Один шаг вперёд — только если впереди пусто.
        var oneStep = new Position(from.Row + direction, from.Col);
        if (board.IsInsideBoard(oneStep) && board.GetPiece(oneStep) is null)
        {
            AddPawnMoveOrPromotions(moves, from, oneStep, lastRow);

            // Двойной шаг — только из стартового ряда и через пустую промежуточную клетку
            // (которую мы уже проверили). Двойной шаг сам не попадает на последнюю горизонталь,
            // поэтому промоушен здесь невозможен.
            var twoStep = new Position(from.Row + (2 * direction), from.Col);
            if (from.Row == startRow && board.GetPiece(twoStep) is null)
            {
                moves.Add(new Move(from, twoStep));
            }
        }

        // Две диагонали взятия (с EP).
        TryAddPawnCapture(board, moves, from, color, direction, -1, lastRow, epTarget);
        TryAddPawnCapture(board, moves, from, color, direction, +1, lastRow, epTarget);

        return moves;
    }

    // Обрабатывает обычное диагональное взятие и en passant как один случай.
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

        // Обычное взятие: на диагонали стоит фигура противника.
        if (target is not null && target.Color != color)
        {
            AddPawnMoveOrPromotions(moves, from, to, lastRow);
            return;
        }

        // En passant: клетка пуста, но совпадает с EP-целью.
        // Промоушен с EP невозможен (EP-цель никогда не на последней горизонтали).
        if (target is null && epTarget == to)
        {
            moves.Add(new Move(from, to));
        }
    }

    // Если ход пешки попадает на последнюю горизонталь — выдаём 4 хода (один на каждую возможную фигуру).
    // UI потом покажет попап выбора и подменит Promotion на нужный.
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

    // Конь: 8 фиксированных «L»-смещений, фильтр пустая-или-вражеская клетка.
    private static List<Move> GetKnightMoves(Board board, Position from, PieceColor color)
    {
        var moves = new List<Move>();
        foreach (var (dRow, dCol) in KnightOffsets)
        {
            AddIfEmptyOrEnemy(board, moves, from, new Position(from.Row + dRow, from.Col + dCol), color);
        }

        return moves;
    }

    // Король: 8 соседних клеток (рокировку добавляет GameRules).
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

    // Дальнобойные фигуры (слон/ладья/ферзь): «скользим» по направлению, пока есть
    // место. Алгоритм: пусто — продолжаем, чужая — берём и стоп, своя — стоп.
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

                    // Любая фигура (своя или чужая) останавливает дальнейшее скольжение.
                    break;
                }

                row += dRow;
                col += dCol;
            }
        }

        return moves;
    }

    // Хелпер: добавить ход, если клетка пуста или занята фигурой противника.
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
