using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using SigmaChess.Engine;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

public sealed class PuzzleSolveViewModel : ViewModelBase
{
    private readonly GameController _controller = new();
    private readonly BoardLayoutService _layoutService;
    private readonly FirebaseSyncRepository _firebaseSync;

    private string? _puzzleId;
    private string? _lastLoadedId;
    private Move? _solutionMove;
    private bool _boardLocked;
    private string _headerText = "Puzzle";
    private string _statusText = string.Empty;
    private double _boardExtent = 320;

    public PuzzleSolveViewModel(BoardLayoutService layoutService, FirebaseSyncRepository firebaseSync)
    {
        _layoutService = layoutService;
        _firebaseSync = firebaseSync;

        GoBackCommand = new Command(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
            }
        });

        DeviceDisplay.MainDisplayInfoChanged += (_, _) => UpdateBoardExtent();
    }

    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    public ICommand GoBackCommand { get; }

    public string HeaderText
    {
        get => _headerText;
        private set
        {
            if (_headerText == value)
            {
                return;
            }

            _headerText = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

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
            OnPropertyChanged(nameof(BoardGridSize));
            OnPropertyChanged(nameof(PieceFontSize));
            OnPropertyChanged(nameof(CoordFontSize));
        }
    }

    private const double CoordStrip = 28;

    public double BoardGridSize => BoardExtent + CoordStrip;

    public double PieceFontSize => Math.Clamp(BoardExtent / 8.0 * 0.62, 14, 44);

    public double CoordFontSize => Math.Clamp(BoardExtent * 0.045, 10, 15);

    public void ApplyNavigationQuery(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("PuzzleId", out var raw))
        {
            return;
        }

        var s = raw as string ?? raw?.ToString();
        if (string.IsNullOrWhiteSpace(s))
        {
            return;
        }

        var trimmed = s.Trim();
        if (!string.Equals(_puzzleId, trimmed, StringComparison.Ordinal))
        {
            _lastLoadedId = null;
        }

        _puzzleId = trimmed;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        UpdateBoardExtent();
        EnsureCellsCreated();

        if (string.IsNullOrEmpty(_puzzleId))
        {
            StatusText = "No puzzle selected.";
            RefreshBoard();
            return;
        }

        if (string.Equals(_lastLoadedId, _puzzleId, StringComparison.Ordinal))
        {
            RefreshBoard();
            return;
        }

        try
        {
            var dto = await _firebaseSync.GetPuzzleByIdAsync(_puzzleId, cancellationToken).ConfigureAwait(false);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Fen))
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HeaderText = "Puzzle";
                    StatusText = "Puzzle not found.";
                    _boardLocked = true;
                    RefreshBoard();
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var solutionMove = SolutionToMove(dto.Solution);
            if (solutionMove is null || !_controller.TryLoadFromFen(dto.Fen.Trim()))
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HeaderText = string.IsNullOrWhiteSpace(dto.Title) ? _puzzleId! : dto.Title.Trim();
                    StatusText = "Invalid puzzle data.";
                    _boardLocked = true;
                    RefreshBoard();
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _solutionMove = solutionMove;
                _boardLocked = false;
                HeaderText = string.IsNullOrWhiteSpace(dto.Title) ? _puzzleId! : dto.Title.Trim();
                StatusText = "Find the winning move.";
                _lastLoadedId = _puzzleId;
                RefreshBoard();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HeaderText = "Puzzle";
                StatusText = "Failed to load puzzle.";
                _boardLocked = true;
                RefreshBoard();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task OnCellTappedAsync(BoardCellViewModel cell)
    {
        if (_boardLocked || _solutionMove is null)
        {
            return;
        }

        var solution = _solutionMove;

        var pending = _controller.GetPendingMove(cell.Row, cell.Col);
        if (pending is null)
        {
            _controller.HandleSelection(cell.Row, cell.Col);
            RefreshBoard();
            return;
        }

        var board = _controller.GetBoard();
        var moverPiece = board.GetPiece(pending.From);
        if (moverPiece is null)
        {
            return;
        }

        if (moverPiece.Type == PieceType.Pawn && IsPromotionRank(moverPiece.Color, pending.To.Row))
        {
            var promo = solution.Promotion ?? PieceType.Queen;
            pending = pending with { Promotion = promo };
        }

        if (MovesMatch(pending, solution))
        {
            _controller.ExecutePlannedMove(pending);
            RefreshBoard();
            await RecordSolveAsync().ConfigureAwait(false);
        }
        else
        {
            StatusText = "Not quite — try again.";
            _controller.ClearMoveSelection();
            RefreshBoard();
        }
    }

    private async Task RecordSolveAsync()
    {
        var msg = "Correct!";
        if (!string.IsNullOrEmpty(_puzzleId))
        {
            try
            {
                var firstTime = await _firebaseSync.TryMarkPuzzleSolvedFirstTimeAsync(_puzzleId).ConfigureAwait(false);
                msg = firstTime ? "Correct! Rank progress saved." : "Correct! (already solved)";
            }
            catch
            {
                msg = "Correct!";
            }
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _boardLocked = true;
            StatusText = msg;
        }).ConfigureAwait(false);
    }

    private static bool MovesMatch(Move attempted, Move solution) =>
        attempted.From == solution.From
        && attempted.To == solution.To
        && Nullable.Equals(attempted.Promotion, solution.Promotion);

    private static bool IsPromotionRank(PieceColor color, int row) =>
        color == PieceColor.White ? row == 0 : row == 7;

    private static PieceType? ParsePromotionPiece(string? dtoPromotion)
    {
        if (string.IsNullOrWhiteSpace(dtoPromotion))
        {
            return null;
        }

        return dtoPromotion.Trim().ToLowerInvariant() switch
        {
            "queen" => PieceType.Queen,
            "rook" => PieceType.Rook,
            "bishop" => PieceType.Bishop,
            "knight" => PieceType.Knight,
            _ => null
        };
    }

    private static Move? SolutionToMove(FirebasePuzzleSolutionDto? s)
    {
        if (s is null
            || !AlgebraicNotation.TryParseSquare(s.FromPos, out var from)
            || !AlgebraicNotation.TryParseSquare(s.ToPos, out var to))
        {
            return null;
        }

        var promo = ParsePromotionPiece(s.Promotion);
        return new Move(from, to, promo);
    }

    private void UpdateBoardExtent()
    {
        BoardExtent = _layoutService.CalculateBoardExtentForGamePage(
            DeviceDisplay.Current.MainDisplayInfo,
            faceToFaceLayout: false);
    }

    private void EnsureCellsCreated()
    {
        if (Cells.Count == 64)
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

    public void RefreshBoard()
    {
        var board = _controller.GetBoard();
        var selected = _controller.GetSelectedSquare();
        var highlightSet = new HashSet<(int R, int C)>(
            _controller.GetHighlightedSquares().Select(h => (h.Row, h.Col)));
        var turn = _controller.GetCurrentTurn();
        var lastMove = _controller.GetLastMove();
        var showLastMove = lastMove is not null && selected is null;

        foreach (var cell in Cells)
        {
            var pos = new Position(cell.Row, cell.Col);
            cell.Piece = board.GetPiece(pos);
            cell.IsSelected = selected == pos;
            cell.PieceGlyphRotation = 0;

            var isMoveTarget = highlightSet.Contains((cell.Row, cell.Col));
            if (isMoveTarget)
            {
                cell.IsHighlighted = true;
                var targetPiece = board.GetPiece(pos);
                var isCapture = targetPiece is not null && targetPiece.Color != turn;
                cell.MoveTarget = isCapture ? MoveTargetHighlight.Capture : MoveTargetHighlight.ToEmpty;
                continue;
            }

            cell.MoveTarget = MoveTargetHighlight.None;
            cell.IsHighlighted = showLastMove && lastMove is not null && IsLastMoveSquare(lastMove, cell.Row, cell.Col);
        }
    }

    private static bool IsLastMoveSquare(Move move, int row, int col)
    {
        var pos = new Position(row, col);
        return move.From == pos || move.To == pos;
    }
}
