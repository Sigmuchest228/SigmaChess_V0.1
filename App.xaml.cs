using SigmaChess.Views;

namespace SigmaChess;

/// <summary>
/// Корень приложения: гостевой или авторизованный <see cref="Shell"/> (без flyout).
/// Первое окно создаётся после <see cref="MauiProgram.CreateMauiApp"/> — так безопасно на Android.
/// </summary>
public partial class App : Application
{
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
        Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
        Routing.RegisterRoute(nameof(GameReplayPage), typeof(GameReplayPage));
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Токен Firebase не пишется на диск (InMemoryRepository) — при новом запуске процесса всегда гостевой Shell.
        return new Window(new AppShellNotAuth());
    }

    public void SetAuthenticatedShell()
    {
        SkipLoaderDelayOnceForAuthShell = true;
        if (Windows.Count > 0)
        {
            Windows[0].Page = new AppShellAuth();
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
    }
}
