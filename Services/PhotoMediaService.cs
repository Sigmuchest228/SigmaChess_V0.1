using SigmaChess.Views;

namespace SigmaChess.Services;

/// <summary>Разрешения и системный выбор фото из галереи или камеры (общее для обоев и профиля).</summary>
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
