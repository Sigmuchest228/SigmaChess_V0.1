using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using SigmaChess.Views;

namespace SigmaChess.Services;

// Выбор источника фото, разрешения, галерея/камера, локальный pending-аватар и превью по URL.
#region Интерфейс и результат выбора (Gallery / Camera)

public interface IPhotoSourcePicker
{
    Task<PickPhotoSource> PickSourceAsync(CancellationToken cancellationToken = default);
}

public enum PickPhotoSource
{
    Cancel,
    Gallery,
    Camera,
}

/// <summary>Показывает попап выбора Gallery/Camera при добавлении фото (обои, аватар).</summary>
public class PhotoSourcePicker : IPhotoSourcePicker
{
    public Task<PickPhotoSource> PickSourceAsync(CancellationToken cancellationToken = default) =>
        PickPhotoSourcePopup.ShowAsync().WaitAsync(cancellationToken);
}

#endregion

#region Разрешения и открытие потока фото

/// <summary>Разрешения и системный выбор фото из галереи или камеры (для профиля).</summary>
public static class PhotoMediaService
{
    public static Task<bool> EnsurePhotosPermissionAsync() =>
        EnsurePermissionAsync<Permissions.Photos>();

    public static Task<bool> EnsureCameraPermissionAsync() =>
        EnsurePermissionAsync<Permissions.Camera>();

    public static async Task<Stream?> TryOpenGalleryPhotoAsync(string pickerTitle)
    {
        try
        {
#if !WINDOWS
            if (!await EnsurePhotosPermissionAsync().ConfigureAwait(false))
            {
                await ShowPermissionDeniedAsync().ConfigureAwait(false);
                return null;
            }
#endif

            return await OpenPickPhotoStreamCrossPlatformAsync(pickerTitle).ConfigureAwait(false);
        }
        catch (PermissionException)
        {
            await ShowPermissionDeniedAsync().ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Открывает поток снимка с камеры; на Windows при недоступности камеры — fallback через файловый пикер.</summary>
    public static async Task<Stream?> TryOpenCameraPhotoAsync(string pickerTitle)
    {
        Stream? stream = null;
        try
        {
#if !WINDOWS
            if (!await EnsureCameraPermissionAsync().ConfigureAwait(false))
            {
                await ShowPermissionDeniedAsync().ConfigureAwait(false);
                return null;
            }
#endif

#if WINDOWS
            try
            {
                var shot =
                    await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = pickerTitle })
                        .ConfigureAwait(false);
                if (shot is not null)
                {
                    stream = await shot.OpenReadAsync().ConfigureAwait(false);
                }
            }
            catch (FeatureNotSupportedException)
            {
                /* fallback ниже */
            }

            if (stream is null)
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = pickerTitle,
                    FileTypes = FilePickerFileType.Images,
                }).ConfigureAwait(false);

                if (result is not null)
                {
                    stream = await result.OpenReadAsync().ConfigureAwait(false);
                }
            }
#else
            var photo =
                await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = pickerTitle })
                    .ConfigureAwait(false);
            if (photo is null)
            {
                return null;
            }

            stream = await photo.OpenReadAsync().ConfigureAwait(false);
#endif

            return stream;
        }
        catch (FeatureNotSupportedException)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                    await ConfirmPopup.ShowAsync(pickerTitle, "Camera is not available on this device.", "OK"))
                .ConfigureAwait(false);
            return null;
        }
        catch (PermissionException)
        {
            await ShowPermissionDeniedAsync().ConfigureAwait(false);
            return null;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    private static async Task<Stream?> OpenPickPhotoStreamCrossPlatformAsync(string pickerTitle)
    {
#if WINDOWS
        try
        {
            var photo =
                await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = pickerTitle })
                    .ConfigureAwait(false);
            if (photo is not null)
            {
                return await photo.OpenReadAsync().ConfigureAwait(false);
            }
        }
        catch (FeatureNotSupportedException)
        {
            // Fallback ниже.
        }

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = pickerTitle,
            FileTypes = FilePickerFileType.Images,
        }).ConfigureAwait(false);

        return result is null ? null : await result.OpenReadAsync().ConfigureAwait(false);
#else
        var picked =
            await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = pickerTitle })
                .ConfigureAwait(false);
        return picked is null ? null : await picked.OpenReadAsync().ConfigureAwait(false);
#endif
    }

    private static async Task<bool> EnsurePermissionAsync<TPermission>()
        where TPermission : Permissions.BasePermission, new()
    {
        var status = await Permissions.CheckStatusAsync<TPermission>().ConfigureAwait(false);
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<TPermission>().ConfigureAwait(false);
        return status == PermissionStatus.Granted;
    }

    public static Task ShowPermissionDeniedAsync() =>
        MainThread.InvokeOnMainThreadAsync(async () =>
            await ConfirmPopup.ShowAsync("Permissions", "Photo or camera access was denied.", "OK"));
}

#endregion

#region Локальный pending-аватар (Preferences)

/// <summary>
/// Локальный pending-аватар: путь в Preferences + файл в кэше, без Firebase Storage.
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

#endregion

#region Превью аватара (pending → URL → дефолт)

/// <summary>Приоритет: локальный pending (аватар) → URL из профиля → дефолт.</summary>
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

#endregion
