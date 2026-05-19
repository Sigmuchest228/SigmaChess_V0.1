using SigmaChess.Services;

namespace SigmaChess.Views;

public partial class MainBottomNavBar : ContentView
{
    public MainBottomNavBar()
    {
        InitializeComponent();
        AttachCoordinator();
        Loaded += OnLoadedOnce;
    }

    private void OnLoadedOnce(object? sender, EventArgs e)
    {
        Loaded -= OnLoadedOnce;
        AttachCoordinator();
    }

    private void AttachCoordinator()
    {
        if (BindingContext is BottomNavigationCoordinator)
        {
            return;
        }

        var c = AppService.GetInstance().BottomNavigation;
        BindingContext = c;
        c.SyncFromShell();
    }
}
