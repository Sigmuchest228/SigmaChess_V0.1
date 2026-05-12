using Microsoft.Extensions.DependencyInjection;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess;

/// <summary>
/// Корень приложения: гостевой или авторизованный <see cref="Shell"/> (без flyout).
/// Первое окно создаётся после <see cref="MauiProgram.CreateMauiApp"/> — так безопасно на Android.
/// </summary>
public partial class App : Application
{
    /// <summary>Выставляется в <see cref="MauiProgram.CreateMauiApp"/> до показа окна — чтобы проверить сохранённую сессию Firebase.</summary>
    internal static IServiceProvider? Services { get; set; }

    /// <summary>Перед <see cref="SetAuthenticatedShell"/> — следующий Loader на авторизованном Shell без задержки.</summary>
    internal static bool SkipLoaderDelayOnceForAuthShell { get; set; }

    public App()
    {
        InitializeComponent();
        RegisterShellRoutes();
    }

    private static void RegisterShellRoutes()
    {
        Routing.RegisterRoute(nameof(PlayedGamesPage), typeof(PlayedGamesPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(ProfileWallpaperSettingsPage), typeof(ProfileWallpaperSettingsPage));
        Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
        Routing.RegisterRoute(nameof(GameReplayPage), typeof(GameReplayPage));
        Routing.RegisterRoute(nameof(PuzzleSolvePage), typeof(PuzzleSolvePage));
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page root;
        var appService = Services?.GetService<AppService>();
        if (appService is not null
            && !string.IsNullOrEmpty(appService.CurrentUserId)
            && !appService.IsAnonymousUser)
        {
            root = new AppShellAuth();
        }
        else
        {
            root = new AppShellNotAuth();
        }

        return new Window(root);
    }

    public void SetAuthenticatedShell()
    {
        SkipLoaderDelayOnceForAuthShell = true;
        if (Windows.Count > 0)
        {
            Windows[0].Page = new AppShellAuth();
        }
        else
        {
            MainPage = new AppShellAuth();
        }
    }

    public void SetUnauthenticatedShell()
    {
        // Не вызывать GoToAsync сразу после смены корня — на Android ломается FragmentManager.
        // У AppShellNotAuth первый Tab — LoaderPage, затем переход на Auth.
        if (Windows.Count > 0)
        {
            Windows[0].Page = new AppShellNotAuth();
        }
        else
        {
            MainPage = new AppShellNotAuth();
        }
    }
}
