
using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SigmaChess.Models;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

#region Режим раскладки партии и результат диалога «новая игра»

// Режим раскладки доски: Casual — обычная игра (доска и панель ходов снизу),
// FaceToFace — игра «лицом к лицу» за одним устройством, когда фигуры одной стороны
// разворачиваются.
public enum GameLayoutMode
{

    Casual,

    FaceToFace
}

// Результат диалога настройки новой игры: без лимита времени или нет, одинаковое ли
// время у обоих, минуты белых и чёрных, и выбранный режим раскладки. Возвращается из
// попапа настройки и применяется в GameViewModel.
public record NewGameSetupResult(
    bool Unlimited,
    bool SameTimeForBoth,
    int WhiteMinutes,
    int BlackMinutes,
    GameLayoutMode LayoutMode);

#endregion

#region Respect list and user search

// ViewModel одной строки в списке «уважения» (respect): другой пользователь, на
// которого можно нажать и открыть его профиль. Хранит id, имя, аватар и команду тапа.
public class RespectRowViewModel : ViewModelBase
{
    private ImageSource? _avatar;

    // Конструктор: задаёт данные пользователя и команду открытия профиля по тапу.
    public RespectRowViewModel(string uid, string displayName, Func<Task> openProfile)
    {
        Uid = uid;
        DisplayName = displayName;
        TapCommand = new Command(async () => await openProfile());
    }

    public string Uid { get; }

    public string DisplayName { get; }

    // Аватар пользователя. Грузится отдельно (асинхронно), поэтому при установке
    // уведомляет интерфейс, чтобы картинка появилась.
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

// ViewModel строки в результатах поиска пользователей. Похожа на строку respect, но
// дополнительно знает, показывать ли кнопку «добавить в respect» (ShowRespectButton).
public class SearchUserRowViewModel : ViewModelBase
{
    private ImageSource? _avatar;

    // Конструктор: данные найденного пользователя, нужна ли кнопка respect и команда
    // открытия профиля.
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

    // Аватар найденного пользователя (грузится асинхронно, уведомляет интерфейс).
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

// ViewModel строки статистики в профиле: просто пара «подпись — значение»
// (например «Сыграно партий» — «42»).
public class ProfileStatRowViewModel : ViewModelBase
{
    // Конструктор: задаёт подпись и значение строки.
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

// Одна строка в таблице ходов: номер хода и ходы белых и чёрных в этой паре
// (в нотации, например «e4» / «e5»). Отображается в списке ходов партии.
public class MoveHistoryRow
{
    public int FullMoveNumber { get; init; }

    public string WhiteMove { get; init; } = string.Empty;

    public string BlackMove { get; init; } = string.Empty;

    // Готовая подпись номера хода с точкой, например «1.».
    public string NumberLabel => $"{FullMoveNumber}.";
}

// ViewModel строки в списке сыгранных партий: id партии, заголовок исхода
// (кто победил), строка с деталями (причина окончания и дата) и цвет исхода.
public class PlayedGameRowViewModel : ViewModelBase
{
    // Конструктор: задаёт все поля строки сыгранной партии.
    public PlayedGameRowViewModel(string gameId, string outcomeTitle, string detailLine, Color outcomeColor)
    {
        GameId = gameId;
        TitleLine = outcomeTitle;
        DetailLine = detailLine;
        OutcomeColor = outcomeColor;
    }

    public string GameId { get; }

    public string TitleLine { get; }

    public string DetailLine { get; }

    public Color OutcomeColor { get; }

    // Фабрика: собирает строку списка из сводки партии (PastGame). Форматирует дату,
    // переводит причину окончания в текст, подбирает цвет и заголовок исхода.
    public static PlayedGameRowViewModel FromSummary(PastGame s)
    {
        var dateStr = s.EndedAt?.ToLocalTime().ToString("MMM d, yyyy · HH:mm", CultureInfo.CurrentCulture) ?? "—";
        var detail = $"{HumanEndReason(s.EndReason)} · {dateStr}";
        var color = ChessOutcomePalette.TextForWinner(s.GameWinner);
        var title = ChessOutcomePalette.ListOutcomeTitle(s.GameWinner);
        return new PlayedGameRowViewModel(s.GameId, title, detail, color);
    }

    // Переводит техническую причину окончания партии (например «fifty_move») в
    // понятную человеку подпись (например «50-move rule»).
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
