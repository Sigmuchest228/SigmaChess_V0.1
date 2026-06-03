using SigmaChess.Views;

namespace SigmaChess;

// Главный класс приложения. Управляет, какой Shell (набор экранов) сейчас активен:
// гостевой (AppShellNotAuth) до входа или авторизованный (AppShellAuth) после входа.
// Также регистрирует маршруты для страниц, которые открываются «поверх» стеком.
public partial class App : Application
{

    // Разовый флаг: пропустить задержку загрузчика один раз при переходе на
    // авторизованный Shell (чтобы после входа сразу показать главный экран).
    internal static bool SkipLoaderDelayOnceForAuthShell { get; set; }

    // Конструктор: инициализирует XAML приложения и регистрирует маршруты Shell.
    public App()
    {
        InitializeComponent();
        RegisterShellRoutes();
    }

    // Регистрирует маршруты страниц, на которые переходят по имени (открываются в стеке
    // поверх текущего экрана): сыгранные партии, настройки, профиль, реплей.
    private static void RegisterShellRoutes()
    {
        Routing.RegisterRoute(nameof(PlayedGamesPage), typeof(PlayedGamesPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(UserProfilePage), typeof(UserProfilePage));
        Routing.RegisterRoute(nameof(GameReplayPage), typeof(GameReplayPage));
    }

    // Создаёт главное окно приложения при старте: открывает гостевой Shell (до входа).
    protected override Window CreateWindow(IActivationState? activationState)
    {

        return new Window(new AppShellNotAuth());
    }

    // Переключает приложение на авторизованный Shell (после успешного входа). Ставит
    // флаг пропуска задержки загрузчика и подменяет страницу окна.
    public void SetAuthenticatedShell()
    {
        SkipLoaderDelayOnceForAuthShell = true;
        if (Windows.Count > 0)
        {
            Windows[0].Page = new AppShellAuth();
        }
    }

    // Переключает приложение обратно на гостевой Shell (после выхода из аккаунта).
    public void SetUnauthenticatedShell()
    {

        if (Windows.Count > 0)
        {
            Windows[0].Page = new AppShellNotAuth();
        }
    }
}
