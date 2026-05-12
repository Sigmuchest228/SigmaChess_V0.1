namespace SigmaChess.Services;

/// <summary>Титул по числу решённых задач; источник истины — <see cref="UserProfileRtdbDto.PuzzlesSolved"/>.</summary>
public static class UserSigmaRank
{
    public const string Baby = "baby sigma";

    public const string Beginier = "beginier sigma";

    public const string Intermediate = "intermidiate sigma";

    public const string ProMax = "sigma pro max";

    public static string GetRankTitle(int puzzlesSolved)
    {
        if (puzzlesSolved >= 50)
        {
            return ProMax;
        }

        if (puzzlesSolved >= 20)
        {
            return Intermediate;
        }

        if (puzzlesSolved >= 5)
        {
            return Beginier;
        }

        return Baby;
    }

    public static int NormalizePuzzlesSolved(int? value) => Math.Max(0, value ?? 0);
}
