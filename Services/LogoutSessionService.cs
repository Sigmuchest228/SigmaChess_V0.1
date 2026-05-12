using SigmaChess.ViewModels;

namespace SigmaChess.Services;

/// <summary>
/// Один сценарий выхода: локальное хранилище галереи, сброс фона для гостя, сетка пресетов, партия в VM, SignOut, смена Shell.
/// Поля обоев в RTDB не трогаем — при следующем входе <see cref="IUserAppearanceService.ApplyFromProfileAsync"/> подтянет сохранённые пресет/URL.
/// </summary>
public sealed class LogoutSessionService
{
    private readonly AppService _app;
    private readonly IUserAppearanceService _appearance;
    private readonly GameViewModel _game;
    private readonly ProfileWallpaperSettingsViewModel _wallpaperSettings;

    public LogoutSessionService(
        AppService app,
        IUserAppearanceService appearance,
        GameViewModel game,
        ProfileWallpaperSettingsViewModel wallpaperSettings)
    {
        _app = app;
        _appearance = appearance;
        _game = game;
        _wallpaperSettings = wallpaperSettings;
    }

    public async Task PerformFullLogoutAsync(CancellationToken cancellationToken = default)
    {
        WallpaperUserGalleryStore.ClearAll();
        UserAvatarLocalStore.ClearPendingLocalAvatarPath();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _appearance.ResetBackdropToDefault();
            _game.ResetSessionForLogout();
        }).WaitAsync(cancellationToken).ConfigureAwait(false);

        await _wallpaperSettings.ReloadWallpaperGridAfterLogoutAsync(cancellationToken).ConfigureAwait(false);

        _app.Logout();

        await MainThread.InvokeOnMainThreadAsync(static () =>
        {
            if (Application.Current is App appShell)
            {
                appShell.SetUnauthenticatedShell();
            }
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
