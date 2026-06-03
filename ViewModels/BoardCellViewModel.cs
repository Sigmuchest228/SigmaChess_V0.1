using Microsoft.Maui.Graphics;
using SigmaChess.Engine;

namespace SigmaChess.ViewModels;

// Как подсвечивать клетку-цель хода: никак (None), как ход на пустую клетку
// (ToEmpty) или как взятие (Capture). Влияет на цвет подсветки клетки.
public enum MoveTargetHighlight
{
    None,
    ToEmpty,
    Capture
}

// ViewModel одной клетки доски (одна из 64). Хранит, какая фигура на ней стоит и в
// каком она визуальном состоянии: выбрана, подсвечена, цель хода. Сама считает свой
// цвет фона и символ фигуры, чтобы интерфейс просто «привязался» к этим свойствам.
// Список из 64 таких клеток держит GameViewModel и обновляет их при каждом ходе.
public class BoardCellViewModel : ViewModelBase
{
    private Piece? _piece;
    private MoveTargetHighlight _moveTarget;
    private bool _isSelected;
    private bool _isHighlighted;

    public int Row { get; }

    public int Col { get; }

    // Фигура на этой клетке (или null). При смене уведомляет интерфейс и обновляет
    // символ фигуры и цвет фона.
    public Piece? Piece
    {
        get => _piece;
        set
        {
            _piece = value;
            OnPropertyChanged();

            OnPropertyChanged(nameof(PieceSymbol));
            RefreshSquareBackground();
        }
    }

    // Подсветка клетки как цели хода (пусто/ход/взятие). При смене перекрашивает фон.
    public MoveTargetHighlight MoveTarget
    {
        get => _moveTarget;
        set
        {
            if (_moveTarget == value)
            {
                return;
            }

            _moveTarget = value;
            OnPropertyChanged();
            RefreshSquareBackground();
        }
    }

    // Выбрана ли эта клетка игроком (с неё собираются ходить). При смене перекрашивает фон.
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
            RefreshSquareBackground();
        }
    }

    // Подсвечена ли клетка (например как часть последнего хода). При смене перекрашивает фон.
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value)
            {
                return;
            }

            _isHighlighted = value;
            OnPropertyChanged();
            RefreshSquareBackground();
        }
    }

    // Светлая ли это клетка доски. Вычисляется по чётности суммы координат (как
    // чёрно-белый узор шахматной доски).
    public bool IsWhiteSquare => (Row + Col) % 2 == 0;

    // Поворот символа фигуры в градусах. Нужен для режима «лицом к лицу», где фигуры
    // одной стороны разворачивают на 180°. При смене уведомляет интерфейс.
    public double PieceGlyphRotation
    {
        get => _pieceGlyphRotation;
        set
        {
            if (Math.Abs(_pieceGlyphRotation - value) < 0.01)
            {
                return;
            }

            _pieceGlyphRotation = value;
            OnPropertyChanged();
        }
    }

    private double _pieceGlyphRotation;

    // Символ фигуры для отображения (шахматный юникод-символ) или пустая строка, если
    // клетка пуста.
    public string PieceSymbol => GetPieceSymbol(Piece);

    // Текущий цвет фона клетки. Пересчитывается в RefreshSquareBackground в зависимости
    // от состояния (выбор/подсветка/обычный цвет).
    public Color SquareBackground { get; private set; } = Colors.Transparent;

    // Конструктор: запоминает координаты клетки и сразу задаёт её базовый цвет фона.
    public BoardCellViewModel(int row, int col)
    {
        Row = row;
        Col = col;

        RefreshSquareBackground();
    }

    private static readonly Color Light = Color.FromArgb("#F0D9B5");
    private static readonly Color Dark = Color.FromArgb("#B58863");
    private static readonly Color MoveOnLight = Color.FromArgb("#B9E4B0");
    private static readonly Color MoveOnDark = Color.FromArgb("#6FAF6A");
    private static readonly Color CaptureOnLight = Color.FromArgb("#F5A3A3");
    private static readonly Color CaptureOnDark = Color.FromArgb("#C85A5A");
    private static readonly Color SelectedOnLight = Color.FromArgb("#FFE082");
    private static readonly Color SelectedOnDark = Color.FromArgb("#F0A040");

    // Пересчитывает цвет фона клетки по приоритету: выбранная клетка > взятие >
    // ход/подсветка > обычный цвет доски. Для каждого случая берёт свой оттенок для
    // светлой и тёмной клетки, затем уведомляет интерфейс.
    private void RefreshSquareBackground()
    {
        var isLight = IsWhiteSquare;

        Color next;
        if (IsSelected)
        {
            next = isLight ? SelectedOnLight : SelectedOnDark;
        }
        else if (_moveTarget == MoveTargetHighlight.Capture)
        {
            next = isLight ? CaptureOnLight : CaptureOnDark;
        }
        else if (_moveTarget == MoveTargetHighlight.ToEmpty || _isHighlighted)
        {
            next = isLight ? MoveOnLight : MoveOnDark;
        }
        else
        {
            next = isLight ? Light : Dark;
        }

        SquareBackground = next;
        OnPropertyChanged(nameof(SquareBackground));
    }

    // Превращает фигуру в её шахматный символ (♙♘♗♖♕♔ для белых, ♟♞♝♜♛♚ для чёрных).
    // Если фигуры нет — пустая строка.
    private static string GetPieceSymbol(Piece? piece)
    {
        if (piece is null)
        {
            return string.Empty;
        }

        return (piece.Color, piece.Type) switch
        {
            (PieceColor.White, PieceType.Pawn) => "♙",
            (PieceColor.White, PieceType.Knight) => "♘",
            (PieceColor.White, PieceType.Bishop) => "♗",
            (PieceColor.White, PieceType.Rook) => "♖",
            (PieceColor.White, PieceType.Queen) => "♕",
            (PieceColor.White, PieceType.King) => "♔",
            (PieceColor.Black, PieceType.Pawn) => "♟",
            (PieceColor.Black, PieceType.Knight) => "♞",
            (PieceColor.Black, PieceType.Bishop) => "♝",
            (PieceColor.Black, PieceType.Rook) => "♜",
            (PieceColor.Black, PieceType.Queen) => "♛",
            (PieceColor.Black, PieceType.King) => "♚",
            _ => string.Empty
        };
    }
}
