using SigmaChess.Views;

namespace SigmaChess;

public partial class App : Application
{

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

        if (Windows.Count > 0)
        {
            Windows[0].Page = new AppShellNotAuth();
        }
    }
}
