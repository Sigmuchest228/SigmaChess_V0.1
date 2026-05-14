using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Dispatching;
using CommunityToolkit.Maui.Views;
using SigmaChess.Engine;
using SigmaChess.Models;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

// Экран партии толще учебного LoginBaseApp: движок, таймеры, запись в Firebase — сигнатуры методов длиннее, это ожидаемо.

/// <summary>
/// ViewModel страницы игры на одном устройстве. Связывает <see cref="SigmaChess.Engine.GameController"/>
/// (движок) с UI-коллекцией из 64 ячеек, обрабатывает тапы пользователя, рассчитывает
/// размер доски под экран и хранит пользовательские настройки партии.
/// </summary>
public class GameViewModel : ViewModelBase
{
    private readonly global::SigmaChess.Engine.GameController _controller;
    private readonly BoardLayoutService _layoutService;
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;
    private readonly List<SavedMove> _moveHistory = [];
    private readonly Stopwatch _moveStopwatch = new();
    private bool _gameSaved;
    private double _boardExtent = 320;
    private string _gameStatusText = string.Empty;
    private string _gameResultText = string.Empty;
    private string _timeoutResultText = string.Empty;
    private bool _isInitialized;
    private bool _autoFlipEnabled;
    private bool _highlightLastMoveEnabled = true;
    private bool _autoQueenEnabled;
    private bool _isBoardFlipped;
    private bool _needsInitialTimePopup = true;
    private int _replayPliesApplied;
    private IDispatcherTimer? _clockTimer;
    private DateTime _lastClockUtc;
    private bool _unlimitedTime = true;
    private int _minutesWhite = 5;
    private int _minutesBlack = 5;
    private GameLayoutMode _layoutMode = GameLayoutMode.Casual;
    private TimeSpan _whiteRemaining;
    private TimeSpan _blackRemaining;
    private PieceColor? _timeoutLoser;
    private bool _gameOverPopupShown;
    private bool _shellHomeNavRegistered;

    /// <summary>Первая сессия без выбора времени: показать попап на GamePage.</summary>
    public bool NeedsInitialTimePopup
    {
        get => _needsInitialTimePopup;
        private set
        {
            if (_needsInitialTimePopup == value)
            {
                return;
            }

            _needsInitialTimePopup = value;
            OnPropertyChanged();
        }
    }

    // Полоса с координатами файлов/рангов вокруг доски (по 28 px на одну сторону).
    // Используется только для расчёта размера внешнего Grid.
    private const double CoordStrip = 28;

    /// <summary>64 клетки доски в порядке (row=0,col=0)...(row=7,col=7).</summary>
    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    /// <summary>Строки записи ходов для списка справа от доски.</summary>
    public ObservableCollection<MoveHistoryRow> MoveRows { get; } = [];

    public Command<BoardCellViewModel> CellTappedCommand { get; }

    public ICommand BackToMenuCommand { get; }

    public Command StepBackwardCommand { get; }

    public Command StepForwardCommand { get; }

    public string CurrentTurnText =>
        _controller.GetCurrentTurn() == PieceColor.White ? "White to move" : "Black to move";

    public string GameStatusText => _gameStatusText;

    public string GameResultText => _gameResultText;

    public string WhiteClockText { get; private set; } = "—";

    public string BlackClockText { get; private set; } = "—";

    public bool CanStepBackward => _replayPliesApplied > 0;

    public bool CanStepForward => _replayPliesApplied < _controller.GetPlayedMoves().Count;

    /// <summary>Сторона квадрата самой доски в DIP. Меняется при ротации/смене окна.</summary>
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

    /// <summary>Сторона внешнего квадрата (доска + полоса координат с двух сторон).</summary>
    public double BoardGridSize => BoardExtent + CoordStrip;

    public double PieceFontSize => Math.Clamp(BoardExtent / 8.0 * 0.62, 14, 44);

    public double CoordFontSize => Math.Clamp(BoardExtent * 0.045, 10, 15);

