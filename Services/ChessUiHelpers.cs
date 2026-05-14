// Вспомогательный код для UI шахмат: алгебра полей, цвета/подписи исхода, разбор ходов реплея из RTDB.
// Объединено в один файл — меньше узлов в папке Services.

using System.Text;
using Microsoft.Maui.Graphics;
using SigmaChess.Engine;
using SigmaChess.Models;

namespace SigmaChess.Services;

#region Алгебраическая нотация полей и короткая запись хода

/// <summary>
/// Координаты движка: row 0 — верх доски (чёрные), col 0 — файл a.
/// В алгебре: rank = 8 - row, file = a + col.
/// </summary>
public static class AlgebraicNotation
{
    public static string ToSquare(SigmaChess.Engine.Position pos) =>
        $"{(char)('a' + pos.Col)}{8 - pos.Row}";

    /// <summary>Парсинг поля вроде <c>e4</c> в координаты движка.</summary>
    public static bool TryParseSquare(string? square, out Position pos)
    {
        pos = default;
        if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
        {
            return false;
        }

        var file = char.ToLowerInvariant(square[0]);
        if (file is < 'a' or > 'h')
        {
            return false;
        }

        if (!char.IsDigit(square[1]))
        {
            return false;
        }

        var rankDigit = square[1] - '0';
        if (rankDigit is < 1 or > 8)
        {
            return false;
        }

        var col = file - 'a';
        var row = 8 - rankDigit;
        pos = new Position(row, col);
        return true;
    }

    private static readonly GameRules SanRules = new(new MoveGenerator());

    /// <summary>
    /// Стандартная алгебраическая запись (SAN) для списка ходов: <c>e4</c>, <c>Nf3</c>, <c>O-O</c>, <c>exd5</c>, <c>e8=Q+</c>.
    /// <paramref name="moveIndex"/> — индекс <paramref name="move"/> в <paramref name="allMoves"/> (0 = первый полуход).
    /// </summary>
    public static string MoveToShortNotation(Move move, IReadOnlyList<Move> allMoves, int moveIndex)
    {
        if (moveIndex < 0 || moveIndex >= allMoves.Count)
        {
            return FallbackLong(move);
        }

        var g = new Game();
        for (var j = 0; j < moveIndex; j++)
        {
            if (!g.MakeMove(allMoves[j]))
            {
                return FallbackLong(move);
            }
        }

        return MoveToSan(move, g) ?? FallbackLong(move);
    }

    private static string FallbackLong(Move move) =>
        $"{ToSquare(move.From)}-{ToSquare(move.To)}";

    private static string? MoveToSan(Move move, Game g)
    {
        var board = g.Board;
        var piece = board.GetPiece(move.From);
        if (piece is null || piece.Color != g.CurrentTurn)
        {
            return null;
        }

        var movingColor = piece.Color;
        var opponent = movingColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

        // Рокировка: король на два файла.
        if (piece.Type == PieceType.King && Math.Abs(move.To.Col - move.From.Col) == 2)
        {
            var castle = move.To.Col == 6 ? "O-O" : "O-O-O";
            return AppendCheckSuffix(castle, move, g, opponent);
        }

        var dest = ToSquare(move.To);
        var capturedOnTo = board.GetPiece(move.To);
        var isEnPassant = piece.Type == PieceType.Pawn
            && g.EnPassantTarget == move.To
            && capturedOnTo is null;
        var isCapture = capturedOnTo is not null || isEnPassant;

        string core;
        if (piece.Type == PieceType.Pawn)
        {
            var fromFile = (char)('a' + move.From.Col);
            if (isCapture)
            {
                core = $"{fromFile}x{dest}";
            }
            else
            {
                core = dest;
            }

            if (move.Promotion is { } promo)
            {
                core += "=" + PromotionLetter(promo);
            }
        }
        else
        {
            var sb = new StringBuilder();
            sb.Append(PieceSanLetter(piece.Type));

            var ambiguousFrom = new List<Position>();
            foreach (var m in SanRules.GetAllLegalMoves(board, movingColor, g))
            {
                if (m.To != move.To)
                {
                    continue;
                }

                var p = board.GetPiece(m.From);
                if (p?.Type == piece.Type && p?.Color == piece.Color)
                {
                    ambiguousFrom.Add(m.From);
                }
            }

            ambiguousFrom = ambiguousFrom.Distinct().ToList();
            if (ambiguousFrom.Count > 1)
            {
                sb.Append(Disambiguate(move.From, ambiguousFrom));
            }

            if (isCapture)
            {
                sb.Append('x');
            }

            sb.Append(dest);
            core = sb.ToString();
        }

        return AppendCheckSuffix(core, move, g, opponent);
    }

    private static string AppendCheckSuffix(string core, Move move, Game gBefore, PieceColor opponent)
    {
        var trial = new Game();
        for (var j = 0; j < gBefore.History.Count; j++)
        {
            trial.MakeMove(gBefore.History[j]);
        }

        if (!trial.MakeMove(move))
        {
            return core;
        }

        var result = SanRules.GetGameResult(trial.Board, trial.CurrentTurn, trial);
        if (result == GameResult.Checkmate)
        {
            return core + '#';
        }

        if (SanRules.IsKingInCheck(trial.Board, opponent))
        {
            return core + '+';
        }

        return core;
    }

