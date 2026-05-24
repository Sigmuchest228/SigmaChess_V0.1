using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Microsoft.Maui.Controls;
using SigmaChess.ViewModels;

namespace SigmaChess.Services;

public class AppService
{
    private static AppService? _instance;
    private static readonly object InstanceLock = new();

    private readonly object _initLock = new();
    private FirebaseAuthClient? _auth;
    private FirebaseClient? _client;

    private FirebaseSyncRepository? _firebaseSync;
    private BoardLayoutService? _boardLayout;
    private global::SigmaChess.Engine.GameController? _gameController;
    private GameViewModel? _gameViewModel;
    private BottomNavigationCoordinator? _bottomNavigation;
    private IPhotoSourcePicker? _photoPicker;

    private AppService()
    {
    }

    public static AppService GetInstance()
    {
        if (_instance is not null)
        {
            return _instance;
        }

        lock (InstanceLock)
        {
            return _instance ??= new AppService();
        }
    }

    public void Init()
    {
        EnsureInitialized();
    }

    public FirebaseSyncRepository FirebaseSync => _firebaseSync ??= new FirebaseSyncRepository(this);

    public BoardLayoutService BoardLayout => _boardLayout ??= new BoardLayoutService();

    public global::SigmaChess.Engine.GameController GameController => _gameController ??= new global::SigmaChess.Engine.GameController();

    public GameViewModel GameViewModel =>
        _gameViewModel ??= new GameViewModel(GameController, BoardLayout, this, FirebaseSync);

    public BottomNavigationCoordinator BottomNavigation => _bottomNavigation ??= new BottomNavigationCoordinator(GameViewModel);

    public IPhotoSourcePicker PhotoPicker => _photoPicker ??= new PhotoSourcePicker();

    private FirebaseAuthClient Auth
    {
        get
        {
            EnsureInitialized();
            return _auth!;
        }
    }

    public FirebaseClient RealtimeDatabase
    {
        get
        {
            EnsureInitialized();
            return _client!;
        }
    }

    public string? CurrentUserId => Auth.User?.Uid;

    public bool IsAnonymousUser => Auth.User?.IsAnonymous ?? false;

    private void EnsureInitialized()
    {
        if (_auth is not null)
        {
            return;
        }

        lock (_initLock)
        {
            if (_auth is not null)
            {
                return;
            }

            var config = new FirebaseAuthConfig
            {

                ApiKey = "AIzaSyCl1Ix-ZEcM4JLBjFew5XsS1LTQIpg8j7U",
                AuthDomain = "sigmachess-75f04.firebaseapp.com",
                Providers = [new EmailProvider()],

            };

            var auth = new FirebaseAuthClient(config);
            _client = new FirebaseClient(
                "https://sigmachess-75f04-default-rtdb.europe-west1.firebasedatabase.app",
                new FirebaseOptions
                {
                    AuthTokenAsyncFactory = () => GetIdTokenFromAuthClientAsync(auth, false)
                });
            _auth = auth;
        }
    }

    public Task<string?> GetIdTokenAsync(bool forceRefresh = false)
    {
        EnsureInitialized();
        if (_auth?.User is null)
        {
            return Task.FromResult<string?>(null);
        }

        return GetIdTokenFromAuthClientAsync(_auth, forceRefresh);
    }

    private static async Task<string?> GetIdTokenFromAuthClientAsync(FirebaseAuthClient auth, bool forceRefresh)
    {
        if (auth.User is null)
        {
            return null;
        }

        return await auth.User.GetIdTokenAsync(forceRefresh).ConfigureAwait(false);
    }

    public async Task<bool> TryRegister(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        try
        {
            await Auth.CreateUserWithEmailAndPasswordAsync(email, password).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryLogin(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        try
        {
            await Auth.SignInWithEmailAndPasswordAsync(email, password).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TrySignInAnonymouslyAsync()
    {
        try
        {
            EnsureInitialized();
            await Auth.SignInAnonymouslyAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Logout()
    {
        try
        {
            Auth.SignOut();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task PerformFullLogoutAsync(CancellationToken cancellationToken = default)
    {
        await MainThread.InvokeOnMainThreadAsync(() => { GameViewModel.ResetSessionForLogout(); }).WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        Logout();

        await MainThread.InvokeOnMainThreadAsync(static () =>
        {
            if (Application.Current is App appShell)
            {
                appShell.SetUnauthenticatedShell();
            }
        }).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
