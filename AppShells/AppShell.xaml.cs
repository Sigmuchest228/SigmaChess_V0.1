using SigmaChess.Views;

namespace SigmaChess;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AuthPage), typeof(AuthPage));
        Routing.RegisterRoute(nameof(PuzzlesPage), typeof(PuzzlesPage));
        Routing.RegisterRoute(nameof(FriendsPage), typeof(FriendsPage));
        Routing.RegisterRoute(nameof(WatchPage), typeof(WatchPage));
        Routing.RegisterRoute(nameof(LearnPage), typeof(LearnPage));
        Routing.RegisterRoute(nameof(MenuPage), typeof(MenuPage));
    }
}