    private static char PieceSanLetter(PieceType t) =>
        t switch
        {
            PieceType.Knight => 'N',
            PieceType.Bishop => 'B',
            PieceType.Rook => 'R',
            PieceType.Queen => 'Q',
            PieceType.King => 'K',
            _ => '?',
        };

    private static char PromotionLetter(PieceType t) =>
        t switch
        {
            PieceType.Queen => 'Q',
            PieceType.Rook => 'R',
            PieceType.Bishop => 'B',
            PieceType.Knight => 'N',
            _ => 'Q',
        };

    /// <summary>
    /// Если на одно поле могут пойти несколько фигур одного типа, добавляем файл, ранг или оба (FIDE SAN).
    /// </summary>
    private static string Disambiguate(Position from, List<Position> sources)
    {
        if (sources.Count <= 1)
        {
            return string.Empty;
        }

        var sameFile = sources.Count(s => s.Col == from.Col);
        if (sameFile == 1)
        {
            return ((char)('a' + from.Col)).ToString();
        }

        var sameRank = sources.Count(s => s.Row == from.Row);
        if (sameRank == 1)
        {
            return (8 - from.Row).ToString();
        }

        return $"{(char)('a' + from.Col)}{8 - from.Row}";
    }
}

#endregion

#region Цвета и подписи исхода партии

/// <summary>Цвет подписи исхода партии по победившей стороне (шахматные «белые/чёрные» оттенки).</summary>
public static class ChessOutcomePalette
{
    public static string NormalizeWinner(string? winner)
    {
        if (string.IsNullOrWhiteSpace(winner))
        {
            return string.Empty;
        }

        if (string.Equals(winner, "White", StringComparison.OrdinalIgnoreCase))
        {
            return "White";
        }

        if (string.Equals(winner, "Black", StringComparison.OrdinalIgnoreCase))
        {
            return "Black";
        }

        if (string.Equals(winner, "Draw", StringComparison.OrdinalIgnoreCase))
        {
            return "Draw";
        }

        return string.Empty;
    }

    public static Color TextForWinner(string normalizedWinner) =>
        normalizedWinner switch
        {
            "White" => Color.FromArgb("#F3ECD9"),
            "Black" => Color.FromArgb("#B58863"),
            "Draw" => Color.FromArgb("#94A3B8"),
            _ => Color.FromArgb("#94A3B8"),
        };

    /// <summary>Подпись исхода в списках сыгранных партий (нейтрально по победителю).</summary>
    public static string ListOutcomeTitle(string normalizedWinner) =>
        normalizedWinner switch
        {
            "White" => "White wins",
            "Black" => "Black wins",
            "Draw" => "Draw",
            _ => "—",
        };

    /// <summary>Краткая подпись победителя для реплея.</summary>
    public static string ReplayWinnerCaption(string normalizedWinner) =>
        normalizedWinner switch
        {
            "White" => "White won",
            "Black" => "Black won",
            "Draw" => "Draw",
            _ => string.Empty,
        };
}

#endregion

#region Разбор цепочки ходов реплея из записей Firebase

/// <summary>Восстанавливает цепочку движковых ходов из RTDB (FromPos/ToPos), в т. ч. при неоднозначном превращении.</summary>
public static class GameReplayMoveResolver
{
    public static bool TryResolve(IReadOnlyList<SavedMove> orderedMoves, out List<Move> resolved)
    {
        resolved = [];
        if (orderedMoves.Count == 0)
        {
            return false;
        }

        var rules = new GameRules(new MoveGenerator());
        var prefix = new List<Move>();
        List<Move>? built = null;

        bool Dfs(int idx)
        {
            if (idx >= orderedMoves.Count)
            {
                built = [..prefix];
                return true;
            }

            var rec = orderedMoves[idx];
            if (!AlgebraicNotation.TryParseSquare(rec.FromPos, out var from)
                || !AlgebraicNotation.TryParseSquare(rec.ToPos, out var to))
            {
                return false;
            }

            var g = new Game();
            foreach (var m in prefix)
            {
                if (!g.MakeMove(m))
                {
                    return false;
                }
            }

            var candidates = rules.GetLegalMovesFrom(g.Board, from, g)
                .Where(m => m.To == to)
                .ToList();

            if (candidates.Count == 0)
            {
                return false;
            }

            foreach (var cand in OrderCandidates(candidates))
            {
                prefix.Add(cand);
                if (Dfs(idx + 1))
                {
                    return true;
                }

                prefix.RemoveAt(prefix.Count - 1);
            }

            return false;
        }

        if (!Dfs(0) || built is null)
        {
            return false;
        }

        resolved = built;
        return true;
    }

    private static IEnumerable<Move> OrderCandidates(List<Move> candidates)
    {
        foreach (var m in candidates.Where(x => x.Promotion is null))
        {
            yield return m;
        }

        PieceType[] promoOrder = [PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight];
        foreach (var pt in promoOrder)
        {
            foreach (var m in candidates.Where(x => x.Promotion == pt))
            {
                yield return m;
            }
        }
    }
}

#endregion
