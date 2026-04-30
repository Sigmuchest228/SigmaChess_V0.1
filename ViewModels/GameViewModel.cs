using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Devices;
using SigmaChess.Engine;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

/// <summary>
/// ViewModel страницы игры на одном устройстве. Связывает <see cref="GameController"/>
/// (движок) с UI-коллекцией из 64 ячеек, обрабатывает тапы пользователя, рассчитывает
/// размер доски под экран и хранит пользовательские настройки партии.
/// </summary>
public class GameViewModel : ViewModelBase
{
    private readonly GameController _controller;
    private readonly BoardLayoutService _layoutService;
    private double _boardExtent = 320;
    private string _gameStatusText = string.Empty;
    private string _gameResultText = string.Empty;
    private bool _isInitialized;
    private bool _autoFlipEnabled;
    private bool _highlightLastMoveEnabled;
    private bool _autoQueenEnabled;
    private bool _isBoardFlipped;

    // Полоса с координатами файлов/рангов вокруг доски (по 28 px на одну сторону).
    // Используется только для расчёта размера внешнего Grid.
    private const double CoordStrip = 28;

    /// <summary>64 клетки доски в порядке (row=0,col=0)...(row=7,col=7).</summary>
    public ObservableCollection<BoardCellViewModel> Cells { get; } = [];

    public Command<BoardCellViewModel> CellTappedCommand { get; }

    public ICommand BackToMenuCommand { get; }

    public ICommand NewGameCommand { get; }

    public string CurrentTurnText =>
        _controller.GetCurrentTurn() == PieceColor.White ? "White to move" : "Black to move";

    public string GameStatusText => _gameStatusText;

    public string GameResultText => _gameResultText;

    /// <summary>Сторона квадрата самой доски в DIP. Меняется при ротации/смене окна.</summary>
    public double BoardExtent
    {
        get => _boardExtent;
        private set
        {
            // Маленький порог, чтобы не молотить INotify на каждый микроразмер.
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

    /// <summary>
    /// Размер шрифта Юникод-фигуры внутри клетки.
    /// 0.62 от стороны клетки — экспериментально выбранный коэффициент,
    /// при котором глифы крупных и мелких шрифтов выглядят одинаково плотно.
    /// Clamp ограничивает от уродливо мелких/гигантских значений на краях.
    /// </summary>
    public double PieceFontSize => Math.Clamp(BoardExtent / 8.0 * 0.62, 14, 44);

    /// <summary>Размер шрифта подписи координаты (a..h, 1..8) в полосе вокруг доски.</summary>
    public double CoordFontSize => Math.Clamp(BoardExtent * 0.045, 10, 15);

    /// <summary>Авто-переворот доски после каждого хода (для игры на одном устройстве вдвоём).</summary>
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

    /// <summary>Подсвечивать последний сделанный ход (две клетки From/To).</summary>
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
            // Перерисовываем доску немедленно — пользователь ждёт визуального ответа на тоггл.
            RefreshBoard();
        }
    }

    /// <summary>Авто-ферзь при превращении пешки (без попапа выбора).</summary>
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

    /// <summary>Доска перевёрнута (чёрные снизу). Меняется автопереворотом или вручную.</summary>
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

    public GameViewModel(GameController controller, BoardLayoutService layoutService)
    {
        _controller = controller;
        _layoutService = layoutService;

        CellTappedCommand = new Command<BoardCellViewModel>(async cell => await OnCellTappedAsync(cell));
        BackToMenuCommand = new Command(async () => await GoBackToMenuAsync());
        NewGameCommand = new Command(async () => await ConfirmAndStartNewGameAsync());

        // Реагируем на смену ориентации/размера экрана: пересчитываем размер доски.
        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
    }

