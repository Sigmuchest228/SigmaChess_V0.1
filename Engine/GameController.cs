namespace SigmaChess.Engine;

/// <summary>Оркестрация партии: выбор, легальные ходы, исполнение через <see cref="Game"/>.</summary>
public class GameController
{
    private readonly MoveGenerator _moveGenerator = new();
    private readonly GameRules _rules;
    private Game _game = new();
    private Position? _selected;
    private List<Move> _legalMoves = [];

    public GameController()
    {
        _rules = new GameRules(_moveGenerator);
    }

    public void InitializeGame()
    {
        _game = new();
        ClearSelection();
    }

    public void HandleCellClick(int row, int col)
    {
        var to = new Position(row, col);
        var board = _game.Board;

        if (TryExecuteMoveTo(to, board))
        {
            return;
        }

        var tapped = board.GetPiece(to);
        if (tapped is not null && tapped.Color == _game.CurrentTurn)
        {
            _selected = to;
            _legalMoves = [.._rules.GetLegalMovesFrom(board, to)];
            return;
        }

        ClearSelection();
    }

    public Board GetBoard() => _game.Board;

    public IReadOnlyList<(int Row, int Col)> GetHighlightedSquares() =>
        _legalMoves.Select(m => (m.To.Row, m.To.Col)).ToList();

    public Position? GetSelectedSquare() => _selected;

    public PieceColor GetCurrentTurn() => _game.CurrentTurn;

    public GameResult GetGameResult() => _rules.GetGameResult(_game.Board, _game.CurrentTurn);

    private bool TryExecuteMoveTo(Position to, Board board)
    {
        if (_selected is null)
        {
            return false;
        }

        var from = _selected;
        if (!_legalMoves.Any(m => m.To.Row == to.Row && m.To.Col == to.Col))
        {
            return false;
        }

        var move = new Move(from, to);
        if (_game.MakeMove(move))
        {
            ClearSelection();
            return true;
        }

        return false;
    }

    private void ClearSelection()
    {
        _selected = null;
        _legalMoves = [];
    }
}
