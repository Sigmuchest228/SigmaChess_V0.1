namespace SigmaChess.Services;

/// <summary>Показывает попап выбора Gallery/Camera при добавлении фото (обои, аватар).</summary>
public sealed class PhotoSourcePicker : IPhotoSourcePicker
{
    public Task<PickPhotoSource> PickSourceAsync(CancellationToken cancellationToken = default) =>
        Views.PickPhotoSourcePopup.ShowAsync().WaitAsync(cancellationToken);
}
