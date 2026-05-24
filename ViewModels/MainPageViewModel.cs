using System.Windows.Input;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

public class MainPageViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;

    private static readonly HashSet<string> ShellRootRoutes =
    [
        nameof(AuthPage),
        nameof(MainPage),
        nameof(GamePage),
        nameof(RespectsPage),
    ];

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

    public MainPageViewModel()
        : this(AppService.GetInstance(), AppService.GetInstance().FirebaseSync)
    {
    }

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

    public bool ShowPlayedGamesLink => !IsGuest;

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

    public bool ShowProfileAvatar => !IsGuest;

    public bool ShowGuestAuthButtons => IsGuest;

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

    public string GameSectionTitle => "SIGMA CHESS";

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

    public ICommand OpenProfileCommand { get; }

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

    public void RefreshAuthState()
    {
        IsGuest = Shell.Current is AppShellNotAuth;
    }

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

    private async Task RefreshAvatarSourceAsync(CancellationToken cancellationToken)
    {
        var src =
            await UserAvatarPreview.LoadAsync(_appService.CurrentUserId, cancellationToken)
                .ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() => ProfileAvatarSource = src).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

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

    private static string GetRouteBase(string route)
    {
        var s = route.Trim();
        if (s.StartsWith("//", StringComparison.Ordinal))
        {
            s = s[2..];
        }

        return s.Split('?', 2)[0].TrimStart('/');
    }

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

    public ICommand OpenRespectListCommand { get; }

    public ICommand OpenPlayedGamesCommand { get; }
}
