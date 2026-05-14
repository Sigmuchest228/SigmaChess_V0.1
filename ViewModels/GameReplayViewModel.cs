using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using SigmaChess.Engine;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

/// <summary>Просмотр сохранённой партии по <c>gameId</c>: загрузка ходов из RTDB и пошаговый реплей на движке.</summary>
/// <remarks>
/// В отличие от минимальных примеров MVVM, здесь есть <see cref="FirebaseSyncRepository"/> и разбор ходов через <see cref="GameReplayMoveResolver"/> —
/// это не «магия», а необходимость приложения с облаком.
/// </remarks>
public class GameReplayViewModel : ViewModelBase
{
    private readonly global::SigmaChess.Engine.GameController _controller = new();
    private readonly BoardLayoutService _layoutService;
    private readonly FirebaseSyncRepository _firebaseSync;

    private string? _gameId;
    private string? _lastLoadedGameId;
    private int _replayPlies;
    private double _boardExtent = 320;
    private string _headerText = "Replay";
    private string _winnerOutcomeText = string.Empty;
    private Color _winnerOutcomeColor = ChessOutcomePalette.TextForWinner(string.Empty);
    private string _replaySubtitleTail = string.Empty;

    public GameReplayViewModel()
        : this(AppService.GetInstance().BoardLayout, AppService.GetInstance().FirebaseSync)
    {
    }

    public GameReplayViewModel(BoardLayoutService layoutService, FirebaseSyncRepository firebaseSync)
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

