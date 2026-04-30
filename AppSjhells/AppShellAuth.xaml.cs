using Microsoft.Extensions.DependencyInjection;
using SigmaChess.Services;

namespace SigmaChess;

/// <summary>
/// Shell для авторизованного пользователя. Содержит табы и пункт меню «Logout».
/// Сами табы и иконки описаны в XAML; здесь только обработчик логаута.
/// </summary>
public partial class AppShellAuth : Shell
{
    public AppShellAuth()
    {
        InitializeComponent();
    }

    // Достаём AppService из DI-контейнера через MauiContext, потому что Shell не получает
    // зависимости в конструкторе (он создаётся вручную в App.xaml.cs).
    private void MenuItem_Logout_Clicked(object sender, EventArgs e)
    {
        if (Application.Current?.Handler?.MauiContext?.Services is IServiceProvider sp)
        {
            sp.GetRequiredService<AppService>().Logout();
        }

        if (Application.Current is App app)
        {
            // Меняем Shell на гостевой и возвращаемся на главную (см. App.SetUnauthenticatedShell).
            app.SetUnauthenticatedShell();
        }
    }
}
