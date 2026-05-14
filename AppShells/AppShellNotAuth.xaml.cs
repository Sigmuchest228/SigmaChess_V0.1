using SigmaChess.Services;

namespace SigmaChess;

/// <summary>Гостевой Shell: только маршруты Main/Game, таб-бар скрыт.</summary>
public partial class AppShellNotAuth : Shell
{
    public AppShellNotAuth()
    {
        InitializeComponent();
        Navigated += OnShellNavigated;
    }

    private static void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        AppService.GetInstance().BottomNavigation.SyncFromShell();
    }
}