    public bool AutoFlipEnabled
    {
        get => _autoFlipEnabled;
        set
        {
            if (_autoFlipEnabled == value)
            {
                return;
            }

            _autoFlipEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool HighlightLastMoveEnabled
    {
        get => _highlightLastMoveEnabled;
        set
        {
            if (_highlightLastMoveEnabled == value)
            {
                return;
            }

            _highlightLastMoveEnabled = value;
            OnPropertyChanged();
            RefreshBoard();
        }
    }

    public bool AutoQueenEnabled
    {
        get => _autoQueenEnabled;
        set
        {
            if (_autoQueenEnabled == value)
            {
                return;
            }

            _autoQueenEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsBoardFlipped
    {
        get => _isBoardFlipped;
        private set
        {
            if (_isBoardFlipped == value)
            {
                return;
            }

            _isBoardFlipped = value;
            OnPropertyChanged();
        }
    }

    public GameLayoutMode LayoutMode
    {
        get => _layoutMode;
        private set
        {
            if (_layoutMode == value)
            {
                return;
            }

            _layoutMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFaceToFaceLayout));
            OnPropertyChanged(nameof(IsCasualLayout));
            UpdateBoardExtent();
        }
    }

    public bool IsFaceToFaceLayout => _layoutMode == GameLayoutMode.FaceToFace;

    public bool IsCasualLayout => _layoutMode == GameLayoutMode.Casual;

    public GameViewModel(
        global::SigmaChess.Engine.GameController controller,
        BoardLayoutService layoutService,
        AppService appService,
        FirebaseSyncRepository firebaseSync)
    {
        _controller = controller;
        _layoutService = layoutService;
        _appService = appService;
        _firebaseSync = firebaseSync;

        CellTappedCommand = new Command<BoardCellViewModel>(
            async cell => await OnCellTappedAsync(cell),
            _ => !IsPlayLocked());
        BackToMenuCommand = new Command(async () => await GoBackToMenuAsync());
        StepBackwardCommand = new Command(StepBackward, () => CanStepBackward);
        StepForwardCommand = new Command(StepForward, () => CanStepForward);

        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
    }

    /// <summary>Ячейки и размер доски без старта партии.</summary>
    public Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            // Повторный заход на GamePage: всё ещё обновляем размер доски под текущее окно
            // (иначе на WinUI Grid может отрисоваться с нулевыми строками/колонками).
            UpdateBoardExtent();
            return Task.CompletedTask;
        }

        UpdateBoardExtent();
        EnsureCellsCreated();
        // Если пользователь уже играл (есть ходы), попап выбора времени при входе не нужен.
        if (_controller.GetPlayedMoves().Count > 0)
        {
            NeedsInitialTimePopup = false;
            _replayPliesApplied = _controller.GetPlayedMoves().Count;
            RebuildMoveRowsFromHistory();
            WhiteClockText = _unlimitedTime ? "—" : FormatClock(_whiteRemaining);
            BlackClockText = _unlimitedTime ? "—" : FormatClock(_blackRemaining);
            OnPropertyChanged(nameof(WhiteClockText));
            OnPropertyChanged(nameof(BlackClockText));
            RestartClockForCurrentGame();
        }

        _isInitialized = true;
        return Task.CompletedTask;
    }

    /// <summary>Регистрирует обработчик <see cref="OnShellNavigatedForHomeFromGame"/> (один раз).</summary>
    private void TryRegisterShellHomeNavigationHandler()
    {
        if (_shellHomeNavRegistered || Shell.Current is null)
        {
            return;
        }

        Shell.Current.Navigated += OnShellNavigatedForHomeFromGame;
        _shellHomeNavRegistered = true;
    }

    private void OnShellNavigatedForHomeFromGame(object? sender, ShellNavigatedEventArgs e)
    {
        var prev = e.Previous?.Location?.OriginalString ?? string.Empty;
        var cur = e.Current?.Location?.OriginalString ?? string.Empty;
        if (prev.Length == 0 || cur.Length == 0)
        {
            return;
        }

        if (!cur.Contains("MainPage", StringComparison.OrdinalIgnoreCase)
            || !prev.Contains("GamePage", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StopClockTimer();
            ResetGameStateWhenNavigatingHome();
        });
    }

    /// <summary>Показать попап выбора времени при первом заходе на пустую партию.</summary>
    public bool ShouldOfferTimeSetupOnAppear() =>
        _controller.GetPlayedMoves().Count == 0 && NeedsInitialTimePopup;

