using SigmaChess.Engine;

namespace SigmaChess.Services;

/// <summary>Восстанавливает цепочку движковых ходов из RTDB (FromPos/ToPos), в т. ч. при неоднозначном превращении.</summary>
public static class GameReplayMoveResolver
{
    public static bool TryResolve(IReadOnlyList<FirebaseMoveRecord> orderedMoves, out List<Move> resolved)
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
