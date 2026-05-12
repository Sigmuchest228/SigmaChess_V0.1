namespace SigmaChess.Services;

public interface IPhotoSourcePicker
{
    Task<PickPhotoSource> PickSourceAsync(CancellationToken cancellationToken = default);
}
