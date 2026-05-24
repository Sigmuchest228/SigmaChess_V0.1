using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using SigmaChess.Views;

namespace SigmaChess.Services;

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

public class PhotoSourcePicker : IPhotoSourcePicker
{
    public Task<PickPhotoSource> PickSourceAsync(CancellationToken cancellationToken = default) =>
        PickPhotoSourcePopup.ShowAsync().WaitAsync(cancellationToken);
}

#endregion

#region Разрешения и открытие потока фото

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

#region Локальный аватар на устройстве (Preferences + AppData)

public static class UserAvatarLocalStore
{
    private static string KeyForUser(string userId) => $"AvatarLocalPath_{userId}";

    public static void SetLocalAvatarPath(string userId, string absolutePath) =>
        Preferences.Set(KeyForUser(userId), absolutePath);

    public static string? GetLocalAvatarPath(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var path = Preferences.Get(KeyForUser(userId), string.Empty)?.Trim();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public static void ClearLocalAvatarPath(string userId) =>
        Preferences.Remove(KeyForUser(userId));

    public static async Task<string> SaveLocalAvatarAsync(string userId, Stream photoStream,
        CancellationToken cancellationToken = default)
    {
        var avatarsDir = Path.Combine(FileSystem.AppDataDirectory, "avatars");
        Directory.CreateDirectory(avatarsDir);

        var fileName = $"avatar_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jpg";
        var savePath = Path.Combine(avatarsDir, fileName);

        await using (var fs = File.Create(savePath))
        {
            await photoStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(savePath);
        }
        catch
        {
            fullPath = savePath;
        }

        DeleteStoredFileIfExists(GetLocalAvatarPath(userId));
        SetLocalAvatarPath(userId, fullPath);
        return fullPath;
    }

    private static void DeleteStoredFileIfExists(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return;
        }

        try
        {
            var fp = Path.GetFullPath(absolutePath.Trim());
            if (File.Exists(fp))
            {
                File.Delete(fp);
            }
        }
        catch
        {

        }
    }
}

#endregion

#region Превью аватара (локальный файл → дефолт)

public static class UserAvatarPreview
{
    public static Task<ImageSource> LoadAsync(string? userId,
        CancellationToken cancellationToken, bool preferLocalStore = true)
    {
        if (preferLocalStore && !string.IsNullOrWhiteSpace(userId))
        {
            var localPath = UserAvatarLocalStore.GetLocalAvatarPath(userId);
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                if (TryNormalizeExistingFilePath(localPath, out var normalized))
                {
                    return Task.FromResult(ImageSource.FromFile(normalized));
                }

                UserAvatarLocalStore.ClearLocalAvatarPath(userId);
            }
        }

        return Task.FromResult(ImageSource.FromFile("defaultsigma.jpg"));
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
