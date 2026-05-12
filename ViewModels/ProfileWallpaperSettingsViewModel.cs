using System.Collections.ObjectModel;
using System.Windows.Input;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

public sealed class ProfileWallpaperSettingsViewModel : ViewModelBase
{
    private readonly FirebaseSyncRepository _firebase;
    private readonly IUserAppearanceService _appearance;
    private readonly IPhotoSourcePicker _photoPicker;

    private bool _presetThumbnailsHydrated;

    public ProfileWallpaperSettingsViewModel(
        FirebaseSyncRepository firebase,
        IUserAppearanceService appearance,
        IPhotoSourcePicker photoPicker)
    {
        _firebase = firebase;
        _appearance = appearance;
        _photoPicker = photoPicker;
        SelectWallpaperCommand = new Command<WallpaperGridItem>(async item => await SelectWallpaperAsync(item));
        GoBackCommand = new Command(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
            }
        });
    }

    public ObservableCollection<string> WallpaperPresetFiles => WallpaperPresetsCatalog.PresetFileNames;

    public ObservableCollection<WallpaperGridItem> WallpaperItems { get; } = new();

    public ICommand SelectWallpaperCommand { get; }

    public ICommand GoBackCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await WallpaperPresetsCatalog.EnsurePresetFileNamesLoadedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        if (!_presetThumbnailsHydrated)
        {
            _presetThumbnailsHydrated = true;
            await MainThread.InvokeOnMainThreadAsync(RebuildWallpaperItemsUI).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    MergeUserPhotosFromPersistedGallery();
                    EnsureSingleAddTileAtEnd();
                }).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>После выхода из аккаунта: пустая галерея, заново градиенты и пресеты.</summary>
    public async Task ReloadWallpaperGridAfterLogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await WallpaperPresetsCatalog.EnsurePresetFileNamesLoadedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            /* игнор */
        }

        await MainThread.InvokeOnMainThreadAsync(RebuildWallpaperItemsUI).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private void RebuildWallpaperItemsUI()
    {
        WallpaperItems.Clear();
        InsertGradientTilesAtTop();
        BuildPresetItems();
        MergeUserPhotosFromPersistedGallery();
        EnsureSingleAddTileAtEnd();
    }

    private void EnsureSingleAddTileAtEnd()
    {
        for (var i = WallpaperItems.Count - 1; i >= 0; i--)
        {
            if (WallpaperItems[i].Kind == WallpaperGridKind.AddTile)
            {
                WallpaperItems.RemoveAt(i);
            }
        }

        WallpaperItems.Add(new WallpaperGridItem { Kind = WallpaperGridKind.AddTile });
    }

    private void InsertGradientTilesAtTop()
    {
        var catalog = WallpaperGradientDefinitions.Catalog;
        for (var i = catalog.Count - 1; i >= 0; i--)
        {
            var (id, _) = catalog[i];
            WallpaperItems.Insert(
                0,
                new WallpaperGridItem
                {
                    Kind = WallpaperGridKind.Gradient,
                    GradientId = id,
                    TilePreviewBrush = WallpaperGradientDefinitions.PreviewBrushForId(id),
                    Thumb = null,
                });
        }
    }

    private void MergeUserPhotosFromPersistedGallery()
    {
        var persisted = WallpaperUserGalleryStore.LoadEntriesOrderedNewestFirst();
        var existingLocal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in WallpaperItems.Where(static z =>
                     z.Kind == WallpaperGridKind.UserPhoto && !string.IsNullOrEmpty(z.LocalCachePath)))
        {
            try
            {
                existingLocal.Add(Path.GetFullPath(i.LocalCachePath!));
            }
            catch
            {
                existingLocal.Add(i.LocalCachePath!.Trim());
            }
        }

        for (var i = persisted.Count - 1; i >= 0; i--)
        {
            var entry = persisted[i];
            if (string.IsNullOrWhiteSpace(entry.LocalPath) || !File.Exists(entry.LocalPath))
            {
                continue;
            }

            string fullLocal;
            try
            {
                fullLocal = Path.GetFullPath(entry.LocalPath);
            }
            catch
            {
                continue;
            }

            if (existingLocal.Contains(fullLocal))
            {
                continue;
            }

            WallpaperItems.Insert(
                0,
                new WallpaperGridItem
                {
                    Kind = WallpaperGridKind.UserPhoto,
                    LocalCachePath = fullLocal,
                    RemoteDownloadUrl =
                        string.IsNullOrWhiteSpace(entry.RemoteUrl) ? null : entry.RemoteUrl.Trim(),
                    Thumb = ImageSource.FromFile(fullLocal),
                });
            existingLocal.Add(fullLocal);
        }
    }

    private void BuildPresetItems()
    {
        var existingPresets = new HashSet<string>(
            WallpaperItems.Where(static i => i.Kind == WallpaperGridKind.Preset && i.PresetFileName is not null)
                .Select(static i => i.PresetFileName!),
            StringComparer.OrdinalIgnoreCase);

        foreach (var name in WallpaperPresetsCatalog.PresetFileNames)
        {
            if (existingPresets.Contains(name))
            {
                continue;
            }

            var logical = WallpaperPresetsCatalog.LogicalName(name);
            var thumb = ImageSource.FromStream(async _ =>
            {
                await using var s =
                    await FileSystem.OpenAppPackageFileAsync(logical).WaitAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                var ms = new MemoryStream();
                await s.CopyToAsync(ms, CancellationToken.None).ConfigureAwait(false);
                ms.Position = 0;
                return ms;
            });

            WallpaperItems.Add(new WallpaperGridItem
            {
                Kind = WallpaperGridKind.Preset,
                PresetFileName = name,
                Thumb = thumb,
            });
        }
    }

    private async Task SelectWallpaperAsync(WallpaperGridItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            if (item.Kind == WallpaperGridKind.AddTile)
            {
                var src = await _photoPicker.PickSourceAsync().ConfigureAwait(false);
                if (src == PickPhotoSource.Gallery)
                {
                    await PickGalleryAsync().ConfigureAwait(false);
                }
                else if (src == PickPhotoSource.Camera)
                {
                    await CaptureWallpaperAsync().ConfigureAwait(false);
                }

                return;
            }

            if (item.Kind == WallpaperGridKind.Gradient && !string.IsNullOrEmpty(item.GradientId))
            {
                WallpaperUserGalleryStore.ClearPendingLocalWallpaperPath();
                var key = WallpaperGradientDefinitions.SentinelPresetKey(item.GradientId);
                await _firebase.PatchUserAppearanceAsync(new Dictionary<string, object?>
                    {
                        ["WallpaperPreset"] = key,
                        ["WallpaperCustomUrl"] = null,
                    }).ConfigureAwait(false);
                await _appearance.ApplyPresetAsync(key).ConfigureAwait(false);
                return;
            }

            if (item.Kind == WallpaperGridKind.Preset && !string.IsNullOrEmpty(item.PresetFileName))
            {
                WallpaperUserGalleryStore.ClearPendingLocalWallpaperPath();
                await _firebase.PatchUserAppearanceAsync(new Dictionary<string, object?>
                    {
                        ["WallpaperPreset"] = item.PresetFileName,
                        ["WallpaperCustomUrl"] = null,
                    }).ConfigureAwait(false);
                await _appearance.ApplyPresetAsync(item.PresetFileName).ConfigureAwait(false);
                return;
            }

            if (item.Kind != WallpaperGridKind.UserPhoto)
            {
                return;
            }

            if (!string.IsNullOrEmpty(item.RemoteDownloadUrl))
            {
                WallpaperUserGalleryStore.ClearPendingLocalWallpaperPath();
                await _firebase.PatchUserAppearanceAsync(new Dictionary<string, object?>
                    {
                        ["WallpaperCustomUrl"] = item.RemoteDownloadUrl,
                        ["WallpaperPreset"] = null,
                    }).ConfigureAwait(false);
                await _appearance.ApplyRemoteWallpaperAsync(item.RemoteDownloadUrl).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrEmpty(item.LocalCachePath))
            {
                WallpaperUserGalleryStore.SetPendingLocalWallpaperPath(item.LocalCachePath);
                await _appearance.ApplyLocalWallpaperFromFileAsync(item.LocalCachePath).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                    await ConfirmPopup.ShowAsync("Wallpapers", $"Could not apply: {ex.Message}", "OK"))
                .ConfigureAwait(false);
        }
    }

    private async Task PickGalleryAsync()
    {
        Stream? stream = null;
        try
        {
            stream = await PhotoMediaService.TryOpenGalleryPhotoAsync("Wallpaper").ConfigureAwait(false);
            if (stream is null)
            {
                return;
            }

            await SaveAndApplyLocalWallpaperAsync(stream).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                    await ConfirmPopup.ShowAsync("Wallpapers", $"Could not pick image: {ex.Message}", "OK"))
                .ConfigureAwait(false);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    private async Task CaptureWallpaperAsync()
    {
        Stream? stream = null;
        try
        {
            stream = await PhotoMediaService.TryOpenCameraPhotoAsync("Wallpaper").ConfigureAwait(false);
            if (stream is null)
            {
                return;
            }

            await SaveAndApplyLocalWallpaperAsync(stream).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                    await ConfirmPopup.ShowAsync("Wallpapers", $"Could not capture: {ex.Message}", "OK"))
                .ConfigureAwait(false);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    private async Task SaveAndApplyLocalWallpaperAsync(Stream stream)
    {
        var cachePath = Path.Combine(FileSystem.CacheDirectory, $"wallpaper_user_{Guid.NewGuid():N}.jpg");
        await using (var fs = File.Create(cachePath))
        {
            await stream.CopyToAsync(fs).ConfigureAwait(false);
        }

        string fullPickPath;
        try
        {
            fullPickPath = Path.GetFullPath(cachePath);
        }
        catch
        {
            fullPickPath = cachePath;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
            {
                WallpaperItems.Insert(
                    0,
                    new WallpaperGridItem
                    {
                        Kind = WallpaperGridKind.UserPhoto,
                        LocalCachePath = fullPickPath,
                        Thumb = ImageSource.FromFile(fullPickPath),
                    });
                WallpaperUserGalleryStore.UpsertFront(fullPickPath, null);
                WallpaperUserGalleryStore.SetPendingLocalWallpaperPath(fullPickPath);
            }).ConfigureAwait(false);

        await _appearance.ApplyLocalWallpaperFromFileAsync(fullPickPath).ConfigureAwait(false);
    }
}
