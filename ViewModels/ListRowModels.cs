// Простые типы для списков и сеток (строки подписчиков, статистика, история ходов, партии).
// Один файл — в Solution Explorer меньше файлов; у каждой страницы по-прежнему своя ViewModel в отдельном .cs.

using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SigmaChess.Models;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

#region Режим раскладки партии и результат диалога «новая игра»

/// <summary>Раскладка экрана партии: обычная (экран) или «за доской» (обе стороны).</summary>
public enum GameLayoutMode
{
    /// <summary>Таймеры сверху/снизу доски, одна колонка записи справа.</summary>
    Casual,

    /// <summary>Таймеры сбоку, две колонки записей, чёрные фигуры повёрнуты на 180°.</summary>
    FaceToFace
}

/// <summary>Выбор времени и режима раскладки при старте партии.</summary>
public record NewGameSetupResult(
    bool Unlimited,
    bool SameTimeForBoth,
    int WhiteMinutes,
    int BlackMinutes,
    GameLayoutMode LayoutMode);

#endregion

#region Respect list and user search

public class RespectRowViewModel : ViewModelBase
{
    private ImageSource? _avatar;

    public RespectRowViewModel(string uid, string displayName, Func<Task> openProfile)
    {
        Uid = uid;
        DisplayName = displayName;
        TapCommand = new Command(async () => await openProfile());
    }

    public string Uid { get; }

    public string DisplayName { get; }

    public ImageSource? Avatar
    {
        get => _avatar;
        set
        {
            if (ReferenceEquals(_avatar, value))
            {
                return;
            }

            _avatar = value;
            OnPropertyChanged();
        }
    }

    public ICommand TapCommand { get; }
}

public class SearchUserRowViewModel : ViewModelBase
{
    private ImageSource? _avatar;

    public SearchUserRowViewModel(string uid, string displayName, bool showRespectButton, Func<Task> openProfile)
    {
        Uid = uid;
        DisplayName = displayName;
        ShowRespectButton = showRespectButton;
        TapCommand = new Command(async () => await openProfile());
    }

    public string Uid { get; }

    public string DisplayName { get; }

    public bool ShowRespectButton { get; }

    public ICommand TapCommand { get; }

    public ImageSource? Avatar
    {
        get => _avatar;
        set
        {
            if (ReferenceEquals(_avatar, value))
            {
                return;
            }

            _avatar = value;
            OnPropertyChanged();
        }
    }
}

#endregion

#region Профиль: строка статистики

/// <summary>Строка статистики для профиля (опциональные поля).</summary>
public class ProfileStatRowViewModel : ViewModelBase
{
    public ProfileStatRowViewModel(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }
}

#endregion

#region Запись ходов и сыгранные партии

/// <summary>Одна строка записи ходов: полный номер хода и полуходы белых/чёрных.</summary>
public class MoveHistoryRow
{
    public int FullMoveNumber { get; init; }

    public string WhiteMove { get; init; } = string.Empty;

    public string BlackMove { get; init; } = string.Empty;

    public string NumberLabel => $"{FullMoveNumber}.";
}

/// <summary>Строка списка сыгранных партий.</summary>
public class PlayedGameRowViewModel : ViewModelBase
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

    public static PlayedGameRowViewModel FromSummary(PastGame s)
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
            "timeout" => "Time",
            _ => endReason
        };
    }
}

#endregion
