using System.Windows.Input;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

// ViewModel главного экрана (страница MainPage). Отсюда игрок открывает игру, список
// «уважения», профиль, сыгранные партии, а гость — вход/регистрацию. Знает, вошёл ли
// пользователь (IsGuest), показывает аватар или кнопки гостя и аккуратно обрабатывает
// навигацию (включая запрет разделов для гостя). Использует AppService (пользователь)
// и FirebaseSyncRepository (профиль, respect).
public class MainPageViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;

    // Маршруты, которые являются «корнями» Shell (к ним переходят с префиксом //).
    private static readonly HashSet<string> ShellRootRoutes =
    [
        nameof(AuthPage),
        nameof(MainPage),
        nameof(GamePage),
        nameof(RespectsPage),
    ];

    // Разделы, недоступные гостю — при попытке открыть предложим войти.
    private static readonly HashSet<string> GuestRestrictedRoutes =
    [
        nameof(RespectsPage),
        nameof(SettingsPage),
        nameof(PlayedGamesPage),
    ];

    private const string AuthLoginRoute = nameof(AuthPage);
    private const string AuthSignupRoute = nameof(AuthPage) + "?mode=register";

    private const string RespectListRoute = nameof(RespectsPage);

    private bool _isGuest = true;

    // Конструктор без параметров (для интерфейса): берёт сервисы из общего AppService.
    public MainPageViewModel()
        : this(AppService.GetInstance(), AppService.GetInstance().FirebaseSync)
    {
    }

    // Основной конструктор: сохраняет сервисы, создаёт команды открытия разделов
    // (вход, регистрация, профиль, игра на одном устройстве, respect, сыгранные партии)
    // и сразу определяет, гость это или вошедший пользователь.
    public MainPageViewModel(AppService appService, FirebaseSyncRepository firebaseSync)
    {
        _appService = appService;
        _firebaseSync = firebaseSync;

        OpenLoginCommand = new Command(async () => await NavigateAsync(AuthLoginRoute));
        OpenSignupCommand = new Command(async () => await NavigateAsync(AuthSignupRoute));
        OpenProfileCommand = new Command(async () => await NavigateAsync(nameof(UserProfilePage)));

        OpenOneDeviceGameCommand = new Command(async () => await NavigateAsync("//GamePage"));
        OpenRespectListCommand = new Command(async () => await NavigateAsync(RespectListRoute));
        OpenPlayedGamesCommand = new Command(async () => await NavigateAsync(nameof(PlayedGamesPage)));

        RefreshAuthState();
    }

    // Показывать ли ссылку на сыгранные партии (только для вошедших).
    public bool ShowPlayedGamesLink => !IsGuest;

    // Гость ли текущий пользователь (не выполнен вход). При смене обновляет все
    // зависимые от этого элементы интерфейса (аватар, кнопки гостя, ссылки).
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
            OnPropertyChanged(nameof(ShowProfileAvatar));
            OnPropertyChanged(nameof(ShowGuestAuthButtons));
            OnPropertyChanged(nameof(ShowPlayedGamesLink));
        }
    }

    // Показывать аватар (вошедшему) или кнопки входа/регистрации (гостю).
    public bool ShowProfileAvatar => !IsGuest;

    public bool ShowGuestAuthButtons => IsGuest;

    // Картинка аватара профиля для главного экрана.
    public ImageSource? ProfileAvatarSource
    {
        get => _profileAvatarSource;
        private set
        {
            if (ReferenceEquals(_profileAvatarSource, value))
            {
                return;
            }

            _profileAvatarSource = value;
            OnPropertyChanged();
        }
    }

    private ImageSource? _profileAvatarSource = ImageSource.FromFile("defaultsigma.jpg");

    private string _respectsSummaryText = string.Empty;

    // Заголовок игрового раздела.
    public string GameSectionTitle => "SIGMA CHESS";

    // Краткий текст-сводка про respect-лист (сколько игроков / приглашение войти).
    public string RespectsSummaryText
    {
        get => _respectsSummaryText;
        private set
        {
            if (_respectsSummaryText == value)
            {
                return;
            }

            _respectsSummaryText = value;
            OnPropertyChanged();
        }
    }

    // Команда открытия профиля пользователя.
    public ICommand OpenProfileCommand { get; }

    // Обновляет текст-сводку respect-листа: для гостя — приглашение войти, иначе тянет
    // количество из Firebase и формирует фразу. Обновление текста — в главном потоке.
    public async Task RefreshRespectsSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (IsGuest)
        {
            await MainThread.InvokeOnMainThreadAsync(() => RespectsSummaryText = "Sign in to manage your respect list.")
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var uids = await _firebaseSync.GetRespectUidsAsync(cancellationToken).ConfigureAwait(false);
            var n = uids.Count;
            var text = n == 0
                ? "Your respect list is empty."
                : $"{n} player{(n == 1 ? string.Empty : "s")} in your respect list.";
            await MainThread.InvokeOnMainThreadAsync(() => RespectsSummaryText = text).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() => RespectsSummaryText = "Respects").WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // Определяет, гость ли сейчас, по типу текущего Shell (гостевой или авторизованный).
    public void RefreshAuthState()
    {
        IsGuest = Shell.Current is AppShellNotAuth;
    }

    // Если пользователь вошёл — создаёт/обновляет его профиль в Firebase и обновляет
    // аватар. Ошибки синхронизации молча игнорируются.
    public async Task SyncFirebaseProfileIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appService.CurrentUserId))
        {
            return;
        }

        try
        {
            await _firebaseSync.EnsureUserAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {

        }

        await RefreshAvatarSourceAsync(cancellationToken).ConfigureAwait(false);
    }

    // Загружает аватар текущего пользователя и выставляет его в свойство (в главном
    // потоке).
    private async Task RefreshAvatarSourceAsync(CancellationToken cancellationToken)
    {
        var src =
            await UserAvatarPreview.LoadAsync(_appService.CurrentUserId, cancellationToken)
                .ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => ProfileAvatarSource = src).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // Общая навигация по разделам. Особые случаи: переход на главный с префиксом //;
    // для гостя на закрытый раздел — попап с предложением войти; иначе нормализует
    // маршрут и переходит, показывая попап при ошибке.
    private async Task NavigateAsync(string route)
    {
        if (Shell.Current is null)
        {
            return;
        }

        var baseRoute = GetRouteBase(route);

        if (route.StartsWith("//", StringComparison.Ordinal) && baseRoute == nameof(MainPage))
        {
            await Shell.Current.GoToAsync("//MainPage");
            return;
        }

        if (Shell.Current is AppShellNotAuth && GuestRestrictedRoutes.Contains(baseRoute))
        {
            var goLogin = await ConfirmPopup.ShowAsync(
                "Account required",
                "Sign in or sign up to open this section.",
                "Log in",
                "Cancel");
            if (goLogin)
            {
                await Shell.Current.GoToAsync("//AuthPage");
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
            System.Diagnostics.Debug.WriteLine($"Navigation failed for route '{path}': {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await ConfirmPopup.ShowAsync("Navigation error", $"Could not open: {path}", "OK");
            });
        }
    }

    // Достаёт «базовое» имя маршрута без префикса // и без параметров после ? (например
    // из «AuthPage?mode=register» получится «AuthPage»).
    private static string GetRouteBase(string route)
    {
        var s = route.Trim();
        if (s.StartsWith("//", StringComparison.Ordinal))
        {
            s = s[2..];
        }

        return s.Split('?', 2)[0].TrimStart('/');
    }

    // Приводит маршрут к виду для Shell: корневым маршрутам добавляет префикс //,
    // остальные оставляет как есть (переход в стек поверх).
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

    // Команды кнопок главного экрана: вход, регистрация, игра на одном устройстве,
    // список respect, сыгранные партии.
    public ICommand OpenLoginCommand { get; }

    public ICommand OpenSignupCommand { get; }

    public ICommand OpenOneDeviceGameCommand { get; }

    public ICommand OpenRespectListCommand { get; }

    public ICommand OpenPlayedGamesCommand { get; }
}
