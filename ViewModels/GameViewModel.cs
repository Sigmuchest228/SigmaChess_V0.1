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

// Главная ViewModel экрана игры (страница GamePage). Связывает движок (через
// GameController) с интерфейсом: держит 64 клетки доски (Cells), список ходов
// (MoveRows), часы каждой стороны, настройки (автопереворот доски, авто-ферзь,
// подсветка последнего хода), режим раскладки и сохранение завершённой партии в
// Firebase. Реагирует на тапы по клеткам, ведёт перемотку ходов и показывает попап
// окончания игры. Сами правила шахмат тут не реализованы — за ними идём в движок.
public class GameViewModel : ViewModelBase
{
    // Зависимости: контроллер движка, сервис расчёта размеров доски, общий сервис
    // приложения (пользователь и т.п.) и репозиторий синхронизации с Firebase.
    private readonly global::SigmaChess.Engine.GameController _controller;
    private readonly BoardLayoutService _layoutService;
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;
    // Ходы текущей партии в виде для сохранения в облако и секундомер времени на ход.
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

    private const double CoordStrip = 28;

    // Коллекция из 64 клеток доски, к которой привязан интерфейс.
    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    // Строки таблицы ходов (по паре белые/чёрные в строке).
    public ObservableCollection<MoveHistoryRow> MoveRows { get; } = [];

    // Команда тапа по клетке доски (доступна, только когда игра не заблокирована).
    public Command<BoardCellViewModel> CellTappedCommand { get; }

    // Команда «назад в меню».
    public ICommand BackToMenuCommand { get; }

    // Команды перемотки на ход назад/вперёд при просмотре партии.
    public Command StepBackwardCommand { get; }

    public Command StepForwardCommand { get; }

    // Подпись «чей ход» для интерфейса.
    public string CurrentTurnText =>
        _controller.GetCurrentTurn() == PieceColor.White ? "White to move" : "Black to move";

    // Короткий статус (например «Check») и текст результата партии.
    public string GameStatusText => _gameStatusText;

    public string GameResultText => _gameResultText;

    // Текст часов белых и чёрных («—» если без лимита времени).
    public string WhiteClockText { get; private set; } = "—";

    public string BlackClockText { get; private set; } = "—";

    // Можно ли перематывать назад/вперёд (зависит от позиции в истории ходов).
    public bool CanStepBackward => _replayPliesApplied > 0;

    public bool CanStepForward => _replayPliesApplied < _controller.GetPlayedMoves().Count;

    // Размер доски в пикселях. При изменении пересчитывает зависимые размеры (сетка,
    // шрифт фигур, шрифт координат).
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

    // Производные размеры для интерфейса: общий размер сетки с полоской координат,
    // размер шрифта фигур и размер шрифта подписей координат.
    public double BoardGridSize => BoardExtent + CoordStrip;

    public double PieceFontSize => Math.Clamp(BoardExtent / 8.0 * 0.62, 14, 44);

    public double CoordFontSize => Math.Clamp(BoardExtent * 0.045, 10, 15);

    // Настройка: автоматически переворачивать доску к стороне, чей ход.
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

    // Настройка: подсвечивать клетки последнего сделанного хода. При смене
    // перерисовывает доску.
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

    // Настройка: при превращении пешки автоматически ставить ферзя, не спрашивая.
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

    // Перевёрнута ли сейчас доска (чёрными вниз). Меняется автопереворотом.
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

    // Режим раскладки (обычный или «лицом к лицу»). При смене обновляет производные
    // флаги и пересчитывает размер доски.
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

    // Удобные флаги текущего режима раскладки для привязки в интерфейсе.
    public bool IsFaceToFaceLayout => _layoutMode == GameLayoutMode.FaceToFace;

    public bool IsCasualLayout => _layoutMode == GameLayoutMode.Casual;

    // Конструктор: получает зависимости, создаёт команды (тап по клетке, в меню,
    // перемотка) и подписывается на изменение размеров экрана.
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

