namespace SigmaChess.Models;

public class RespectUser
{
    public string Uid { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? AvatarUrl { get; init; }
}
