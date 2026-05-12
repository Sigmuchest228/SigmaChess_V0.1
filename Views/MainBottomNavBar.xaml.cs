using Microsoft.Extensions.DependencyInjection;
using SigmaChess;

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

    /// <summary>
    /// Сразу задаём контекст навбара — иначе первый проход биндингов наследует страницу
    /// (<see cref="ViewModels.FollowsPageViewModel"/> и т. п.) и даёт предупреждения / поломанные команды.
    /// Повтор при Loaded — если в ctor ещё не был готов <see cref="App.Services"/>.
    /// </summary>
    private void AttachCoordinator()
    {
        if (BindingContext is Services.BottomNavigationCoordinator)
        {
            return;
        }

        if (App.Services?.GetService<Services.BottomNavigationCoordinator>() is not { } c)
        {
            return;
        }

        BindingContext = c;
        c.SyncFromShell();
    }
}
