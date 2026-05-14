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

    /// <summary>
    /// Сразу задаём контекст навбара — иначе первый проход биндингов наследует страницу
    /// (<see cref="SigmaChess.ViewModels.RespectsPageViewModel"/> и т. п.) и даёт предупреждения / поломанные команды.
    /// Повтор при Loaded — если первый проход был до готовности <see cref="SigmaChess.Services.AppService"/>.
    /// </summary>
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
