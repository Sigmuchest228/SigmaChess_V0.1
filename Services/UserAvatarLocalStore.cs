using Microsoft.Maui.Storage;

namespace SigmaChess.Services;

/// <summary>
/// Локальный аватар как pending-обои: путь в Preferences + файл в кэше, без Firebase Storage.
/// </summary>
public static class UserAvatarLocalStore
{
    internal const string PendingLocalAvatarPathKey = "AvatarPendingLocalPath";

    public static void SetPendingLocalAvatarPath(string absolutePath) =>
        Preferences.Set(PendingLocalAvatarPathKey, absolutePath);

    public static void ClearPendingLocalAvatarPath() =>
        Preferences.Remove(PendingLocalAvatarPathKey);

    public static string? GetPendingLocalAvatarPath() =>
        Preferences.Get(PendingLocalAvatarPathKey, string.Empty)?.Trim();
}
