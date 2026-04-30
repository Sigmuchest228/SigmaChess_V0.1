namespace SigmaChess.Engine;

/// <summary>
/// Состояние доски — массив 8x8 клеток <see cref="Piece"/>?.
/// Координаты: Row=0 — верхняя горизонталь (там стоят чёрные после <see cref="Initialize"/>),
/// Row=7 — нижняя (белые); Col=0 — файл «a», Col=7 — файл «h».
/// <para>
/// Метод <see cref="MovePiece"/> делает «голый» перенос From → To. Все спецслучаи
/// (en passant, рокировка, превращение) живут в <see cref="Game.ApplyMoveToBoard"/>,
/// потому что им нужно знать состояние партии (право на рокировку, EP-цель).
/// </para>
/// </summary>
public class Board
{
    /// <summary>Прямой доступ к 64 клеткам. Null — пустая клетка.</summary>
    public Piece?[,] Squares { get; } = new Piece?[8, 8];

    /// <summary>Сбрасывает все клетки и расставляет стандартную начальную позицию.</summary>
    public void Initialize()
    {
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                Squares[row, col] = null;
            }
        }

        // Чёрные сверху (по соглашению Row=0).
        SetupBackRank(0, PieceColor.Black);
        SetupPawns(1, PieceColor.Black);

        // Белые снизу.
        SetupPawns(6, PieceColor.White);
        SetupBackRank(7, PieceColor.White);
    }

    /// <summary>Возвращает фигуру на клетке или null.</summary>
    public Piece? GetPiece(Position position)
    {
        EnsureInsideBoard(position);
        return Squares[position.Row, position.Col];
    }

    /// <summary>Записать (или очистить, если null) фигуру на клетку.</summary>
    public void SetPiece(Position position, Piece? piece)
    {
        EnsureInsideBoard(position);
        Squares[position.Row, position.Col] = piece;
    }

    /// <summary>
    /// «Голый» перенос фигуры: From очищается, на To кладётся фигура из From.
    /// Никакой логики EP/рокировки/превращения здесь нет — этим занимается <see cref="Game"/>.
    /// </summary>
    public void MovePiece(Move move)
    {
        EnsureInsideBoard(move.From);
        EnsureInsideBoard(move.To);

        var movingPiece = GetPiece(move.From);
        if (movingPiece is null)
        {
            throw new InvalidOperationException("There is no piece on the source position.");
        }

        SetPiece(move.To, movingPiece);
        SetPiece(move.From, null);
    }

    /// <summary>Координата лежит в диапазоне 0..7 по обеим осям.</summary>
    public bool IsInsideBoard(Position position)
    {
        return position.Row >= 0 &&
               position.Row < 8 &&
               position.Col >= 0 &&
               position.Col < 8;
    }

    // Расставляет 8 пешек в один ряд.
    private void SetupPawns(int row, PieceColor color)
    {
        for (var col = 0; col < 8; col++)
        {
            Squares[row, col] = new Piece(PieceType.Pawn, color);
        }
    }

    // Тяжёлые фигуры в стандартном порядке: R N B Q K B N R.
    private void SetupBackRank(int row, PieceColor color)
    {
        Squares[row, 0] = new Piece(PieceType.Rook, color);
        Squares[row, 1] = new Piece(PieceType.Knight, color);
        Squares[row, 2] = new Piece(PieceType.Bishop, color);
        Squares[row, 3] = new Piece(PieceType.Queen, color);
        Squares[row, 4] = new Piece(PieceType.King, color);
        Squares[row, 5] = new Piece(PieceType.Bishop, color);
        Squares[row, 6] = new Piece(PieceType.Knight, color);
        Squares[row, 7] = new Piece(PieceType.Rook, color);
    }

    // Защита от выхода за пределы — кидает исключение, чтобы быстро падать в багах.
    private void EnsureInsideBoard(Position position)
    {
        if (!IsInsideBoard(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is outside the board.");
        }
    }
}