        StepFirstCommand = new Command(() => StepTo(0), () => _replayPlies > 0);
        StepPrevCommand = new Command(() => StepTo(_replayPlies - 1), () => _replayPlies > 0);
        StepNextCommand = new Command(
            () => StepTo(_replayPlies + 1),
            () => _replayPlies < _controller.GetPlayedMoves().Count);
        StepLastCommand = new Command(
            () => StepTo(_controller.GetPlayedMoves().Count),
            () => _replayPlies < _controller.GetPlayedMoves().Count);

        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
    }

    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    public ObservableCollection<MoveHistoryRow> MoveRows { get; } = [];

    public ICommand GoBackCommand { get; }

    public Command StepFirstCommand { get; }

    public Command StepPrevCommand { get; }

    public Command StepNextCommand { get; }

    public Command StepLastCommand { get; }

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

    public string WinnerOutcomeText
    {
        get => _winnerOutcomeText;
        private set
        {
            if (_winnerOutcomeText == value)
            {
                return;
            }

            _winnerOutcomeText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasWinnerCaption));
        }
    }

    public bool HasWinnerCaption => !string.IsNullOrEmpty(_winnerOutcomeText);

    public Color WinnerOutcomeColor
    {
        get => _winnerOutcomeColor;
        private set
        {
            if (_winnerOutcomeColor == value)
            {
                return;
            }

            _winnerOutcomeColor = value;
            OnPropertyChanged();
        }
    }

    public string ReplaySubtitleTail
    {
        get => _replaySubtitleTail;
        private set
        {
            if (_replaySubtitleTail == value)
            {
                return;
            }

            _replaySubtitleTail = value;
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
        if (!query.TryGetValue("GameId", out var raw))
        {
            return;
        }

        var s = raw as string ?? raw?.ToString();
        if (string.IsNullOrWhiteSpace(s))
        {
            return;
        }

        var trimmed = s.Trim();
        if (!string.Equals(_gameId, trimmed, StringComparison.Ordinal))
        {
            _lastLoadedGameId = null;
        }

        _gameId = trimmed;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        UpdateBoardExtent();
        EnsureCellsCreated();

        if (string.IsNullOrEmpty(_gameId))
        {
            ClearReplayChrome();
            RefreshBoard();
            return;
        }

        if (string.Equals(_lastLoadedGameId, _gameId, StringComparison.Ordinal))
        {
            RefreshBoard();
            return;
        }

        try
        {
            var record = await _firebaseSync.GetChessGameByIdAsync(_gameId, cancellationToken).ConfigureAwait(false);
            if (record is null || record.Moves.Count == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HeaderText = "Replay";
                    ApplyErrorReplayChrome("Game not found or has no moves.");
                    _controller.InitializeGame();
                    _replayPlies = 0;
                    MoveRows.Clear();
                    RefreshBoard();
                    NotifyStepCommands();
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var ordered = record.Moves
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Value)
                .ToList();

            if (!GameReplayMoveResolver.TryResolve(ordered, out var engineMoves)
                || !_controller.TryReplayMoves(engineMoves))
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    HeaderText = "Replay";
                    ApplyErrorReplayChrome("Could not rebuild moves from saved data.");
                    _controller.InitializeGame();
                    _replayPlies = 0;
                    MoveRows.Clear();
                    RefreshBoard();
                    NotifyStepCommands();
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HeaderText = "Game replay";
                ApplySuccessReplayChrome(record.Winner, record.EndReason ?? string.Empty);
                RebuildMoveRowsFromHistory();
                _replayPlies = 0;
                _lastLoadedGameId = _gameId;
                RefreshBoard();
                NotifyStepCommands();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HeaderText = "Replay";
                ApplyErrorReplayChrome("Failed to load game.");
                _controller.InitializeGame();
                _replayPlies = 0;
                MoveRows.Clear();
                RefreshBoard();
                NotifyStepCommands();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ClearReplayChrome()
    {
        WinnerOutcomeText = string.Empty;
        WinnerOutcomeColor = ChessOutcomePalette.TextForWinner(string.Empty);
        ReplaySubtitleTail = string.Empty;
    }

    private void ApplyErrorReplayChrome(string message)
    {
        WinnerOutcomeText = string.Empty;
        WinnerOutcomeColor = ChessOutcomePalette.TextForWinner(string.Empty);
        ReplaySubtitleTail = message;
    }

    private void ApplySuccessReplayChrome(string winnerRaw, string endReason)
    {
        var nw = ChessOutcomePalette.NormalizeWinner(winnerRaw);
        var caption = ChessOutcomePalette.ReplayWinnerCaption(nw);
        WinnerOutcomeText = caption;
        WinnerOutcomeColor = ChessOutcomePalette.TextForWinner(nw);
        var humanEnd = HumanEndReason(endReason);
        ReplaySubtitleTail = string.IsNullOrEmpty(caption) ? humanEnd : " · " + humanEnd;
    }

    private static string HumanEndReason(string endReason) =>
        endReason.ToLowerInvariant() switch
        {
            "checkmate" => "Checkmate",
            "stalemate" => "Stalemate",
            "fifty_move" => "50-move rule",
            "repetition" => "Repetition",
            "insufficient_material" => "Insufficient material",
            _ => string.IsNullOrWhiteSpace(endReason) ? "—" : endReason
        };

    private void StepTo(int target)
    {
        var n = _controller.GetPlayedMoves().Count;
        var next = Math.Clamp(target, 0, n);
        if (next == _replayPlies)
        {
            return;
        }

        _replayPlies = next;
        RefreshBoard();
        NotifyStepCommands();
    }

    private void NotifyStepCommands()
    {
        StepFirstCommand.ChangeCanExecute();
        StepPrevCommand.ChangeCanExecute();
        StepNextCommand.ChangeCanExecute();
        StepLastCommand.ChangeCanExecute();
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => UpdateBoardExtent();

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

    private void RebuildMoveRowsFromHistory()
    {
        MoveRows.Clear();
        var moves = _controller.GetPlayedMoves();
        for (var i = 0; i < moves.Count; i += 2)
        {
            var white = AlgebraicNotation.MoveToShortNotation(moves[i], moves, i);
            var black = i + 1 < moves.Count ? AlgebraicNotation.MoveToShortNotation(moves[i + 1], moves, i + 1) : string.Empty;
            MoveRows.Add(new MoveHistoryRow
            {
                FullMoveNumber = i / 2 + 1,
                WhiteMove = white,
                BlackMove = black
            });
        }
    }

    public void RefreshBoard()
    {
        var history = _controller.GetPlayedMoves();
        if (_replayPlies > history.Count)
        {
            _replayPlies = history.Count;
        }

        var board = _controller.GetBoardAfterPlies(_replayPlies);
        Move? lastMove = _replayPlies > 0 ? history[_replayPlies - 1] : null;
        var showLastMove = lastMove is not null;

        foreach (var cell in Cells)
        {
            var pos = new Position(cell.Row, cell.Col);
            cell.Piece = board.GetPiece(pos);
            cell.IsSelected = false;
            cell.MoveTarget = MoveTargetHighlight.None;
            cell.IsHighlighted = showLastMove && IsLastMoveSquare(lastMove!, cell.Row, cell.Col);
            cell.PieceGlyphRotation = 0;
        }
    }

    private static bool IsLastMoveSquare(Move move, int row, int col)
    {
        var pos = new Position(row, col);
        return move.From == pos || move.To == pos;
    }
}
