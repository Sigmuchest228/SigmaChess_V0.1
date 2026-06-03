using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using SigmaChess.Engine;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

// ViewModel экрана просмотра сохранённой партии (страница GameReplayPage). Загружает
// партию из Firebase по её id, восстанавливает ходы и даёт листать их кнопками
// (в начало, назад, вперёд, в конец). В отличие от GameViewModel, тут нельзя ходить —
// только смотреть. Свой отдельный GameController используется как «проигрыватель»
// истории.
public class GameReplayViewModel : ViewModelBase
{
    // Свой контроллер движка (отдельный от игрового), часы тут не нужны.
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

    // Конструктор без параметров (для интерфейса): берёт сервисы из общего AppService.
    public GameReplayViewModel()
        : this(AppService.GetInstance().BoardLayout, AppService.GetInstance().FirebaseSync)
    {
    }

    // Основной конструктор: сохраняет сервисы, создаёт команду «назад» и команды
    // листания (в начало/назад/вперёд/в конец) с условиями доступности, подписывается
    // на изменение размеров экрана.
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

    // 64 клетки доски и строки таблицы ходов для интерфейса.
    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    public ObservableCollection<MoveHistoryRow> MoveRows { get; } = [];

    // Команда возврата на предыдущий экран.
    public ICommand GoBackCommand { get; }

    // Команды листания партии: в начало, на ход назад, на ход вперёд, в конец.
    public Command StepFirstCommand { get; }

    public Command StepPrevCommand { get; }

    public Command StepNextCommand { get; }

    public Command StepLastCommand { get; }

    // Заголовок экрана («Replay» / «Game replay»).
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

    // Текст исхода партии («White won» и т.п.). При смене обновляет и флаг наличия
    // подписи исхода.
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

    // Есть ли подпись исхода (нужно интерфейсу, чтобы показать/скрыть блок).
    public bool HasWinnerCaption => !string.IsNullOrEmpty(_winnerOutcomeText);

    // Цвет подписи исхода (зелёный/серый и т.п. в зависимости от победителя).
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

    // «Хвост» подзаголовка: причина окончания партии (или сообщение об ошибке загрузки).
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

    // Размер доски в пикселях; при смене пересчитывает зависимые размеры.
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

    // Производные размеры для интерфейса (сетка с координатами, шрифт фигур и координат).
    public double BoardGridSize => BoardExtent + CoordStrip;

    public double PieceFontSize => Math.Clamp(BoardExtent / 8.0 * 0.62, 14, 44);

    public double CoordFontSize => Math.Clamp(BoardExtent * 0.045, 10, 15);

    // Принимает параметры навигации: достаёт id партии (GameId), которую надо открыть.
    // Если id сменился — сбрасывает кэш «уже загруженной» партии, чтобы перезагрузить.
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

    // Загружает партию для просмотра. Считает размер доски и создаёт клетки. Если id
    // нет — пустой экран. Если эта партия уже загружена — просто перерисовывает. Иначе
    // тянет запись из Firebase, упорядочивает ходы, восстанавливает их через
    // GameReplayMoveResolver и проигрывает в контроллере. На любой проблеме (нет
    // партии, не удалось восстановить ходы, ошибка сети) показывает сообщение. Всё
    // обновление интерфейса — в главном потоке.
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

    // Очищает оформление экрана (нет партии): убирает подпись исхода и подзаголовок.
    private void ClearReplayChrome()
    {
        WinnerOutcomeText = string.Empty;
        WinnerOutcomeColor = ChessOutcomePalette.TextForWinner(string.Empty);
        ReplaySubtitleTail = string.Empty;
    }

    // Оформление при ошибке: убирает подпись исхода и показывает сообщение об ошибке
    // в подзаголовке.
    private void ApplyErrorReplayChrome(string message)
    {
        WinnerOutcomeText = string.Empty;
        WinnerOutcomeColor = ChessOutcomePalette.TextForWinner(string.Empty);
        ReplaySubtitleTail = message;
    }

    // Оформление при успешной загрузке: формирует подпись победителя, её цвет и
    // понятную причину окончания партии в подзаголовке.
    private void ApplySuccessReplayChrome(string winnerRaw, string endReason)
    {
        var nw = ChessOutcomePalette.NormalizeWinner(winnerRaw);
        var caption = ChessOutcomePalette.ReplayWinnerCaption(nw);
        WinnerOutcomeText = caption;
        WinnerOutcomeColor = ChessOutcomePalette.TextForWinner(nw);
        var humanEnd = HumanEndReason(endReason);
        ReplaySubtitleTail = string.IsNullOrEmpty(caption) ? humanEnd : " · " + humanEnd;
    }

    // Переводит техническую причину окончания в понятный человеку текст.
    private static string HumanEndReason(string endReason) =>
        endReason.ToLowerInvariant() switch
        {
            "checkmate" => "Checkmate",
            "stalemate" => "Stalemate",
            "fifty_move" => "50-move rule",
            "repetition" => "Repetition",
            "insufficient_material" => "Insufficient material",
            "timeout" => "Time",
            _ => string.IsNullOrWhiteSpace(endReason) ? "—" : endReason
        };

    // Переходит к показу позиции после target полуходов (ограничивая диапазоном).
    // Перерисовывает доску и обновляет доступность кнопок листания.
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

    // Сообщает интерфейсу, что доступность кнопок листания могла измениться.
    private void NotifyStepCommands()
    {
        StepFirstCommand.ChangeCanExecute();
        StepPrevCommand.ChangeCanExecute();
        StepNextCommand.ChangeCanExecute();
        StepLastCommand.ChangeCanExecute();
    }

    // Реакция на изменение экрана: пересчитать размер доски.
    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => UpdateBoardExtent();

    // Пересчитывает размер доски для раскладки реплея (доска + колонка ходов сбоку).
    private void UpdateBoardExtent()
    {
        BoardExtent = _layoutService.CalculateBoardExtentForGamePage(
            DeviceDisplay.Current.MainDisplayInfo,
            GamePageBoardExtentMode.SideMoveColumn);
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

    // Перестраивает таблицу ходов из восстановленной истории партии (парами,
    // в короткой нотации).
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

    // Перерисовывает доску для текущей позиции просмотра: берёт доску после _replayPlies
    // полуходов и подсвечивает последний сделанный ход. Интерактива (выбор/ходы) тут
    // нет — это только просмотр.
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

    // Помощник: входит ли клетка (row, col) в последний ход (для подсветки).
    private static bool IsLastMoveSquare(Move move, int row, int col)
    {
        var pos = new Position(row, col);
        return move.From == pos || move.To == pos;
    }
}