    /// <summary>
    /// Готовит партию к показу: один раз создаёт 64 ячейки и стартует игру.
    /// Безопасно вызывать многократно — повторные вызовы ничего не делают.
    /// Идемпотентность важна, потому что метод дёргается из <c>OnAppearing</c> страницы,
    /// а та может срабатывать многократно при возврате с других экранов.
    /// </summary>
    public Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return Task.CompletedTask;
        }

        UpdateBoardExtent();
        EnsureCellsCreated();
        _controller.InitializeGame();
        RefreshBoard();
        _isInitialized = true;
        return Task.CompletedTask;
    }

    /// <summary>Сбрасывает партию и подсветку, оставляет настройки и существующие ячейки.</summary>
    public void StartNewGame()
    {
        _controller.InitializeGame();
        IsBoardFlipped = false;
        RefreshBoard();
    }

    private async Task GoBackToMenuAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        await Shell.Current.GoToAsync("//MainPage");
    }

    // Спрашиваем подтверждение через CommunityToolkit Popup, чтобы не сбросить партию случайно.
    private async Task ConfirmAndStartNewGameAsync()
    {
        var confirmed = await ConfirmPopup.ShowAsync(
            "New game",
            "Start a new game? Current progress will be lost.",
            "Start",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        StartNewGame();
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        UpdateBoardExtent();
    }

    private void UpdateBoardExtent()
    {
        BoardExtent = _layoutService.CalculateBoardExtent(DeviceDisplay.Current.MainDisplayInfo);
    }

    // Создаёт коллекцию ячеек один раз: при последующих стартах партии те же VM-ячейки переиспользуются,
    // меняется только их состояние. Это важно для производительности — не пересоздавать UI-элементы доски.
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

    /// <summary>
    /// Обработчик тапа по клетке.
    /// Пайплайн (по шагам):
    ///   1. У контроллера спрашиваем «есть ли запланированный ход в эту клетку?»
    ///      Если нет — это просто выбор/смена выбора фигуры: вызываем HandleSelection и выходим.
    ///   2. Если есть — проверяем, не превращение ли это пешки. Если да:
    ///      - при включенном Auto-queen берём ферзя без вопросов;
    ///      - иначе показываем PromotionPopup и ждём выбор пользователя (await).
    ///   3. Исполняем ход через ExecutePlannedMove.
    ///   4. Если включен Auto-flip и сторона хода сменилась — переворачиваем доску.
    ///   5. Перерисовываем UI.
    /// </summary>
    public async Task OnCellTappedAsync(BoardCellViewModel? cell)
    {
        if (cell is null)
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
            // Защита от рассинхрона UI/контроллера; в нормальном сценарии не наступает.
            return;
        }

        // Превращение возможно только пешкой и только на «последней» горизонтали для её цвета.
        var lastRank = moverPiece.Color == PieceColor.White ? 0 : 7;
        if (moverPiece.Type == PieceType.Pawn && pending.To.Row == lastRank)
        {
            var promo = AutoQueenEnabled
                ? PieceType.Queen
                : await PromotionPopup.ShowAsync(moverPiece.Color);
            // Move — record, его нельзя мутировать; делаем «копию с подмененным полем».
            pending = pending with { Promotion = promo };
        }

        var turnBefore = _controller.GetCurrentTurn();
        var executed = _controller.ExecutePlannedMove(pending);
        var turnAfter = _controller.GetCurrentTurn();

        if (executed && turnBefore != turnAfter && AutoFlipEnabled)
        {
            // Сторона, чей сейчас ход — всегда внизу.
            IsBoardFlipped = turnAfter == PieceColor.Black;
        }

        RefreshBoard();
    }

    /// <summary>
    /// Синхронизирует коллекцию из 64 ячеек VM с состоянием движка:
    /// фигуры, выбранная клетка, цели легальных ходов, подсветка последнего хода, лейблы.
    /// </summary>
    public void RefreshBoard()
    {
        var board = _controller.GetBoard();
        var selected = _controller.GetSelectedSquare();
        // HashSet для O(1) проверки «эта клетка — цель легального хода?».
        var highlightSet = new HashSet<(int R, int C)>(
            _controller.GetHighlightedSquares().Select(h => (h.Row, h.Col)));
        var turn = _controller.GetCurrentTurn();
        var lastMove = _controller.GetLastMove();
        // Подсветку «последний ход» прячем, если пользователь уже выбрал свою фигуру —
        // иначе зелёные индикаторы хода путаются с зелёным last-move.
        var showLastMove = HighlightLastMoveEnabled && selected is null && lastMove is not null;

        foreach (var cell in Cells)
        {
            var pos = new Position(cell.Row, cell.Col);
            cell.Piece = board.GetPiece(pos);

            cell.IsSelected = selected == pos;

            var isMoveTarget = highlightSet.Contains((cell.Row, cell.Col));

            if (isMoveTarget)
            {
                cell.IsHighlighted = true;
                var targetPiece = board.GetPiece(pos);
                // Взятие, если на клетке стоит фигура противоположного цвета (своих туда не хожу).
                var isCapture = targetPiece is not null && targetPiece.Color != turn;
                cell.MoveTarget = isCapture ? MoveTargetHighlight.Capture : MoveTargetHighlight.ToEmpty;
                continue;
            }

            cell.MoveTarget = MoveTargetHighlight.None;
            cell.IsHighlighted = showLastMove && IsLastMoveSquare(lastMove!, cell.Row, cell.Col);
        }

        OnPropertyChanged(nameof(CurrentTurnText));
        RefreshStatusLabels();
    }

    private static bool IsLastMoveSquare(Move move, int row, int col)
    {
        var pos = new Position(row, col);
        return move.From == pos || move.To == pos;
    }

    // Маппинг GameResult → пользовательский текст. Текст «Check» — отдельная плашка статуса,
    // потому что игра при шахе всё ещё идёт. Остальные исходы — финальные.
    private void RefreshStatusLabels()
    {
        var result = _controller.GetGameResult();
        var turn = _controller.GetCurrentTurn();

        _gameResultText = result switch
        {
            // На мате текущая сторона осталась без ходов — выигрывает противоположная.
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
