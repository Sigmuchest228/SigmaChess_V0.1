namespace SigmaChess.Engine;

/// <summary>Текущий статус партии. UI показывает текст по этому значению.</summary>
public enum GameResult
{
    /// <summary>Игра идёт, шаха нет.</summary>
    Ongoing,
    /// <summary>Сторона, чей сейчас ход, под шахом, но ходы есть.</summary>
    Check,
    /// <summary>Шах + нет легальных ходов = мат.</summary>
    Checkmate,
    /// <summary>Шаха нет + нет легальных ходов = пат (ничья).</summary>
    Stalemate,
    /// <summary>Полуходов без хода пешкой и без взятия больше или равно 100 = ничья.</summary>
    DrawFiftyMoveRule,
    /// <summary>Какая-то позиция повторилась 3 раза = ничья.</summary>
    DrawRepetition,
    /// <summary>На доске нет материала, достаточного для мата (KK, KB+K, KN+K, KB+KB одного цвета).</summary>
    DrawInsufficientMaterial,
}

/// <summary>
/// Шахматные правила поверх <see cref="MoveGenerator"/>.
/// Здесь живёт всё, что требует знания «весь ли ход целиком легален»:
/// king-safety, рокировка с её FIDE-условиями, проверка концовок партии.
/// </summary>
public class GameRules
{
    private readonly MoveGenerator _moveGenerator;

    public GameRules(MoveGenerator moveGenerator)
    {
        _moveGenerator = moveGenerator;
    }

    /// <summary>
    /// Все легальные ходы конкретной фигуры с клетки <paramref name="from"/>.
    /// Используется UI для подсветки целей при выборе фигуры.
    /// </summary>
    public IReadOnlyList<Move> GetLegalMovesFrom(Board board, Position from, Game game)
    {
        var piece = board.GetPiece(from);
        if (piece is null)
        {
            return [];
        }

        // Берём «псевдо-легальные» ходы и отфильтровываем те, после которых наш король под шахом.
        var legal = new List<Move>();
        foreach (var move in _moveGenerator.GetPossibleMoves(board, from, game.EnPassantTarget))
        {
            if (IsMoveLegal(board, move, game.EnPassantTarget))
            {
                legal.Add(move);
            }
        }

        // Рокировку выдаём отдельно — у неё свои строгие проверки FIDE.
        if (piece.Type == PieceType.King)
        {
            legal.AddRange(GetCastlingMovesFor(board, piece.Color, game));
        }

        return legal;
    }

    /// <summary>Все легальные ходы стороны. Используется для определения мата/пата.</summary>
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

