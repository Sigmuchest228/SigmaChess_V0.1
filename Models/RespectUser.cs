namespace SigmaChess.Models;

/// <summary>Игрок в списке respect или в результатах поиска.</summary>
public class RespectUser
{
    public string Uid { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? AvatarUrl { get; init; }
}
