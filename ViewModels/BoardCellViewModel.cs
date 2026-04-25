using Microsoft.Maui.Graphics;
using SigmaChess.Engine;

namespace SigmaChess.ViewModels;

public enum MoveTargetHighlight
{
    None,
    ToEmpty,
    Capture
}

public class BoardCellViewModel : ViewModelBase
{
    private Piece? _piece;
    private MoveTargetHighlight _moveTarget;
    private bool _isSelected;

    public int Row { get; }

    public int Col { get; }

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

    public bool IsWhiteSquare => (Row + Col) % 2 == 0;

    public string PieceSymbol => GetPieceSymbol(Piece);

    public Color SquareBackground { get; private set; }

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
        else if (_moveTarget == MoveTargetHighlight.ToEmpty)
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

    // Временное текстовое представление фигуры вместо изображений.
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
