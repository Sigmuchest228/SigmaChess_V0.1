namespace SigmaChess.Engine;

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

    public void InitializeGame()
    {
        _game = new();
        _lastMove = null;
        ClearSelection();
    }

    public Board GetBoard() => _game.Board;

    public IReadOnlyList<Move> GetPlayedMoves() => _game.History;

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

    public Move? GetLastMove() => _lastMove;

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

    public void ClearMoveSelection() => ClearSelection();

    private void ClearSelection()
    {
        _selected = null;
        _legalMoves = [];
    }
}
