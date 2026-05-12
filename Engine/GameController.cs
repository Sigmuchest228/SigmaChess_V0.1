namespace SigmaChess.Engine;

/// <summary>
/// «UI-фасад» над движком: хранит текущую партию, выбранную пользователем фигуру,
/// список её легальных ходов и последний выполненный ход (для подсветки).
/// <para>
/// API специально расколот на три метода:
///   <list type="bullet">
///     <item><see cref="HandleSelection"/> — обработать тап без хода (выбор/сброс фигуры),</item>
///     <item><see cref="GetPendingMove"/> — узнать, какой ход выполнится при тапе на клетку,</item>
///     <item><see cref="ExecutePlannedMove"/> — собственно применить ход (возможно с Promotion).</item>
///   </list>
/// Это нужно, чтобы между «увидеть планируемый ход» и «исполнить» можно было
/// показать UI-попап выбора фигуры превращения (асинхронный await).
/// </para>
/// </summary>
public class GameController
{
    private readonly MoveGenerator _moveGenerator = new();
    private readonly GameRules _rules;
    private Game _game = new();
    private Position? _selected;
    private List<Move> _legalMoves = [];
    private Move? _lastMove;

    public GameController()
    {
        _rules = new GameRules(_moveGenerator);
    }

    /// <summary>Создаёт новую партию (сбрасывает выбор и подсветку «последнего хода»).</summary>
    public void InitializeGame()
    {
        _game = new();
        _lastMove = null;
        ClearSelection();
    }

    /// <summary>Загружает позицию из FEN (задачи).</summary>
    public bool TryLoadFromFen(string fen)
    {
        var loaded = Game.TryFromFen(fen);
        if (loaded is null)
        {
            return false;
        }

        _game = loaded;
        _lastMove = null;
        ClearSelection();
        return true;
    }

    public Board GetBoard() => _game.Board;

    /// <summary>Полная история сыгранных ходов (полуходы по очереди белые-чёрные).</summary>
    public IReadOnlyList<Move> GetPlayedMoves() => _game.History;

    /// <summary>
    /// Пересборка позиции после <paramref name="appliedPlies"/> полуходов из текущей истории
    /// (0 — стартовая позиция, <see cref="GetPlayedMoves"/>.Count — совпадает с живой доской).
    /// </summary>
    public Board GetBoardAfterPlies(int appliedPlies)
    {
        var history = _game.History;
        var n = Math.Clamp(appliedPlies, 0, history.Count);
        var replay = new Game();
        for (var i = 0; i < n; i++)
        {
            replay.MakeMove(history[i]);
        }

        return replay.Board;
    }

    public PieceColor GetCurrentTurn() => _game.CurrentTurn;

    public GameResult GetGameResult() => _rules.GetGameResult(_game.Board, _game.CurrentTurn, _game);

    public Position? GetSelectedSquare() => _selected;

    /// <summary>
    /// Клетки, которые подсвечиваются как возможные цели хода выбранной фигуры.
    /// 4 промоушен-хода целятся в одну клетку — здесь они схлопываются в одну подсветку.
    /// </summary>
    public IReadOnlyList<(int Row, int Col)> GetHighlightedSquares()
    {
        var seen = new HashSet<(int, int)>();
        var result = new List<(int Row, int Col)>(_legalMoves.Count);
        foreach (var m in _legalMoves)
        {
            if (seen.Add((m.To.Row, m.To.Col)))
            {
                result.Add((m.To.Row, m.To.Col));
            }
        }

        return result;
    }

    /// <summary>Последний успешно выполненный ход (для подсветки в UI).</summary>
    public Move? GetLastMove() => _lastMove;

    /// <summary>
    /// Тап без выполнения хода: либо выбираем фигуру и считаем её легальные ходы,
    /// либо снимаем выбор. Сюда попадаем, если нет «pending move» по координатам.
    /// </summary>
    public void HandleSelection(int row, int col)
    {
        var to = new Position(row, col);
        var board = _game.Board;
        var tapped = board.GetPiece(to);
        if (tapped is not null && tapped.Color == _game.CurrentTurn)
        {
            // Своя фигура — выбираем её, считаем все легальные ходы из этой клетки.
            _selected = to;
            _legalMoves = [.._rules.GetLegalMovesFrom(board, to, _game)];
            return;
        }

        // Кликнули по пустой клетке или фигуре противника без активного выбора — сбрасываем.
        ClearSelection();
    }

    /// <summary>
    /// Если уже что-то выбрано и (row,col) — легальная цель, возвращает первый из подходящих ходов.
    /// Для промоушена 4 хода имеют общие From/To — ViewModel подменит Promotion перед исполнением.
    /// Если нет «запланированного хода» — возвращает null (значит, надо переключать выбор).
    /// </summary>
    public Move? GetPendingMove(int row, int col)
    {
        if (_selected is null)
        {
            return null;
        }

        var to = new Position(row, col);
        foreach (var m in _legalMoves)
        {
            if (m.To == to)
            {
                return m;
            }
        }

        return null;
    }

    /// <summary>
    /// Применяет ход (включая возможный Promotion, который ViewModel мог подменить).
    /// Возвращает false как страховка, если движок отказал — на практике не должен.
    /// </summary>
    public bool ExecutePlannedMove(Move move)
    {
        if (!_game.MakeMove(move))
        {
            return false;
        }

        _lastMove = move;
        ClearSelection();
        return true;
    }

    /// <summary>Сброс и воспроизведение списка ходов (просмотр сохранённой партии).</summary>
    public bool TryReplayMoves(IReadOnlyList<Move> moves)
    {
        InitializeGame();
        Move? last = null;
        foreach (var m in moves)
        {
            if (!_game.MakeMove(m))
            {
                InitializeGame();
                return false;
            }

            last = m;
        }

        _lastMove = last;
        ClearSelection();
        return true;
    }

    /// <summary>
    /// Тонкая обёртка для случаев без промоушена: сразу либо двигает, либо переключает выбор.
    /// Оставлена ради обратной совместимости и для тестов; основной поток UI идёт через
    /// разделённые методы <see cref="GetPendingMove"/>/<see cref="ExecutePlannedMove"/>.
    /// </summary>
    public void HandleCellClick(int row, int col)
    {
        var pending = GetPendingMove(row, col);
        if (pending is not null && pending.Promotion is null)
        {
            ExecutePlannedMove(pending);
            return;
        }

        HandleSelection(row, col);
    }

    /// <summary>Снимает выделение фигуры (например после неверного хода в задаче).</summary>
    public void ClearMoveSelection() => ClearSelection();

    private void ClearSelection()
    {
        _selected = null;
        _legalMoves = [];
    }
}
