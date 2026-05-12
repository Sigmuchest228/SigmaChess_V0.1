using Microsoft.Maui.Storage;
using Newtonsoft.Json;

namespace SigmaChess.Services;

/// <summary>Сохраняет добавленные пользователем обои (локальный путь + опционально URL после Storage).</summary>
public static class WallpaperUserGalleryStore
{
    private const string GalleryJsonKey = "WallpaperUserGalleryJson";

    internal const string PendingLocalWallpaperPathKey = "WallpaperPendingLocalPath";

    private const int MaxEntries = 48;

    public sealed class Entry
    {
        public Entry()
        {
        }

        public Entry(string localPath, string? remoteUrl)
        {
            LocalPath = localPath;
            RemoteUrl = remoteUrl;
        }

        [JsonProperty("localPath")]
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>Если файл перезалили в Storage после первого сохранения — обновить.</summary>
        [JsonProperty("remoteUrl")]
        public string? RemoteUrl { get; set; }
    }

    public static void SetPendingLocalWallpaperPath(string absolutePath)
    {
        Preferences.Set(PendingLocalWallpaperPathKey, absolutePath);
    }

    public static void ClearPendingLocalWallpaperPath()
    {
        Preferences.Remove(PendingLocalWallpaperPathKey);
    }

    public static string? GetPendingLocalWallpaperPath() =>
        Preferences.Get(PendingLocalWallpaperPathKey, string.Empty)?.Trim();

    /// <summary>Недавнее — в начале списка.</summary>
    public static List<Entry> LoadEntriesOrderedNewestFirst()
    {
        var json = Preferences.Get(GalleryJsonKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonConvert.DeserializeObject<List<Entry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void UpsertFront(string absoluteLocalPath, string? remoteDownloadUrlOrNull)
    {
        var list = LoadEntriesOrderedNewestFirst();
        list.RemoveAll(e => PathsEqual(e.LocalPath, absoluteLocalPath));
        list.Insert(
            0,
            new Entry(
                absoluteLocalPath,
                string.IsNullOrWhiteSpace(remoteDownloadUrlOrNull) ? null : remoteDownloadUrlOrNull.Trim()));

        while (list.Count > MaxEntries)
        {
            list.RemoveAt(list.Count - 1);
        }

        SaveAll(list);
    }

    /// <summary>Обновить только URL для существующего пути.</summary>
    public static void UpdateRemoteUrl(string absoluteLocalPath, string remoteDownloadUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteDownloadUrl))
        {
            return;
        }

        var list = LoadEntriesOrderedNewestFirst();
        var idx = list.FindIndex(e => PathsEqual(e.LocalPath, absoluteLocalPath));
        if (idx < 0)
        {
            UpsertFront(absoluteLocalPath, remoteDownloadUrl);
            return;
        }

        list[idx].RemoteUrl = remoteDownloadUrl.Trim();
        SaveAll(list);
    }

    private static void SaveAll(List<Entry> list)
    {
        var json = JsonConvert.SerializeObject(list);
        Preferences.Set(GalleryJsonKey, json);
    }

    /// <summary>Сброс локальной галереи и pending-пути обоев (например при выходе из аккаунта).</summary>
    public static void ClearAll()
    {
        Preferences.Remove(GalleryJsonKey);
        Preferences.Remove(PendingLocalWallpaperPathKey);
    }

    internal static bool PathsEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static bool RemoteUrlsEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return string.Equals(Uri.UnescapeDataString(a.Trim()), Uri.UnescapeDataString(b.Trim()), StringComparison.OrdinalIgnoreCase)
               || string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
