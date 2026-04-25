using System.Collections.ObjectModel;
using Microsoft.Maui.Devices;
using SigmaChess.Engine;

namespace SigmaChess.ViewModels;

public class GameViewModel : ViewModelBase
{
    private readonly GameController _controller = new();
    private double _boardExtent = 320;
    private string _gameStatusText = string.Empty;
    private const double RankStrip = 28;
    private const double FileStrip = 28;
    private const double RankBoardSpacing = 4;

    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    public Command<BoardCellViewModel> CellTappedCommand { get; }

    public string CurrentTurnText =>
        _controller.GetCurrentTurn() == PieceColor.White ? "Ход: Белые" : "Ход: Черные";

    public string GameStatusText => _gameStatusText;

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

    public GameViewModel()
    {
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
        var info = DeviceDisplay.Current.MainDisplayInfo;
        var density = info.Density <= 0 ? 1 : info.Density;
        var w = info.Width / density;
        var h = info.Height / density;
        var reserved = 220;
        var side = Math.Min(w - 24, h - reserved);
        BoardExtent = Math.Clamp(side, 260, 640);
    }

    public void InitializeBoard()
    {
        _controller.InitializeGame();
        RebuildCellCollection();
        ApplyVisualStateFromController();
    }

    private void RebuildCellCollection()
    {
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
        ApplyVisualStateFromController();
    }

    private void ApplyVisualStateFromController()
    {
        var board = _controller.GetBoard();
        var selected = _controller.GetSelectedSquare();
        var highlightTargets = new HashSet<(int R, int C)>(
            _controller.GetHighlightedSquares().Select(h => (h.Row, h.Col)));
        var turn = _controller.GetCurrentTurn();

        foreach (var cell in Cells)
        {
            cell.Piece = board.GetPiece(new Position(cell.Row, cell.Col));

            cell.IsSelected = selected is not null &&
                              selected.Row == cell.Row &&
                              selected.Col == cell.Col;

            if (!highlightTargets.Contains((cell.Row, cell.Col)))
            {
                cell.MoveTarget = MoveTargetHighlight.None;
                continue;
            }

            var target = board.GetPiece(new Position(cell.Row, cell.Col));
            var isCapture = target is not null && target.Color != turn;
            cell.MoveTarget = isCapture ? MoveTargetHighlight.Capture : MoveTargetHighlight.ToEmpty;
        }

        OnPropertyChanged(nameof(CurrentTurnText));
        RefreshStatusText();
    }

    private void RefreshStatusText()
    {
        var result = _controller.GetGameResult();
        var turn = _controller.GetCurrentTurn();

        _gameStatusText = result switch
        {
            GameResult.Ongoing => string.Empty,
            GameResult.Check => "Шах",
            GameResult.Stalemate => "Пат",
            GameResult.Checkmate => turn == PieceColor.White
                ? "Мат: победа чёрных"
                : "Мат: победа белых",
            _ => string.Empty
        };

        OnPropertyChanged(nameof(GameStatusText));
    }
}
