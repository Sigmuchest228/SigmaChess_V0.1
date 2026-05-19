using SigmaChess.Services;

namespace SigmaChess;

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