    /// <summary>
    /// Полный сброс singleton-состояния партии при переходе с GamePage на главную
    /// (обработчик <see cref="Shell.Navigated"/>).
    /// </summary>
    private void ResetGameStateWhenNavigatingHome()
    {
        InternalResetGameState();
        NeedsInitialTimePopup = true;
        LayoutMode = GameLayoutMode.Casual;
        _unlimitedTime = true;
        _minutesWhite = 5;
        _minutesBlack = 5;
        ResetClocksFromTimeControl();
        RefreshBoard();
        OnPropertyChanged(nameof(CurrentTurnText));
        CellTappedCommand.ChangeCanExecute();
        StepBackwardCommand.ChangeCanExecute();
        StepForwardCommand.ChangeCanExecute();
    }

    /// <summary>Полный сброс партии и настроек до дефолта (выход из аккаунта).</summary>
    public void ResetSessionForLogout()
    {
        StopClockTimer();
        InternalResetGameState();
        NeedsInitialTimePopup = true;
        LayoutMode = GameLayoutMode.Casual;
        _unlimitedTime = true;
        _minutesWhite = 5;
        _minutesBlack = 5;
        ResetClocksFromTimeControl();
        RefreshBoard();
        OnPropertyChanged(nameof(CurrentTurnText));
        CellTappedCommand.ChangeCanExecute();
        StepBackwardCommand.ChangeCanExecute();
        StepForwardCommand.ChangeCanExecute();
    }

    public void ApplyTimeControl(NewGameSetupResult result)
    {
        _unlimitedTime = result.Unlimited;
        _minutesWhite = Math.Clamp(result.WhiteMinutes, 1, 180);
        _minutesBlack = Math.Clamp(result.BlackMinutes, 1, 180);
        LayoutMode = result.LayoutMode;
        ResetClocksFromTimeControl();
        NotifyClocks();
    }

    /// <summary>Старт партии после выбора времени (новая игра с нуля).</summary>
    public void StartNewGameAfterSetup()
    {
        InternalResetGameState();
        NeedsInitialTimePopup = false;
        RefreshBoard();
        RestartClockForCurrentGame();
        OnPropertyChanged(nameof(CurrentTurnText));
    }

    private void InternalResetGameState()
    {
        _controller.InitializeGame();
        IsBoardFlipped = false;
        ResetCloudGameTracking();
        _replayPliesApplied = 0;
        _timeoutLoser = null;
        _timeoutResultText = string.Empty;
        _gameOverPopupShown = false;
        MoveRows.Clear();
        RefreshStatusLabels();
        NotifyReplayCommands();
    }

    private void ResetClocksFromTimeControl()
    {
        if (_unlimitedTime)
        {
            WhiteClockText = "—";
            BlackClockText = "—";
        }
        else
        {
            var w = TimeSpan.FromMinutes(_minutesWhite);
            var b = TimeSpan.FromMinutes(_minutesBlack);
            _whiteRemaining = w;
            _blackRemaining = b;
            WhiteClockText = FormatClock(w);
            BlackClockText = FormatClock(b);
        }

        OnPropertyChanged(nameof(WhiteClockText));
        OnPropertyChanged(nameof(BlackClockText));
    }

    private static string FormatClock(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
        {
            t = TimeSpan.Zero;
        }

        var total = (int)t.TotalMinutes;
        return $"{total}:{t.Seconds:D2}";
    }

    private void NotifyClocks()
    {
        OnPropertyChanged(nameof(WhiteClockText));
        OnPropertyChanged(nameof(BlackClockText));
    }

    private void RestartClockForCurrentGame()
    {
        StopClockTimer();
        if (_unlimitedTime || IsEngineTerminal(_controller.GetGameResult()))
        {
            return;
        }

        _lastClockUtc = DateTime.UtcNow;
        var d = Application.Current?.Dispatcher;
        if (d is null)
        {
            return;
        }

        _clockTimer = d.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromMilliseconds(300);
        _clockTimer.Tick += OnClockTimerTick;
        _clockTimer.Start();
    }

    private void StopClockTimer()
    {
        if (_clockTimer is not null)
        {
            _clockTimer.Tick -= OnClockTimerTick;
            _clockTimer.Stop();
            _clockTimer = null;
        }
    }