        // Один раз дописываем рокировки (они проверяются один раз, не зависят от точки старта в цикле).
        result.AddRange(GetCastlingMovesFor(board, side, game));
        return result;
    }

    /// <summary>Стоит ли король указанного цвета под шахом прямо сейчас.</summary>
    public bool IsKingInCheck(Board board, PieceColor color)
    {
        var king = FindKing(board, color);
        if (king is null)
        {
            // Защита от багов: в нормальной партии король всегда есть.
            return false;
        }

        var attacker = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(board, king.Value, attacker);
    }

    /// <summary>
    /// Не оставит ли ход своего короля под шахом.
    /// Принцип: клонируем доску, применяем ход (включая EP/рокировку/превращение),
    /// смотрим, не в шахе ли наш король после этого.
    /// </summary>
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

    /// <summary>
    /// Главная диагностика партии. Порядок проверок важен:
    /// сначала «нет ходов» (мат/пат), потом постепенные ничьи, и только потом «шах/идёт».
    /// </summary>
    public GameResult GetGameResult(Board board, PieceColor side, Game game)
    {
        var legal = GetAllLegalMoves(board, side, game);
        var inCheck = IsKingInCheck(board, side);

        // Нет легальных ходов = либо мат (если под шахом), либо пат.
        if (legal.Count == 0)
        {
            return inCheck ? GameResult.Checkmate : GameResult.Stalemate;
        }

        // 50 ходов без взятия и без хода пешкой = ничья.
        // HalfmoveClock считает полуходы, поэтому 100 = 50 ходов каждой стороны.
        if (game.HalfmoveClock >= 100)
        {
            return GameResult.DrawFiftyMoveRule;
        }

        // Троекратное повторение позиции = ничья.
        // _positionCounts накапливается в Game после каждого хода.
        foreach (var count in game.PositionCounts.Values)
        {
            if (count >= 3)
            {
                return GameResult.DrawRepetition;
            }
        }

        // Недостаточно материала для мата (тривиальные эндшпили).
        if (IsInsufficientMaterial(board))
        {
            return GameResult.DrawInsufficientMaterial;
        }

        // Партия идёт; различаем «обычное продолжение» и «шах не мат».
        return inCheck ? GameResult.Check : GameResult.Ongoing;
    }

    // ---------- Рокировка ----------

    // Условия FIDE для рокировки:
    //   1. Право не утрачено (король и нужная ладья ни разу не двигались).
    //   2. Между королём и ладьёй пусто.
    //   3. Король не в шахе сейчас.
    //   4. Король не проходит через атакованную клетку.
    //   5. Король не оказывается в шахе после хода.
    // Условия 1 и 2 проверяем напрямую, 3-5 — через клон доски и IsSquareAttacked.
    private List<Move> GetCastlingMovesFor(Board board, PieceColor color, Game game)
    {
        var moves = new List<Move>();
        var row = color == PieceColor.White ? 7 : 0;     // нижняя для белых, верхняя для чёрных.
        var kingPos = new Position(row, 4);              // король стоит на e1/e8.

        var king = board.GetPiece(kingPos);
        if (king is null || king.Type != PieceType.King || king.Color != color)
        {
            // Король не на стартовой клетке — рокировка невозможна.
            return moves;
        }

        // Условие 3: король не в шахе. Если в шахе — обе рокировки отвергаем.
        if (IsKingInCheck(board, color))
        {
            return moves;
        }

        // Достаём права на рокировку из состояния партии.
        var (canKingside, canQueenside) = color == PieceColor.White
            ? (game.WhiteCanCastleKingside, game.WhiteCanCastleQueenside)
            : (game.BlackCanCastleKingside, game.BlackCanCastleQueenside);

        // Короткая (на крыло короля): король e→g, ладья h→f.
        if (canKingside &&
            board.GetPiece(new Position(row, 5)) is null &&     // f-клетка пуста
            board.GetPiece(new Position(row, 6)) is null &&     // g-клетка пуста
            IsRookAt(board, new Position(row, 7), color) &&     // на h стоит наша ладья
            !KingSquareAttackedAfterMove(board, color, kingPos, new Position(row, 5)) &&  // f не под боем
            !KingSquareAttackedAfterMove(board, color, kingPos, new Position(row, 6)))    // g не под боем
        {
            moves.Add(new Move(kingPos, new Position(row, 6)));
        }

        // Длинная (на ферзевый фланг): король e→c, ладья a→d.
        // Заметим, что b-клетка (Col=1) тоже должна быть пустой по правилам, хотя
        // король через неё не проходит (поэтому проверка атак её не касается).
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

    // Pattern-matching хелпер: «на клетке pos лежит наша ладья».
    private static bool IsRookAt(Board board, Position pos, PieceColor color) =>
        board.GetPiece(pos) is { Type: PieceType.Rook } r && r.Color == color;

    // Имитируем перенос короля на клетку kingTo и спрашиваем «под боем ли она?».
    // Нужно для проверки «не через шах».
    private bool KingSquareAttackedAfterMove(Board board, PieceColor color, Position kingFrom, Position kingTo)
    {
        var clone = CloneBoard(board);
        var king = clone.GetPiece(kingFrom);
        clone.SetPiece(kingFrom, null);
        clone.SetPiece(kingTo, king);
        var enemy = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(clone, kingTo, enemy);
    }

    // ---------- Атаки ----------

    // Атакует ли сторона byAttacker клетку square?
    // Важная деталь: пешки обработаны ОТДЕЛЬНО. Дело в том, что MoveGenerator выдаёт
    // пешечный диагональный ход только если на диагонали стоит фигура противника
    // (или EP). А для проверки безопасности клетки нам нужны ВСЕ атаки пешек,
    // включая удары на пустые клетки — иначе король может зайти в клетку, которую
    // бьёт пешка, и движок этого не заметит.
    private bool IsSquareAttacked(Board board, Position square, PieceColor byAttacker)
    {
        // Пешки. Если byAttacker = White, его пешки бьют «вверх» (направление -1),
        // значит атакуют клетку с двух «нижних» диагоналей по отношению к ней.
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

        // Все остальные фигуры — генератор корректно выдаёт цели атак.
        // Пешки в этом цикле пропускаем, чтобы не делать двойную работу.
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

    // ---------- Недостаточный материал ----------

    // Партия — ничья по недостатку материала, если выполняется один из тривиальных случаев:
    //   - KK (только два короля),
    //   - K + B vs K, K + N vs K (один лёгкий минор у одной стороны),
    //   - K + B vs K + B при условии, что слоны на полях одного цвета (никакая комбинация
    //     ходов не приведёт к мату).
    // Любая ладья/ферзь/пешка автоматически считается достаточным материалом.
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

                // Любая «тяжёлая» фигура или пешка = мат возможен.
                if (p.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen)
                {
                    return false;
                }

                if (p.Type == PieceType.King)
                {
                    continue;
                }

                // (r + c) % 2 = 0 для светлой клетки, 1 — для тёмной.
                var bucket = p.Color == PieceColor.White ? whiteMinors : blackMinors;
                bucket.Add((p.Type, (r + c) % 2));
            }
        }

        // KK
        if (whiteMinors.Count == 0 && blackMinors.Count == 0) return true;

        // K + (B|N) vs K
        if (whiteMinors.Count == 1 && blackMinors.Count == 0 &&
            whiteMinors[0].T is PieceType.Bishop or PieceType.Knight) return true;
        if (blackMinors.Count == 1 && whiteMinors.Count == 0 &&
            blackMinors[0].T is PieceType.Bishop or PieceType.Knight) return true;

        // KB vs KB — только если оба слона на полях одного цвета.
        if (whiteMinors.Count == 1 && blackMinors.Count == 1 &&
            whiteMinors[0].T == PieceType.Bishop && blackMinors[0].T == PieceType.Bishop &&
            whiteMinors[0].SquareColor == blackMinors[0].SquareColor)
        {
            return true;
        }

        return false;
    }

    // ---------- Утилиты ----------

    // Создаём полную копию доски. Piece иммутабельный (record), поэтому ссылки можно делить.
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

    // Линейный поиск короля. Можно было бы кешировать, но для 8x8 это микрооптимизация.
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
