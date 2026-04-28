using System.Collections.ObjectModel;
using Microsoft.Maui.Devices;
using SigmaChess.Engine;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

public class GameViewModel : ViewModelBase
{
    private readonly global::SigmaChess.Engine.GameController _controller;
    private readonly BoardLayoutService _layoutService;
    private double _boardExtent = 320;
    private string _gameStatusText = string.Empty;
    private string _gameResultText = string.Empty;
    private const double RankStrip = 28;
    private const double FileStrip = 28;
    private const double RankBoardSpacing = 4;

    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    public Command<BoardCellViewModel> CellTappedCommand { get; }

    public string CurrentTurnText =>
        _controller.GetCurrentTurn() == PieceColor.White ? "Ход: Белые" : "Ход: Чёрные";

    public string GameStatusText => _gameStatusText;

    public string GameResultText => _gameResultText;

    public double BoardExtent
    {
        get => _boardExtent;
        private set
        {
            if (Math.Abs(_boardExtent - value) < 0.5)
            {
                return;
            }

            _boardExtent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BoardGridWidth));
            OnPropertyChanged(nameof(BoardGridHeight));
            OnPropertyChanged(nameof(PieceFontSize));
            OnPropertyChanged(nameof(CoordFontSize));
        }
    }

    public double BoardGridWidth => BoardExtent + RankStrip + RankBoardSpacing;

    public double BoardGridHeight => BoardExtent + FileStrip;

    public double PieceFontSize => Math.Clamp(BoardExtent / 8.0 * 0.62, 14, 44);

    public double CoordFontSize => Math.Clamp(BoardExtent * 0.045, 10, 15);

    public GameViewModel(global::SigmaChess.Engine.GameController controller, BoardLayoutService layoutService)
    {
        _controller = controller;
        _layoutService = layoutService;
        CellTappedCommand = new Command<BoardCellViewModel>(OnCellTapped);
        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
        UpdateBoardExtent();
        InitializeBoard();
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        UpdateBoardExtent();
    }

    private void UpdateBoardExtent()
    {
        BoardExtent = _layoutService.CalculateBoardExtent(DeviceDisplay.Current.MainDisplayInfo);
    }

    /// <summary>Создаёт 64 клетки один раз и запускает новую партию.</summary>
    public void InitializeBoard()
    {
        _controller.InitializeGame();
        EnsureCellsCreated();
        RefreshBoard();
    }

    /// <summary>
    /// Создаёт ровно 64 клетки в порядке «строка за строкой» (row-major),
    /// чтобы они корректно раскладывались в <see cref="GridItemsLayout"/> (Orientation=Vertical, Span=8).
    /// </summary>
    private void EnsureCellsCreated()
    {
        var orderOk = Cells.Count == 64 &&
                      Cells[0].Row == 0 && Cells[0].Col == 0 &&
                      Cells[1].Row == 0 && Cells[1].Col == 1;
        if (orderOk)
        {
            return;
        }

        Cells.Clear();
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                Cells.Add(new BoardCellViewModel(row, col));
            }
        }
    }

    public void OnCellTapped(BoardCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        _controller.HandleCellClick(cell.Row, cell.Col);
        RefreshBoard();
    }

    /// <summary>Синхронизирует коллекцию клеток с <see cref="GameController"/> (доска, выбор, подсветка, текст).</summary>
    public void RefreshBoard()
    {
        var board = _controller.GetBoard();
        var selected = _controller.GetSelectedSquare();
        var highlightSet = new HashSet<(int R, int C)>(
            _controller.GetHighlightedSquares().Select(h => (h.Row, h.Col)));
        var turn = _controller.GetCurrentTurn();

        foreach (var cell in Cells)
        {
            cell.Piece = board.GetPiece(new Position(cell.Row, cell.Col));

            cell.IsSelected = selected is not null &&
                              selected.Row == cell.Row &&
                              selected.Col == cell.Col;

            var isMoveTarget = highlightSet.Contains((cell.Row, cell.Col));
            cell.IsHighlighted = isMoveTarget;

            if (!isMoveTarget)
            {
                cell.MoveTarget = MoveTargetHighlight.None;
                continue;
            }

            var targetPiece = board.GetPiece(new Position(cell.Row, cell.Col));
            var isCapture = targetPiece is not null && targetPiece.Color != turn;
            cell.MoveTarget = isCapture ? MoveTargetHighlight.Capture : MoveTargetHighlight.ToEmpty;
        }

        OnPropertyChanged(nameof(CurrentTurnText));
        RefreshStatusLabels();
    }

    private void RefreshStatusLabels()
    {
        var result = _controller.GetGameResult();
        var turn = _controller.GetCurrentTurn();

        _gameResultText = result switch
        {
            GameResult.Checkmate => turn == PieceColor.White ? "Black wins" : "White wins",
            GameResult.Stalemate => "Draw",
            _ => string.Empty
        };

        _gameStatusText = result switch
        {
            GameResult.Ongoing => string.Empty,
            GameResult.Check => "Шах",
            GameResult.Checkmate => string.Empty,
            GameResult.Stalemate => string.Empty,
            _ => string.Empty
        };

        OnPropertyChanged(nameof(GameStatusText));
        OnPropertyChanged(nameof(GameResultText));
    }
}
