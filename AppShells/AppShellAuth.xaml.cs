using SigmaChess.Services;

namespace SigmaChess;

/// <summary>Shell после входа: таб-бар без flyout и без навбара Shell.</summary>
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
