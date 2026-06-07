namespace SigmaChess.Models;

// Краткая модель игрока для списка «уважения» и результатов поиска: id (Uid), имя для
// показа (DisplayName) и при наличии ссылка на аватар (AvatarUrl). Свойства init-only.
// Используется в RespectsPageViewModel для построения строк списка и поиска.
public class RespectUser
{
    public string Uid { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? AvatarUrl { get; init; }
}