    private void OnClockTimerTick(object? sender, EventArgs e)
    {
        if (_unlimitedTime
            || _replayPliesApplied < _controller.GetPlayedMoves().Count
            || _timeoutLoser is not null
            || IsEngineTerminal(_controller.GetGameResult()))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var dt = now - _lastClockUtc;
        _lastClockUtc = now;
        if (dt > TimeSpan.FromSeconds(2))
        {
            dt = TimeSpan.FromMilliseconds(500);
        }

        var side = _controller.GetCurrentTurn();
        if (side == PieceColor.White)
        {
            _whiteRemaining -= dt;
            if (_whiteRemaining <= TimeSpan.Zero)
            {
                _whiteRemaining = TimeSpan.Zero;
                _timeoutLoser = PieceColor.White;
                _timeoutResultText = "Black wins on time";
                StopClockTimer();
                RefreshStatusLabels();
                CellTappedCommand.ChangeCanExecute();
                _ = TryShowGameOverPopupIfNeededAsync();
                return;
            }

            WhiteClockText = FormatClock(_whiteRemaining);
        }
        else
        {
            _blackRemaining -= dt;
            if (_blackRemaining <= TimeSpan.Zero)
            {
                _blackRemaining = TimeSpan.Zero;
                _timeoutLoser = PieceColor.Black;
                _timeoutResultText = "White wins on time";
                StopClockTimer();
                RefreshStatusLabels();
                CellTappedCommand.ChangeCanExecute();
                _ = TryShowGameOverPopupIfNeededAsync();
                return;
            }

            BlackClockText = FormatClock(_blackRemaining);
        }

        OnPropertyChanged(nameof(WhiteClockText));
        OnPropertyChanged(nameof(BlackClockText));
    }

    public void OnGamePageAppeared()
    {
        TryRegisterShellHomeNavigationHandler();
        RestartClockForCurrentGame();
    }

    public void OnGamePageDisappeared()
    {
        StopClockTimer();
    }

    private static bool IsEngineTerminal(GameResult r) =>
        r is GameResult.Checkmate
            or GameResult.Stalemate
            or GameResult.DrawFiftyMoveRule
            or GameResult.DrawRepetition
            or GameResult.DrawInsufficientMaterial;

    private bool IsPlayLocked() =>
        _controller.GetPlayedMoves().Count != _replayPliesApplied
        || _timeoutLoser is not null
        || IsEngineTerminal(_controller.GetGameResult());

    public void StartNewGame()
    {
        InternalResetGameState();
        RefreshBoard();
        RestartClockForCurrentGame();
    }

    private void ResetCloudGameTracking()
    {
        _moveHistory.Clear();
        _gameSaved = false;
        _moveStopwatch.Restart();
    }

    /// <summary>
    /// Переход на главную (Shell). После навигации полностью сбрасывает партию в VM;
    /// тот же сброс выполняется в <see cref="OnShellNavigatedForHomeFromGame"/> при других переходах на главную.
    /// </summary>
    public async Task NavigateToMainPageAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                StopClockTimer();
                if (Shell.Current is null)
                {
                    return;
                }

