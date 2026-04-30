using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Auth.Repository;
using Firebase.Database;

namespace SigmaChess.Services;

/// <summary>
/// Тонкая обёртка над Firebase: аутентификация (email/пароль) и доступ к Realtime Database.
/// <para>
/// Главная архитектурная фишка — ленивая инициализация под локом. Создание клиентов Firebase
/// — тяжёлая операция (диск + сеть + криптография), и если делать её в конструкторе,
/// которое DI вызывает на старте приложения, на Android регулярно ловится ANR
/// «SigmaChess isn't responding». Поэтому конструктор ничего не делает, а реальные клиенты
/// создаются при первом обращении к свойствам <see cref="Auth"/>/<see cref="Client"/>.
/// </para>
/// </summary>
public class AppService
{
    private readonly object _initLock = new();
    private FirebaseAuthClient? _auth;
    private FirebaseClient? _client;

    public AppService()
    {
        // Намеренно пусто: Firebase инициализируется лениво при первом обращении,
        // чтобы не блокировать UI‑поток во время навигации на AuthPage (ANR на Android).
    }

    private FirebaseAuthClient Auth
    {
        get
        {
            EnsureInitialized();
            return _auth!;
        }
    }

    private FirebaseClient Client
    {
        get
        {
            EnsureInitialized();
            return _client!;
        }
    }

    // Двойная проверка с локом (double-checked locking):
    //   - быстрый путь без локов, если уже инициализирован;
    //   - под локом ещё раз проверяем, потому что другой поток мог успеть инициализировать,
    //     пока мы ждали лок.
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
                // FileUserRepository хранит токен на диске, чтобы пользователь оставался
                // залогиненным между запусками приложения.
                UserRepository = new FileUserRepository("appUserData")
            };

            var auth = new FirebaseAuthClient(config);
            // Realtime Database хочет получать токен на каждый запрос — отдаём фабрику,
            // которая берёт свежий IdToken из текущего пользователя auth.
            _client = new FirebaseClient(
                "https://sigmachess-75f04-default-rtdb.europe-west1.firebasedatabase.app",
                new FirebaseOptions
                {
                    AuthTokenAsyncFactory = () => Task.FromResult(auth.User.Credential.IdToken)
                });
            _auth = auth;
        }
    }

    /// <summary>
    /// Пытается зарегистрировать пользователя по email/паролю.
    /// Возвращает true при успехе, false при любой ошибке (занято/неверный формат/нет сети и т. д.).
    /// </summary>
    public async Task<bool> TryRegister(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        try
        {
            await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            return true;
        }
        catch
        {
            // Любую ошибку Firebase превращаем в false — подробности в UI пока не показываем.
            return false;
        }
    }

    /// <summary>Аналогично <see cref="TryRegister"/>, но с проверкой существующих учётных данных.</summary>
    public async Task<bool> TryLogin(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        try
        {
            await Auth.SignInWithEmailAndPasswordAsync(email, password);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Снимает сохранённую сессию. Вызывается из AppShellAuth при логауте.</summary>
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
