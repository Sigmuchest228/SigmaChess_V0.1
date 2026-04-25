using System.Collections.ObjectModel;
using SigmaChess.Engine;

namespace SigmaChess.ViewModels;

public class GameViewModel : ViewModelBase
{
    private readonly Game _game = new();
    private readonly MoveGenerator _moveGenerator = new();
    private Position? _selectedPosition;
    private List<Move> _possibleMoves = [];

    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    public Command<BoardCellViewModel> CellTappedCommand { get; }

    public string CurrentTurnText => _game.CurrentTurn == PieceColor.White ? "Ход: Белые" : "Ход: Черные";

    public GameViewModel()
    {
        CellTappedCommand = new Command<BoardCellViewModel>(OnCellTapped);
        InitializeBoard();
    }

    public void InitializeBoard()
    {
        Cells.Clear();

        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                var cell = new BoardCellViewModel(row, col)
                {
                    Piece = _game.Board.GetPiece(new Position(row, col))
                };

                Cells.Add(cell);
            }
        }
    }

    public void OnCellTapped(BoardCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        if (TryMakeMoveTo(cell))
        {
            RefreshBoardState();
            return;
        }

        var tappedPiece = cell.Piece;
        if (tappedPiece is not null && tappedPiece.Color == _game.CurrentTurn)
        {
            _selectedPosition = new Position(cell.Row, cell.Col);
            _possibleMoves = _moveGenerator.GetPossibleMoves(_game.Board, _selectedPosition);
            HighlightPossibleMoves();
            return;
        }

        ClearSelectionAndHighlights();
    }

    public void HighlightPossibleMoves()
    {
        foreach (var boardCell in Cells)
        {
            boardCell.IsSelected = _selectedPosition is not null &&
                                   boardCell.Row == _selectedPosition.Row &&
                                   boardCell.Col == _selectedPosition.Col;

            boardCell.IsHighlighted = _possibleMoves.Any(move =>
                move.To.Row == boardCell.Row &&
                move.To.Col == boardCell.Col);
        }
    }

    private bool TryMakeMoveTo(BoardCellViewModel targetCell)
    {
        if (_selectedPosition is null)
        {
            return false;
        }

        var isHighlightedTarget = _possibleMoves.Any(move =>
            move.To.Row == targetCell.Row &&
            move.To.Col == targetCell.Col);

        if (!isHighlightedTarget)
        {
            return false;
        }

        var move = new Move(_selectedPosition, new Position(targetCell.Row, targetCell.Col));
        return _game.MakeMove(move);
    }

    private void RefreshBoardState()
    {
        foreach (var boardCell in Cells)
        {
            boardCell.Piece = _game.Board.GetPiece(new Position(boardCell.Row, boardCell.Col));
            boardCell.IsHighlighted = false;
            boardCell.IsSelected = false;
        }

        _selectedPosition = null;
        _possibleMoves = [];
        OnPropertyChanged(nameof(CurrentTurnText));
    }

    private void ClearSelectionAndHighlights()
    {
        _selectedPosition = null;
        _possibleMoves = [];

        foreach (var boardCell in Cells)
        {
            boardCell.IsHighlighted = false;
            boardCell.IsSelected = false;
        }
    }
}
