using System.Windows.Input;
using SigmaChess;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

/// <summary>
/// ViewModel главной страницы приложения. Отвечает за:
///   1. Команды-кнопки навигации (открыть логин/регистрацию/игру/боты/паззлы и т. д.).
///   2. Гость-гейт: для разделов, недоступных без логина, показывает попап «нужен аккаунт».
///   3. Управление видимостью кнопок «Log in / Sign up» через флаг <see cref="IsGuest"/>.
/// </summary>
public class MainPageViewModel : ViewModelBase
{
    // Маршруты, зарегистрированные как корни Shell. Их нужно открывать с префиксом "//"
    // (Shell интерпретирует "//" как абсолютный путь к корню, а не относительный).
    private static readonly HashSet<string> ShellRootRoutes =
    [
        nameof(MainPage),
        nameof(GamePage),
        nameof(BotsGamePage),
        nameof(PuzzlesPage),
        nameof(FriendsPage),
        nameof(WatchPage),
        nameof(LearnPage),
        nameof(MenuPage),
    ];

    // Список разделов, которые гостю не показываем — сначала просим залогиниться.
    private static readonly HashSet<string> GuestRestrictedRoutes =
    [
        nameof(BotsGamePage),
        nameof(PuzzlesPage),
        nameof(FriendsPage),
        nameof(WatchPage),
        nameof(LearnPage),
        nameof(MenuPage),
    ];

    private const string AuthLoginRoute = nameof(AuthPage);
    private const string AuthSignupRoute = nameof(AuthPage) + "?mode=register";

    private bool _isGuest = true;
    private const string BotsGameRoute = nameof(BotsGamePage);
    private const string PuzzlesRoute = nameof(PuzzlesPage);
    private const string FriendsRoute = nameof(FriendsPage);
    private const string WatchRoute = nameof(WatchPage);
    private const string LearnRoute = nameof(LearnPage);
    private const string MenuRoute = nameof(MenuPage);

    public MainPageViewModel()
    {
        OpenLoginCommand = new Command(async () => await NavigateAsync(AuthLoginRoute));
        OpenSignupCommand = new Command(async () => await NavigateAsync(AuthSignupRoute));

        OpenOneDeviceGameCommand = new Command(async () => await NavigateAsync("//GamePage"));
        OpenBotsCommand = new Command(async () => await NavigateAsync(BotsGameRoute));
        OpenPuzzlesCommand = new Command(async () => await NavigateAsync(PuzzlesRoute));
        OpenFriendsCommand = new Command(async () => await NavigateAsync(FriendsRoute));
        OpenWatchCommand = new Command(async () => await NavigateAsync(WatchRoute));

        OpenHomeCommand = new Command(async () => await NavigateAsync("//MainPage"));
        OpenLearnCommand = new Command(async () => await NavigateAsync(LearnRoute));
        OpenMenuCommand = new Command(async () => await NavigateAsync(MenuRoute));

        // Без этого вызова при старте у гостя кнопки «Log in»/«Sign up» могли бы быть
        // спрятаны (по дефолту IsGuest = true и так, но мы держим единую точку синхронизации).
        RefreshAuthState();
    }

    /// <summary>
    /// true — пользователь не вошёл в аккаунт. XAML по этому флагу показывает кнопки
    /// «Log in» и «Sign up» и прячет их, когда пользователь авторизован.
    /// </summary>
    public bool IsGuest
    {
        get => _isGuest;
        private set
        {
            if (_isGuest == value)
            {
                return;
            }

            _isGuest = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Пересчитывает <see cref="IsGuest"/> по типу текущего <see cref="Shell.Current"/>.
    /// Дёргается из <c>MainPage.OnAppearing</c>, потому что после логина мы заменяем Shell,
    /// и страница могла «прилететь» в новый Shell — флаг надо обновить.
    /// </summary>
    public void RefreshAuthState()
    {
        IsGuest = Shell.Current is AppShellNotAuth;
    }

    // Универсальный навигатор: проверяет гость-гейт, нормализует маршрут и идёт в Shell.
    private static async Task NavigateAsync(string route)
    {
        if (Shell.Current is null)
        {
            return;
        }

        // Гость-гейт: если экран в списке закрытых — показываем попап и при «Log in»
        // ведём на страницу логина (без префикса "//", потому что AuthPage не корень Shell).
        if (Shell.Current is AppShellNotAuth &&
            GuestRestrictedRoutes.Contains(GetRouteBase(route)))
        {
            var goLogin = await ConfirmPopup.ShowAsync(
                "Account required",
                "Sign in or sign up to open this section.",
                "Log in",
                "Cancel");
            if (goLogin)
            {
                await Shell.Current.GoToAsync(nameof(AuthPage));
            }

            return;
        }

        var path = NormalizeShellRoute(route);

        try
        {
            await Shell.Current.GoToAsync(path);
        }
        catch (Exception ex)
        {
            // Иногда Shell кидает исключение, если маршрут не найден или прерывание навигации.
            // Логируем + показываем попап на UI-потоке (диалог нельзя поднять из background).
            System.Diagnostics.Debug.WriteLine($"Navigation failed for route '{path}': {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await ConfirmPopup.ShowAsync("Navigation error", $"Could not open: {path}", "OK");
            });
        }
    }

    // Достаёт «голое» имя маршрута без "//" и без query-строки. Нужно, чтобы корректно
    // сравнить с GuestRestrictedRoutes (там лежат именно базовые имена).
    private static string GetRouteBase(string route)
    {
        var s = route.Trim();
        if (s.StartsWith("//", StringComparison.Ordinal))
        {
            s = s[2..];
        }

        return s.Split('?', 2)[0].TrimStart('/');
    }

    // Превращает короткое имя маршрута в полный Shell-путь: для корней — добавляем "//".
    // Без префикса Shell попытался бы открыть страницу как push, и навигация бы поломалась
    // (мы используем Shell с табами, где экраны только корневые).
    private static string NormalizeShellRoute(string route)
    {
        if (route.StartsWith("//", StringComparison.Ordinal))
        {
            return route;
        }

        var baseRoute = route.Split('?', 2)[0];
        if (ShellRootRoutes.Contains(baseRoute))
        {
            return "//" + route;
        }

        return route;
    }

    public ICommand OpenLoginCommand { get; }

    public ICommand OpenSignupCommand { get; }

    public ICommand OpenOneDeviceGameCommand { get; }

    public ICommand OpenBotsCommand { get; }

    public ICommand OpenPuzzlesCommand { get; }

    public ICommand OpenFriendsCommand { get; }

    public ICommand OpenWatchCommand { get; }

    public ICommand OpenHomeCommand { get; }

    public ICommand OpenLearnCommand { get; }

    public ICommand OpenMenuCommand { get; }
}
