namespace SigmaChess.Engine;

// «Мост» между движком и интерфейсом. Хранит текущую партию (Game) и состояние
// выбора на доске (какая клетка выбрана, какие ходы ей доступны, какой ход был
// последним). Интерфейс (GameViewModel) не работает с Game напрямую, а зовёт методы
// этого контроллера: выбрать фигуру, получить подсветку, сделать ход, узнать
// результат. Один контроллер живёт на всё приложение (создаётся в AppService).
public class GameController
{
    // Генератор ходов и правила — внутренние инструменты движка.
    private readonly MoveGenerator _moveGenerator = new();
    private readonly GameRules _rules;
    // Текущая партия.
    private Game _game = new();
    // Выбранная сейчас клетка (фигура, которой собираются ходить), или null.
    private Position? _selected;
    // Легальные ходы выбранной фигуры (для подсветки и проверки клика).
    private List<Move> _legalMoves = [];
    // Последний сыгранный ход (для подсветки «откуда-куда»).
    private Move? _lastMove;

    // Конструктор: создаёт правила, передав им генератор ходов.
    public GameController()
    {
        _rules = new GameRules(_moveGenerator);
    }

    // Начинает новую партию с нуля: новая Game (стартовая позиция), сброс последнего
    // хода и выбора.
    public void InitializeGame()
    {
        _game = new();
        _lastMove = null;
        ClearSelection();
    }

    // Отдаёт текущую доску наружу (интерфейс читает по ней клетки).
    public Board GetBoard() => _game.Board;

    // Отдаёт список всех сыгранных ходов (история партии).
    public IReadOnlyList<Move> GetPlayedMoves() => _game.History;

    // Возвращает, как выглядела доска после первых appliedPlies полуходов. Делает это
    // «с чистого листа»: создаёт отдельную Game и проигрывает на ней нужное число
    // ходов из истории. Текущую партию не трогает. Используется для перемотки
    // (шаг назад/вперёд) при просмотре своей партии.
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

    // Чей сейчас ход.
    public PieceColor GetCurrentTurn() => _game.CurrentTurn;

    // Текущий результат партии (идёт игра / шах / мат / пат / ничья) — спрашивает у
    // правил для текущей доски и стороны.
    public GameResult GetGameResult() => _rules.GetGameResult(_game.Board, _game.CurrentTurn, _game);

    // Какая клетка сейчас выбрана (или null).
    public Position? GetSelectedSquare() => _selected;

    // Возвращает клетки, которые надо подсветить как доступные ходы выбранной фигуры.
    // Берёт цели из списка легальных ходов и убирает дубликаты (на одну клетку может
    // вести несколько ходов, например 4 превращения пешки).
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

    // Последний сыгранный ход (для подсветки).
    public Move? GetLastMove() => _lastMove;

    // Обрабатывает выбор фигуры по тапу. Если на клетке стоит фигура того, чей сейчас
    // ход — запоминает её как выбранную и сразу считает её легальные ходы (через
    // GameRules). Иначе сбрасывает выбор.
    public void HandleSelection(int row, int col)
    {
        var to = new Position(row, col);
        var board = _game.Board;
        var tapped = board.GetPiece(to);
        if (tapped is not null && tapped.Color == _game.CurrentTurn)
        {

            _selected = to;
            _legalMoves = [.._rules.GetLegalMovesFrom(board, to, _game)];
            return;
        }

        ClearSelection();
    }

    // Проверяет, является ли тап по клетке (row, col) завершением хода уже выбранной
    // фигуры. Если среди легальных ходов есть ход на эту клетку — возвращает его,
    // иначе null. Заново легальность НЕ считается — берётся из готового списка.
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

    // Выполняет выбранный ход: просит Game сыграть его. Если получилось — запоминает
    // как последний ход и сбрасывает выбор. Возвращает успех.
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

    // Восстанавливает партию из готового списка ходов: начинает новую игру и
    // проигрывает ходы по очереди. Если какой-то ход не применился — сбрасывает всё и
    // возвращает false. Используется при просмотре сохранённой партии (реплей).
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

    // Сбрасывает текущий выбор фигуры (публичная обёртка для интерфейса).
    public void ClearMoveSelection() => ClearSelection();

    // Внутренний сброс выбора: забывает выбранную клетку и её список ходов.
    private void ClearSelection()
    {
        _selected = null;
        _legalMoves = [];
    }
}
