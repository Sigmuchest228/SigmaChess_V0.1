using System.Globalization;
using Microsoft.Maui.Graphics;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

/// <summary>Строка списка сыгранных партий.</summary>
public sealed class PlayedGameRowViewModel : ViewModelBase
{
    public PlayedGameRowViewModel(string gameId, string outcomeTitle, string detailLine, Color outcomeColor)
    {
        GameId = gameId;
        TitleLine = outcomeTitle;
        DetailLine = detailLine;
        OutcomeColor = outcomeColor;
    }

    public string GameId { get; }

    /// <summary>Исход партии: White wins / Black wins / Draw.</summary>
    public string TitleLine { get; }

    public string DetailLine { get; }

    /// <summary>Цвет текста исхода — по победившей стороне.</summary>
    public Color OutcomeColor { get; }

    public static PlayedGameRowViewModel FromSummary(PlayedGameSummary s)
    {
        var dateStr = s.EndedAt?.ToLocalTime().ToString("MMM d, yyyy · HH:mm", CultureInfo.CurrentCulture) ?? "—";
        var detail = $"{HumanEndReason(s.EndReason)} · {dateStr}";
        var color = ChessOutcomePalette.TextForWinner(s.GameWinner);
        var title = ChessOutcomePalette.ListOutcomeTitle(s.GameWinner);
        return new PlayedGameRowViewModel(s.GameId, title, detail, color);
    }

    private static string HumanEndReason(string endReason)
    {
        if (string.IsNullOrWhiteSpace(endReason))
        {
            return "—";
        }

        return endReason.ToLowerInvariant() switch
        {
            "checkmate" => "Checkmate",
            "stalemate" => "Stalemate",
            "fifty_move" => "50-move rule",
            "repetition" => "Repetition",
            "insufficient_material" => "Insufficient material",
            _ => endReason
        };
    }
}
