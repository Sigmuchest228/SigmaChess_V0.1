using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Auth.Repository;
using Firebase.Database;

namespace SigmaChess.Services;

/// <summary>
/// Тонкая обёртка над Firebase: аутентификация (email/пароль + анонимная) и доступ к Realtime Database.
/// <para>
/// Конфиг (Api Key, URL RTDB) пока зашит в коде для прототипа — план переноса в User Secrets / env
/// описан в <c>FIREBASE.md</c>.
/// </para>
/// <para>
/// Ленивая инициализация под локом: создание клиентов в конструкторе даёт ANR на Android при старте.
/// </para>
/// </summary>
public class AppService
{
    private readonly object _initLock = new();
    private FirebaseAuthClient? _auth;
    private FirebaseClient? _client;

    public AppService()
    {
    }

    private FirebaseAuthClient Auth
    {
        get
        {
            EnsureInitialized();
            return _auth!;
        }
    }

    /// <summary>Клиент Realtime Database; не кэшируйте ссылку между SignOut/SignIn без повторного обращения.</summary>
    public FirebaseClient RealtimeDatabase
    {
        get
        {
            EnsureInitialized();
            return _client!;
        }
    }

    /// <summary>Текущий Firebase uid или null, если сессии нет.</summary>
    public string? CurrentUserId => Auth.User?.Uid;

    /// <summary>true — текущая сессия создана через <see cref="TrySignInAnonymouslyAsync"/> (гость).</summary>
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
                // Публичный Web API key проекта. Для продакшена см. FIREBASE.md (User Secrets / env).
                ApiKey = "AIzaSyCl1Ix-ZEcM4JLBjFew5XsS1LTQIpg8j7U",
                AuthDomain = "sigmachess-75f04.firebaseapp.com",
                Providers = [new EmailProvider()],
                UserRepository = new FileUserRepository("appUserData")
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

    /// <summary>IdToken текущего пользователя для Storage и др.; <c>null</c>, если сессии нет.</summary>
    public Task<string?> GetIdTokenAsync(bool forceRefresh = false)
    {
        EnsureInitialized();
        if (_auth?.User is null)
        {
            return Task.FromResult<string?>(null);
        }

        return GetIdTokenFromAuthClientAsync(_auth, forceRefresh);
    }

    /// <summary>
    /// Свежий IdToken для каждого запроса RTDB (кэш Firebase сам отдаёт актуальный при forceRefresh: false).
    /// </summary>
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

    /// <summary>Анонимный вход для гостя — стабильный uid и те же правила RTDB, что у email-пользователя.</summary>
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
}
