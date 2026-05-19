using SigmaChess.Services;

namespace SigmaChess;

public partial class AppShellAuth : Shell
{
    public AppShellAuth()
    {
        InitializeComponent();
        Navigated += OnShellNavigated;
    }

    private static void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        AppService.GetInstance().BottomNavigation.SyncFromShell();
    }
}
