using Microsoft.Maui.Graphics;
using SigmaChess.Engine;

namespace SigmaChess.ViewModels;

/// <summary>
/// Тип подсветки клетки как цели хода. Влияет только на цвет фона:
/// <see cref="ToEmpty"/> — обычный «зелёный» индикатор хода,
/// <see cref="Capture"/> — «красный» индикатор взятия фигуры противника.
/// </summary>
public enum MoveTargetHighlight
{
    None,
    ToEmpty,
    Capture
}

/// <summary>
/// ViewModel одной клетки доски (всего их 64). Хранит фигуру, флаги подсветки
/// и сама вычисляет цвет фона по приоритету: выбрана → взятие → ход/последний ход → базовый.
/// </summary>
public class BoardCellViewModel : ViewModelBase
{
    private Piece? _piece;
    private MoveTargetHighlight _moveTarget;
    private bool _isSelected;
    private bool _isHighlighted;

    /// <summary>Координата строки в системе движка (0 — верх, 7 — низ).</summary>
    public int Row { get; }

    /// <summary>Координата столбца в системе движка (0 — файл «a», 7 — «h»).</summary>
    public int Col { get; }

    /// <summary>Фигура на клетке. null — клетка пуста.</summary>
    public Piece? Piece
    {
        get => _piece;
        set
        {
            _piece = value;
            OnPropertyChanged();
            // Дёргаем PieceSymbol тоже — UI рисует Юникод-глиф фигуры.
            OnPropertyChanged(nameof(PieceSymbol));
            RefreshSquareBackground();
        }
    }

    /// <summary>Подсветка клетки как цели хода: пустая или взятие.</summary>
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

    /// <summary>Клетка является «активным выбором» (на ней стоит выделенная пользователем фигура).</summary>
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

    /// <summary>
    /// Универсальный флаг подсветки. Используется для двух вещей: цели легального хода
    /// (выставляется одновременно с <see cref="MoveTarget"/>) и подсветки последнего хода.
    /// </summary>
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

    /// <summary>Светлая клетка (по правилу: чётная сумма координат).</summary>
    public bool IsWhiteSquare => (Row + Col) % 2 == 0;

    /// <summary>Юникод-символ фигуры на клетке (или пустая строка).</summary>
    public string PieceSymbol => GetPieceSymbol(Piece);

    /// <summary>Текущий цвет фона. Перевычисляется в <see cref="RefreshSquareBackground"/>.</summary>
    public Color SquareBackground { get; private set; } = Colors.Transparent;

    public BoardCellViewModel(int row, int col)
    {
        Row = row;
        Col = col;
        // Сразу выставляем базовый цвет фона, чтобы первая отрисовка не моргала.
        RefreshSquareBackground();
    }

    // Палитра. Пары «светлая/тёмная» используются для одной и той же роли
    // (чтобы зелёная подсветка хода читалась и на белой, и на чёрной клетке).
    private static readonly Color Light = Color.FromArgb("#F0D9B5");
    private static readonly Color Dark = Color.FromArgb("#B58863");
    private static readonly Color MoveOnLight = Color.FromArgb("#B9E4B0");
    private static readonly Color MoveOnDark = Color.FromArgb("#6FAF6A");
    private static readonly Color CaptureOnLight = Color.FromArgb("#F5A3A3");
    private static readonly Color CaptureOnDark = Color.FromArgb("#C85A5A");
    private static readonly Color SelectedOnLight = Color.FromArgb("#FFE082");
    private static readonly Color SelectedOnDark = Color.FromArgb("#F0A040");

    // Приоритет цветов (сверху вниз):
    //   1. Клетка выбрана (там стоит фигура, ход которой планируется).
    //   2. Цель = взятие (на клетке фигура противника).
    //   3. Цель = пустая клетка ИЛИ подсветка «последний ход».
    //   4. Базовый цвет светлой/тёмной клетки.
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

    // Текстовое представление фигуры через Юникод-символы шахмат.
    // Используется вместо изображений, чтобы не тащить отдельные файлы под каждую фигуру.
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
