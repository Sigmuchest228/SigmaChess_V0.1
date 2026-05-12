namespace SigmaChess.Services;

/// <summary>Приоритет: локальный pending (как обои) → URL из профиля → дефолт.</summary>
public static class UserAvatarPreview
{
    /// <param name="allowLocalPending">Ложь для чужих профилей — иначе локальный черновик аватара текущего пользователя может отобразиться у другого uid.</param>
    public static async Task<ImageSource> LoadAsync(string? userId, string? avatarUrl,
        CancellationToken cancellationToken, bool allowLocalPending = true)
    {
        if (allowLocalPending)
        {
            var pendingRaw = UserAvatarLocalStore.GetPendingLocalAvatarPath();
            if (!string.IsNullOrWhiteSpace(pendingRaw))
            {
                if (TryNormalizeExistingFilePath(pendingRaw, out var pendNorm))
                {
                    return ImageSource.FromFile(pendNorm);
                }

                UserAvatarLocalStore.ClearPendingLocalAvatarPath();
            }
        }

        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return ImageSource.FromFile("defaultsigma.jpg");
        }

        var uid = userId ?? "anon";
        var cachePath = Path.Combine(FileSystem.CacheDirectory, $"avatar_{uid}.jpg");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            await using var remote =
                await client.GetStreamAsync(avatarUrl, cancellationToken).ConfigureAwait(false);
            await using var fs = File.Create(cachePath);
            await remote.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);

            return ImageSource.FromFile(cachePath);
        }
        catch
        {
            return ImageSource.FromFile("defaultsigma.jpg");
        }
    }

    private static bool TryNormalizeExistingFilePath(string absolutePath, out string normalizedFullPath)
    {
        normalizedFullPath = string.Empty;
        try
        {
            var fp = Path.GetFullPath(absolutePath.Trim());
            if (!File.Exists(fp))
            {
                return false;
            }

            normalizedFullPath = fp;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