                await Shell.Current.GoToAsync("//MainPage");
                ResetGameStateWhenNavigatingHome();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NavigateToMainPage: {ex}");
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                    await ConfirmPopup.ShowAsync(
                        "Navigation error",
                        "Could not open the home screen.",
                        "OK"));
            }
            catch (Exception ex2)
            {
                Debug.WriteLine($"NavigateToMainPage alert: {ex2}");
            }
        }
    }

    private async Task GoBackToMenuAsync()
    {
        var goHome = await ConfirmPopup.ShowAsync(
            "Leave game?",
            "You may lose progress if you go to the home screen.",
            "Go home",
            "Stay");

        if (!goHome)
        {
            return;
        }

        await NavigateToMainPageAsync();
    }

    /// <summary>Тот же сценарий, что и стрелка «назад» на партии (нижняя панель Home).</summary>
    public Task ConfirmLeaveGameAndGoHomeAsync() => GoBackToMenuAsync();

    /// <summary>Новая партия: только попап контроля времени (без отдельного подтверждения).</summary>
    public async Task StartNewGameWithTimeSetupAsync()
    {
        var page = Shell.Current?.CurrentPage as ContentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page as ContentPage;
        if (page is null)
        {
            Debug.WriteLine("StartNewGameWithTimeSetup: no ContentPage");
            return;
        }

        var popup = new NewGameSetupPopup();
        await page.ShowPopupAsync(popup);
        var result = await popup.WaitForResultAsync();
        if (result is null)
        {
            return;
        }

        ApplyTimeControl(result);
        StartNewGame();
    }

    private void StepBackward()
    {
        if (_replayPliesApplied <= 0)
        {
            return;
        }

        _replayPliesApplied--;
        StopClockTimer();
        RefreshBoard();
        NotifyReplayCommands();
    }

    private void StepForward()
    {
        var n = _controller.GetPlayedMoves().Count;
        if (_replayPliesApplied >= n)
        {
            return;
        }

        _replayPliesApplied++;
        RefreshBoard();
        NotifyReplayCommands();
        if (_replayPliesApplied >= n)
        {
            RestartClockForCurrentGame();
        }
    }

    private void NotifyReplayCommands()
    {
        OnPropertyChanged(nameof(CanStepBackward));
        OnPropertyChanged(nameof(CanStepForward));
        StepBackwardCommand.ChangeCanExecute();
        StepForwardCommand.ChangeCanExecute();
        CellTappedCommand.ChangeCanExecute();
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        UpdateBoardExtent();
    }

    private void UpdateBoardExtent()
    {
        BoardExtent = _layoutService.CalculateBoardExtentForGamePage(
            DeviceDisplay.Current.MainDisplayInfo,
            IsFaceToFaceLayout
                ? GamePageBoardExtentMode.FaceToFace
                : GamePageBoardExtentMode.CasualBottomMoveStrip);
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

    public async Task OnCellTappedAsync(BoardCellViewModel? cell)
    {
        if (cell is null || IsPlayLocked())
        {
            return;
        }

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

        var lastRank = moverPiece.Color == PieceColor.White ? 0 : 7;
        if (moverPiece.Type == PieceType.Pawn && pending.To.Row == lastRank)
        {
            var promo = AutoQueenEnabled
                ? PieceType.Queen
                : await PromotionPopup.ShowAsync(moverPiece.Color);
            pending = pending.WithPromotion(promo);
        }

        var turnBefore = _controller.GetCurrentTurn();
        var executed = _controller.ExecutePlannedMove(pending);
        if (executed)
        {
            var elapsed = _moveStopwatch.Elapsed.TotalSeconds;
            _moveStopwatch.Restart();
            var uid = _appService.CurrentUserId ?? string.Empty;
            var halfIndex = _moveHistory.Count;
            var fullMoveNumber = halfIndex / 2 + 1;
            var resultNow = _controller.GetGameResult();
            _moveHistory.Add(new SavedMove
            {
                FromPos = AlgebraicNotation.ToSquare(pending.From),
                ToPos = AlgebraicNotation.ToSquare(pending.To),
                MoveNumber = fullMoveNumber,
                User = uid,
                TimePerMove = Math.Round(elapsed, 2),
                IsCheckmate = resultNow == GameResult.Checkmate ? true : null
            });
            _replayPliesApplied = _controller.GetPlayedMoves().Count;
            RebuildMoveRowsFromHistory();
        }

        var turnAfter = _controller.GetCurrentTurn();

        if (executed && turnBefore != turnAfter && AutoFlipEnabled)
        {
            IsBoardFlipped = turnAfter == PieceColor.Black;
        }

        RefreshBoard();
        if (IsEngineTerminal(_controller.GetGameResult()))
        {
            StopClockTimer();
        }

        CellTappedCommand.ChangeCanExecute();
        await TrySaveCompletedGameIfTerminalAsync();
        await TryShowGameOverPopupIfNeededAsync();
    }

    private bool IsOverallTerminal() =>
        _timeoutLoser is not null || IsEngineTerminal(_controller.GetGameResult());

    private string GetGameOverSummaryText()
    {
        if (!string.IsNullOrEmpty(_timeoutResultText))
        {
            return _timeoutResultText;
        }

        return _gameResultText;
    }

    private async Task TryShowGameOverPopupIfNeededAsync()
    {
        if (_gameOverPopupShown || !IsOverallTerminal())
        {
            return;
        }

        var message = GetGameOverSummaryText();
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        _gameOverPopupShown = true;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = Shell.Current?.CurrentPage as ContentPage;
                if (page is null)
                {
                    Debug.WriteLine("GameOver: Shell.CurrentPage is not ContentPage");
                    _gameOverPopupShown = false;
                    return;
                }

                try
                {
                    await page.ShowPopupAsync(new GameOverPopup(this, message));
                }
                catch (Exception ex)
                {
                    _gameOverPopupShown = false;
                    Debug.WriteLine($"GameOver ShowPopupAsync: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            _gameOverPopupShown = false;
            Debug.WriteLine($"GameOver: {ex}");
        }
    }

    private async Task TrySaveCompletedGameIfTerminalAsync()
    {
        if (_gameSaved)
        {
            return;
        }

        if (_timeoutLoser is not null)
        {
            return;
        }

        var r = _controller.GetGameResult();
        if (r is GameResult.Ongoing or GameResult.Check)
        {
            return;
        }

        var uid = _appService.CurrentUserId;
        if (string.IsNullOrEmpty(uid) || _moveHistory.Count == 0)
        {
            return;
        }

        try
        {
            await _firebaseSync.EnsureUserProfileAsync().ConfigureAwait(false);
            var winnerColor = FirebaseSyncRepository.ResolveWinnerColor(r, _controller.GetCurrentTurn());
            var endReason = FirebaseSyncRepository.ToEndReason(r);
            var gameId = await _firebaseSync.SaveCompletedGameAsync(
                    uid,
                    uid,
                    winnerColor,
                    endReason,
                    _moveHistory)
                .ConfigureAwait(false);
            if (gameId is not null)
            {
                _gameSaved = true;
            }
        }
        catch
        {
            // Игнорируем сеть
        }
    }

    public void RefreshBoard()
    {
        var history = _controller.GetPlayedMoves();
        if (_replayPliesApplied > history.Count)
        {
            _replayPliesApplied = history.Count;
        }

        var atLive = _replayPliesApplied >= history.Count;

        Board board;
        Position? selected;
        HashSet<(int R, int C)> highlightSet;
        PieceColor turn;
        Move? lastMove;

        if (!atLive)
        {
            board = _controller.GetBoardAfterPlies(_replayPliesApplied);
            selected = null;
            highlightSet = [];
            turn = PieceColor.White;
            lastMove = _replayPliesApplied > 0 ? history[_replayPliesApplied - 1] : null;
        }
        else
        {
            board = _controller.GetBoard();
            selected = _controller.GetSelectedSquare();
            highlightSet = new HashSet<(int R, int C)>(
                _controller.GetHighlightedSquares().Select(h => (h.Row, h.Col)));
            turn = _controller.GetCurrentTurn();
            lastMove = _controller.GetLastMove();
        }

        var showLastMove = lastMove is not null && HighlightLastMoveEnabled && (atLive && selected is null || !atLive);

        var f2fBlackGlyphs = _layoutMode == GameLayoutMode.FaceToFace;

        foreach (var cell in Cells)
        {
            var pos = new Position(cell.Row, cell.Col);
            cell.Piece = board.GetPiece(pos);
            cell.IsSelected = atLive && selected == pos;
            cell.PieceGlyphRotation = f2fBlackGlyphs && cell.Piece?.Color == PieceColor.Black ? 180 : 0;

            if (!atLive)
            {
                cell.MoveTarget = MoveTargetHighlight.None;
                cell.IsHighlighted = showLastMove && IsLastMoveSquare(lastMove!, cell.Row, cell.Col);
                continue;
            }

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

        OnPropertyChanged(nameof(CurrentTurnText));
        RefreshStatusLabels();
        NotifyReplayCommands();
        CellTappedCommand.ChangeCanExecute();
    }

    private static bool IsLastMoveSquare(Move move, int row, int col)
    {
        var pos = new Position(row, col);
        return move.From == pos || move.To == pos;
    }

    private void RefreshStatusLabels()
    {
        if (!string.IsNullOrEmpty(_timeoutResultText))
        {
            _gameResultText = _timeoutResultText;
            _gameStatusText = string.Empty;
            OnPropertyChanged(nameof(GameStatusText));
            OnPropertyChanged(nameof(GameResultText));
            return;
        }

        var result = _controller.GetGameResult();
        var turn = _controller.GetCurrentTurn();

        _gameResultText = result switch
        {
            GameResult.Checkmate => turn == PieceColor.White ? "Black wins" : "White wins",
            GameResult.Stalemate => "Draw — stalemate",
            GameResult.DrawFiftyMoveRule => "Draw — 50-move rule",
            GameResult.DrawRepetition => "Draw — threefold repetition",
            GameResult.DrawInsufficientMaterial => "Draw — insufficient material",
            _ => string.Empty
        };

        _gameStatusText = result == GameResult.Check ? "Check" : string.Empty;

        OnPropertyChanged(nameof(GameStatusText));
        OnPropertyChanged(nameof(GameResultText));
    }
}
