using Microsoft.Maui.Graphics;

namespace SigmaChess.Services;

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
