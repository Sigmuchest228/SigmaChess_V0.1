using SigmaChess.Views;

namespace SigmaChess;

/// <summary>
/// Корневой класс MAUI-приложения. Решает только две вещи:
///   1. Регистрирует non-tab маршруты (страницы, которые открываются «push»-ом, а не из табов).
///   2. Управляет тем, какой Shell сейчас активен — гостевой или авторизованный.
/// <para>
/// Сценарий смены Shell: при логине AuthViewModel вызывает <see cref="SetAuthenticatedShell"/>;
/// при логауте AppShellAuth вызывает <see cref="SetUnauthenticatedShell"/>. MAUI рассматривает
/// замену <see cref="Application.MainPage"/> как полную смену корня — и таб-бар, и состояние
/// навигации полностью пересоздаются.
/// </para>
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        RegisterShellRoutes();
        // Стартуем как гость: даже если пользователь авторизован, локально его подхватит
        // FileUserRepository, а первый успешный навигационный сценарий переключит Shell.
        MainPage = new AppShellNotAuth();
    }

    // AuthPage не лежит в табах Shell — её регистрируем явно, чтобы можно было идти
    // через Shell.Current.GoToAsync(nameof(AuthPage)).
    private static void RegisterShellRoutes()
    {
        Routing.RegisterRoute(nameof(AuthPage), typeof(AuthPage));
    }

    public void SetAuthenticatedShell()
    {
        MainPage = new AppShellAuth();
    }

    public void SetUnauthenticatedShell()
    {
        MainPage = new AppShellNotAuth();
        // После смены Shell нужно явно перейти на главную, иначе пользователь увидит
        // первый таб гостевого Shell (что обычно тоже MainPage, но лучше явно).
        // Делаем это на UI-потоке: Shell.GoToAsync требует UI-контекста.
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
        });
    }
}