    // Однократная инициализация экрана: считает размер доски и создаёт 64 клетки.
    // Повторные вызовы только обновляют размер доски. Партия здесь не восстанавливается
    // — при каждом новом заходе на экран сначала показывается попап настройки (см.
    // ShouldOfferTimeSetupOnAppear и PrepareForSetupPopup).
    public Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {

            UpdateBoardExtent();
            return Task.CompletedTask;
        }

        UpdateBoardExtent();
        EnsureCellsCreated();

        _isInitialized = true;
        return Task.CompletedTask;
    }

    // Один раз подписывается на событие навигации Shell, чтобы поймать уход с экрана
    // игры на главный экран и корректно сбросить партию.
    private void TryRegisterShellHomeNavigationHandler()
    {
        if (_shellHomeNavRegistered || Shell.Current is null)
        {
            return;
        }

        Shell.Current.Navigated += OnShellNavigatedForHomeFromGame;
        _shellHomeNavRegistered = true;
    }

    // Обработчик навигации: если ушли именно с GamePage на MainPage — останавливает
    // часы и сбрасывает состояние партии (в главном потоке).
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

    // Нужно ли при открытии экрана показать попап настройки новой партии. Да, пока
    // NeedsInitialTimePopup = true (после ухода на главный экран или первого захода).
    // После подтверждения настройки флаг сбрасывается — при повторном OnAppearing в той
    // же сессии игры попап не мешает. Число ходов не проверяем: состояние не
    // восстанавливается после выхода.
    public bool ShouldOfferTimeSetupOnAppear() => NeedsInitialTimePopup;

    // Сбрасывает движок и UI партии перед попапом настройки, чтобы на доске не осталось
    // ходов от прошлой сессии. Флаг NeedsInitialTimePopup и настройки времени не трогает.
    public void PrepareForSetupPopup()
    {
        InternalResetGameState();
        RefreshBoard();
    }

    // Полный сброс при возврате на главный экран: партия не сохраняется между заходами.
    // Обнуляет движок, ставит NeedsInitialTimePopup = true (при следующем открытии
    // GamePage снова будет попап настройки), сбрасывает раскладку и время по умолчанию.
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

    // Сброс при выходе из аккаунта: останавливает часы и приводит партию и настройки
    // к стартовому состоянию (чтобы новый пользователь не увидел чужую партию).
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

    // Применяет выбор из диалога новой игры: лимит времени, минуты сторон (с
    // ограничением 1..180), режим раскладки; затем сбрасывает и показывает часы.
    public void ApplyTimeControl(NewGameSetupResult result)
    {
        _unlimitedTime = result.Unlimited;
        _minutesWhite = Math.Clamp(result.WhiteMinutes, 1, 180);
        _minutesBlack = Math.Clamp(result.BlackMinutes, 1, 180);
        LayoutMode = result.LayoutMode;
        ResetClocksFromTimeControl();
        NotifyClocks();
    }

    // Старт новой партии после диалога настройки: сброс состояния, прячет попап
    // настройки, перерисовывает доску и запускает часы.
    public void StartNewGameAfterSetup()
    {
        InternalResetGameState();
        NeedsInitialTimePopup = false;
        RefreshBoard();
        RestartClockForCurrentGame();
        OnPropertyChanged(nameof(CurrentTurnText));
    }

    // Внутренний сброс именно партии: новая игра в движке, доска не перевёрнута,
    // обнуление облачного трекинга, перемотки, флагов таймаута и попапа, очистка
    // таблицы ходов и обновление статуса/команд.
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

    // Сбрасывает часы по текущим настройкам времени: либо «—» без лимита, либо
    // заданные минуты для белых и чёрных. Обновляет тексты часов в интерфейсе.
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

    // Форматирует время в строку «минуты:секунды» (отрицательное считает за ноль).
    private static string FormatClock(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
        {
            t = TimeSpan.Zero;
        }

        var total = (int)t.TotalMinutes;
        return $"{total}:{t.Seconds:D2}";
    }

    // Уведомляет интерфейс, что тексты часов могли измениться.
    private void NotifyClocks()
    {
        OnPropertyChanged(nameof(WhiteClockText));
        OnPropertyChanged(nameof(BlackClockText));
    }

    // (Пере)запускает таймер часов для текущей партии. Ничего не делает, если время
    // без лимита или партия уже завершена. Тикает каждые 300 мс.
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

    // Останавливает и убирает таймер часов (отписывается от тика).
    private void StopClockTimer()
    {
        if (_clockTimer is not null)
        {
            _clockTimer.Tick -= OnClockTimerTick;
            _clockTimer.Stop();
            _clockTimer = null;
        }
    }

    // Тик часов (каждые 300 мс). Пропускает тик при отсутствии лимита, в режиме
    // перемотки, после таймаута или конца партии. Иначе вычитает прошедшее время у
    // стороны, чей ход. Если время кончилось — фиксирует поражение по времени,
    // останавливает часы, сохраняет партию и показывает попап. Скачок > 2 c (телефон
    // «засыпал») считает за 0.5 c, чтобы не списать лишнего.
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
                _ = TrySaveCompletedGameIfTerminalAsync();
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
                _ = TrySaveCompletedGameIfTerminalAsync();
                _ = TryShowGameOverPopupIfNeededAsync();
                return;
            }

            BlackClockText = FormatClock(_blackRemaining);
        }

        OnPropertyChanged(nameof(WhiteClockText));
        OnPropertyChanged(nameof(BlackClockText));
    }

    // Вызывается, когда экран игры показан: подписывается на навигацию и запускает часы.
    public void OnGamePageAppeared()
    {
        TryRegisterShellHomeNavigationHandler();
        RestartClockForCurrentGame();
    }

    // Вызывается, когда экран игры скрыт: останавливает часы.
    public void OnGamePageDisappeared()
    {
        StopClockTimer();
    }

    // Завершилась ли партия по правилам движка (мат/пат/любая ничья). Таймаут сюда не
    // входит — он считается отдельно.
    private static bool IsEngineTerminal(GameResult r) =>
        r is GameResult.Checkmate
            or GameResult.Stalemate
            or GameResult.DrawFiftyMoveRule
            or GameResult.DrawRepetition
            or GameResult.DrawInsufficientMaterial;

    // Заблокирована ли игра для ходов: при просмотре истории (перемотке), после
    // таймаута или после конца партии.
    private bool IsPlayLocked() =>
        _controller.GetPlayedMoves().Count != _replayPliesApplied
        || _timeoutLoser is not null
        || IsEngineTerminal(_controller.GetGameResult());

    // Начинает новую партию с текущими настройками времени: сброс, перерисовка, часы.
    public void StartNewGame()
    {
        InternalResetGameState();
        RefreshBoard();
        RestartClockForCurrentGame();
    }

    // Сбрасывает данные для сохранения партии в облако: очищает ходы, снимает флаг
    // «сохранено», перезапускает секундомер времени на ход.
    private void ResetCloudGameTracking()
    {
        _moveHistory.Clear();
        _gameSaved = false;
        _moveStopwatch.Restart();
    }

    // Переход на главный экран: останавливает часы, открывает MainPage и сбрасывает
    // состояние партии. Ошибки навигации показывает попапом. Всё в главном потоке.
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

    // Возврат в меню с подтверждением: спрашивает «выйти?», и только при согласии
    // уходит на главный экран (чтобы случайно не потерять партию).
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

    // Публичная обёртка для запроса «выйти в меню?» (зовётся из интерфейса).
    public Task ConfirmLeaveGameAndGoHomeAsync() => GoBackToMenuAsync();

    // Показывает диалог настройки новой игры (время/раскладка), и если игрок подтвердил
    // — применяет настройки и начинает новую партию.
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

    // Перемотка на ход назад: уменьшает число показанных полуходов, останавливает
    // часы (мы смотрим прошлое) и перерисовывает доску.
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

    // Перемотка на ход вперёд: увеличивает число показанных полуходов; если догнали
    // текущую позицию — снова запускает часы.
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

    // Сообщает интерфейсу, что доступность кнопок перемотки и тапа могла измениться.
    private void NotifyReplayCommands()
    {
        OnPropertyChanged(nameof(CanStepBackward));
        OnPropertyChanged(nameof(CanStepForward));
        StepBackwardCommand.ChangeCanExecute();
        StepForwardCommand.ChangeCanExecute();
        CellTappedCommand.ChangeCanExecute();
    }

    // Реакция на изменение параметров экрана (поворот/размер): пересчитать размер доски.
    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        UpdateBoardExtent();
    }

    // Пересчитывает размер доски через сервис раскладки с учётом текущего режима
    // (обычный или «лицом к лицу»).
    private void UpdateBoardExtent()
    {
        BoardExtent = _layoutService.CalculateBoardExtentForGamePage(
            DeviceDisplay.Current.MainDisplayInfo,
            IsFaceToFaceLayout
                ? GamePageBoardExtentMode.FaceToFace
                : GamePageBoardExtentMode.CasualBottomMoveStrip);
    }

    // Создаёт 64 клетки доски один раз (если их ещё нет).
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

    // Перестраивает таблицу ходов из истории партии: берёт ходы парами (белые/чёрные),
    // переводит каждый в короткую нотацию (AlgebraicNotation) и добавляет строку.
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

    // Главный обработчик тапа по клетке. Если игра не заблокирована: проверяет, не
    // завершает ли тап ход выбранной фигуры. Если нет — это выбор фигуры (подсветить
    // ходы). Если да — при превращении пешки спрашивает фигуру (или ставит ферзя при
    // авто-ферзе), выполняет ход через движок, пишет ход для облака, перестраивает
    // таблицу ходов, при необходимости переворачивает доску, обновляет состояние и
    // проверяет конец партии (сохранение и попап).
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

    // Закончилась ли партия в любом смысле: по времени (таймаут) или по правилам движка.
    private bool IsOverallTerminal() =>
        _timeoutLoser is not null || IsEngineTerminal(_controller.GetGameResult());

    // Текст для попапа окончания: при таймауте — текст таймаута, иначе — текст
    // результата партии.
    private string GetGameOverSummaryText()
    {
        if (!string.IsNullOrEmpty(_timeoutResultText))
        {
            return _timeoutResultText;
        }

        return _gameResultText;
    }

    // Показывает попап окончания партии один раз, если партия завершилась и есть текст
    // результата. Работает в главном потоке; при сбоях снимает флаг показа, чтобы можно
    // было попробовать снова.
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

    // Сохраняет завершённую партию в Firebase один раз. Определяет победителя и причину
    // окончания (таймаут или результат движка), пропускает, если партия не закончена,
    // пользователь не вошёл или ходов нет. Ошибки сохранения молча проглатываются.
    private async Task TrySaveCompletedGameIfTerminalAsync()
    {
        if (_gameSaved)
        {
            return;
        }

        string winnerColor;
        string endReason;
        if (_timeoutLoser is PieceColor loser)
        {
            winnerColor = loser == PieceColor.White ? "Black" : "White";
            endReason = "timeout";
        }
        else
        {
            var r = _controller.GetGameResult();
            if (r is GameResult.Ongoing or GameResult.Check)
            {
                return;
            }

            winnerColor = FirebaseSyncRepository.ResolveWinnerColor(r, _controller.GetCurrentTurn());
            endReason = FirebaseSyncRepository.ToEndReason(r);
        }

        var uid = _appService.CurrentUserId;
        if (string.IsNullOrEmpty(uid) || _moveHistory.Count == 0)
        {
            return;
        }

        try
        {
            await _firebaseSync.EnsureUserAsync().ConfigureAwait(false);
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

        }
    }

    // Перерисовывает всю доску: обновляет фигуру, подсветку и состояние каждой из 64
    // клеток. Работает в двух режимах: «живая» позиция (показываем выбор фигуры,
    // доступные ходы, последний ход) или «перемотка» (показываем доску из прошлого без
    // интерактива). В конце обновляет подписи статуса и доступность команд.
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

    // Помощник: входит ли клетка (row, col) в последний ход (его начало или конец) —
    // для подсветки.
    private static bool IsLastMoveSquare(Move move, int row, int col)
    {
        var pos = new Position(row, col);
        return move.From == pos || move.To == pos;
    }

    // Обновляет подписи статуса и результата. При таймауте показывает текст таймаута.
    // Иначе по результату движка формирует текст: кто победил (мат), вид ничьей, или
    // «Check» для шаха. Уведомляет интерфейс.
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
