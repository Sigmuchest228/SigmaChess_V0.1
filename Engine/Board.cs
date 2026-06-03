namespace SigmaChess.Engine;

// Класс который хранит шахматную доску. Это «память» доски: что и где стоит. Правил
// игры тут нет, они в Game и GameRules. Используется так: Game создаёт Board,
// GameController отдаёт её наружу, интерфейс читает клетки через контроллер.
// Что есть внутри: Squares — массив 8x8, на каждой клетке Piece или пусто.
// Initialize — стартовая позиция. GetPiece и SetPiece — чтение и запись по Position
// с проверкой границ. MovePiece — перенос фигуры с From на To без правил.
// IsInsideBoard и EnsureInsideBoard — проверка, что клетка внутри доски.
// SetupPawns и SetupBackRank — помощники для расстановки. ClearAllPieces — обнулить
// все клетки.
public class Board
{

    // Сама доска: массив 8x8. В каждой ячейке либо фигура Piece, либо null (пусто).
    public Piece?[,] Squares { get; } = new Piece?[8, 8];

    // Функция которая создаёт стартовую позицию. Сначала чистит все клетки в null,
    // потом ставит чёрных: ряд 0 фигуры, ряд 1 пешки; и белых: ряд 6 пешки, ряд 7
    // фигуры. Вызывается из конструктора Game при новой партии.
    public void Initialize()
    {
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                Squares[row, col] = null;
            }
        }

        SetupBackRank(0, PieceColor.Black);
        SetupPawns(1, PieceColor.Black);

        SetupPawns(6, PieceColor.White);
        SetupBackRank(7, PieceColor.White);
    }

    // Возвращает фигуру на клетке (или null, если пусто). Перед чтением проверяет,
    // что клетка внутри доски, иначе бросает исключение.
    public Piece? GetPiece(Position position)
    {
        EnsureInsideBoard(position);
        return Squares[position.Row, position.Col];
    }

    // Кладёт фигуру на клетку (или null, чтобы очистить). Тоже проверяет границы.
    public void SetPiece(Position position, Piece? piece)
    {
        EnsureInsideBoard(position);
        Squares[position.Row, position.Col] = piece;
    }

    // Переносит фигуру с клетки From на клетку To без всяких правил игры: просто
    // ставит её на новое место, а старую клетку очищает. Если на From пусто — ошибка.
    // Особые случаи (рокировка, взятие на проходе, превращение) обрабатываются выше,
    // в Game.ApplyMoveToBoard.
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

    // Проверяет, что клетка находится внутри доски (Row и Col в диапазоне 0..7).
    // Возвращает true/false, ничего не бросает. Используется в MoveGenerator при
    // генерации ходов, чтобы не выйти за край доски.
    public bool IsInsideBoard(Position position)
    {
        return position.Row >= 0 &&
               position.Row < 8 &&
               position.Col >= 0 &&
               position.Col < 8;
    }

    // Помощник расстановки: заполняет весь ряд пешками нужного цвета.
    private void SetupPawns(int row, PieceColor color)
    {
        for (var col = 0; col < 8; col++)
        {
            Squares[row, col] = new Piece(PieceType.Pawn, color);
        }
    }

    // Помощник расстановки: заполняет «тяжёлый» ряд в стандартном порядке —
    // ладья, конь, слон, ферзь, король, слон, конь, ладья.
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

    // Очищает всю доску — ставит null во все 64 клетки.
    public void ClearAllPieces()
    {
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                Squares[row, col] = null;
            }
        }
    }

    // Внутренняя проверка границ: если клетка вне доски — бросает исключение.
    // Защищает GetPiece/SetPiece/MovePiece от обращения за пределы массива.
    private void EnsureInsideBoard(Position position)
    {
        if (!IsInsideBoard(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position is outside the board.");
        }
    }
}
