using System.ComponentModel;
using Microsoft.Maui.Controls;

namespace SigmaChess.Services;

/// <summary>Состояние фона главной и профиля; синхронизация из RTDB и локальных пресетов.</summary>
public interface IUserAppearanceService : INotifyPropertyChanged
{
    ImageSource? WallpaperBackground { get; }

    /// <summary>Подложка под картинку; <c>null</c> — цвет дефолта как <c>GlassPageFallback</c>.</summary>
    Brush? BackdropBaseBrush { get; }

    Task ApplyFromProfileAsync(UserProfileRtdbDto? profile, CancellationToken cancellationToken = default);

    Task ApplyPresetAsync(string presetFileName, CancellationToken cancellationToken = default);

    Task ApplyRemoteWallpaperAsync(string downloadUrl, CancellationToken cancellationToken = default);

    Task ApplyLocalWallpaperFromFileAsync(string absolutePath, CancellationToken cancellationToken = default);

    void ClearWallpaper();

    /// <summary>Сброс фона как при первом запуске: нет bitmap, базовый цвет приложения по умолчанию.</summary>
    void ResetBackdropToDefault();
}

public sealed class UserAppearanceService : IUserAppearanceService
{
    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

    private ImageSource? _wallpaperBackground;
    private Brush? _backdropBaseBrush;

    public ImageSource? WallpaperBackground
    {
        get => _wallpaperBackground;
        private set
        {
            _wallpaperBackground = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WallpaperBackground)));
        }
    }

    public Brush? BackdropBaseBrush
    {
        get => _backdropBaseBrush;
        private set
        {
            _backdropBaseBrush = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackdropBaseBrush)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ClearWallpaper()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            WallpaperBackground = null;
            BackdropBaseBrush = null;
        });
    }

    public void ResetBackdropToDefault()
    {
        ClearWallpaper();
    }

    public async Task ApplyFromProfileAsync(UserProfileRtdbDto? profile, CancellationToken cancellationToken = default)
    {
        var pendingRaw = WallpaperUserGalleryStore.GetPendingLocalWallpaperPath();
        if (!string.IsNullOrWhiteSpace(pendingRaw))
        {
            if (TryNormalizeExistingFilePath(pendingRaw, out var pendNorm))
            {
                await ApplyLocalWallpaperFromFileAsync(pendNorm, cancellationToken).ConfigureAwait(false);
                return;
            }

            WallpaperUserGalleryStore.ClearPendingLocalWallpaperPath();
        }

        if (profile is null)
        {
            ClearWallpaper();
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile.WallpaperCustomUrl))
        {
            await ApplyRemoteWallpaperAsync(profile.WallpaperCustomUrl.Trim(), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile.WallpaperPreset))
        {
            await ApplyPresetAsync(profile.WallpaperPreset.Trim(), cancellationToken).ConfigureAwait(false);
            return;
        }

        ClearWallpaper();
    }

    public async Task ApplyPresetAsync(string presetFileName, CancellationToken cancellationToken = default)
    {
        var name = presetFileName.Trim();
        if (WallpaperGradientDefinitions.IsGradientPreset(name))
        {
            WallpaperGradientDefinitions.TryGetGradientBrush(name, out var brush);
            await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    BackdropBaseBrush = brush;
                    WallpaperBackground = null;
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var logical = WallpaperPresetsCatalog.LogicalName(name);

        var src = ImageSource.FromStream(async _ =>
        {
            await using var packageStream =
                await FileSystem.OpenAppPackageFileAsync(logical).WaitAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            var ms = new MemoryStream();
            await packageStream.CopyToAsync(ms, CancellationToken.None).ConfigureAwait(false);
            ms.Position = 0;
            return ms;
        });

        await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BackdropBaseBrush = null;
                WallpaperBackground = src;
            }).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplyRemoteWallpaperAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        var urlTrim = downloadUrl.Trim();
        var cachePath = Path.Combine(FileSystem.CacheDirectory, WallpaperCacheFileName(urlTrim));

        try
        {
            using var response =
                await Http.GetAsync(urlTrim, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var networkStream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = File.Create(cachePath);
            await networkStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ClearWallpaper();
            return;
        }

        var src = ImageSource.FromFile(cachePath);
        await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BackdropBaseBrush = null;
                WallpaperBackground = src;
            }).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApplyLocalWallpaperFromFileAsync(string absolutePath, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeExistingFilePath(absolutePath, out var normPath))
        {
            ClearWallpaper();
            return;
        }

        var src = ImageSource.FromFile(normPath);
        await MainThread.InvokeOnMainThreadAsync(() =>
            {
                BackdropBaseBrush = null;
                WallpaperBackground = src;
            }).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
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

    private static string WallpaperCacheFileName(string url)
    {
        var trimmed = url.Trim();
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in trimmed)
            {
                hash = (hash ^ c) * 16777619;
            }

            return $"wall_custom_{hash:x8}_{trimmed.Length}.bin";
        }
    }
}
