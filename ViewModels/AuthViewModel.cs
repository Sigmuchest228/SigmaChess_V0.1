using System.Windows.Input;
using Microsoft.Maui.Controls;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

// ViewModel экрана входа/регистрации (страница AuthPage). Одна и та же форма работает
// в двух режимах: вход и регистрация (переключается флагом IsRegisterMode). Хранит
// поля формы (email, имя, пароль), сообщение об ошибке и команды (гость, вход,
// регистрация, смена режима). За сам вход/регистрацию отвечает AppService, за профиль
// пользователя — FirebaseSyncRepository. IQueryAttributable позволяет открыть экран
// сразу в нужном режиме через параметр навигации.
public class AuthViewModel : ViewModelBase, IQueryAttributable
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;
    private bool _isRegisterMode;
    private string _email = string.Empty;
    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;

    // Конструктор без параметров (для интерфейса): берёт сервисы из общего AppService.
    public AuthViewModel()
        : this(AppService.GetInstance(), AppService.GetInstance().FirebaseSync)
    {
    }

    // Основной конструктор: сохраняет сервисы и создаёт команды формы (вход гостем,
    // вход, регистрация, переключение между режимами входа и регистрации).
    public AuthViewModel(AppService appService, FirebaseSyncRepository firebaseSync)
    {
        _appService = appService;
        _firebaseSync = firebaseSync;
        GuestCommand = new Command(async () => await LoginAsGuestAsync());
        LoginCommand = new Command(async () => await LoginAsync());
        RegisterCommand = new Command(async () => await RegisterAsync());
        ShowLoginModeCommand = new Command(() => IsRegisterMode = false);
        ShowRegisterModeCommand = new Command(() => IsRegisterMode = true);
    }

    // Режим формы: true — регистрация, false — вход. При смене чистит ошибку, в режиме
    // входа очищает имя пользователя и обновляет зависимые свойства (заголовок и т.д.).
    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            if (_isRegisterMode == value)
            {
                return;
            }

            _isRegisterMode = value;
            ErrorMessage = string.Empty;
            if (!value)
            {
                UserName = string.Empty;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IsLoginMode));
        }
    }

    // Удобный обратный флаг и заголовок экрана в зависимости от режима.
    public bool IsLoginMode => !IsRegisterMode;

    public string PageTitle => IsRegisterMode ? "Create Account" : "Login";

    // Поля формы. Каждое при изменении уведомляет интерфейс.
    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
        }
    }

    public string UserName
    {
        get => _userName;
        set
        {
            _userName = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged();
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            _confirmPassword = value;
            OnPropertyChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    // Команды формы: войти гостем, войти, зарегистрироваться, показать режим входа,
    // показать режим регистрации.
    public ICommand GuestCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ShowLoginModeCommand { get; }
    public ICommand ShowRegisterModeCommand { get; }

    // Принимает параметры навигации: если передан mode=register — открывает форму
    // сразу в режиме регистрации.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("mode", out var modeObj))
        {
            return;
        }

        var mode = modeObj?.ToString();
        IsRegisterMode = string.Equals(mode, "register", StringComparison.OrdinalIgnoreCase);
    }

    // Вход гостем (анонимно): входит, создаёт профиль при необходимости и уходит на
    // главный экран.
    private async Task LoginAsGuestAsync()
    {
        await RunBusyAsync(async () =>
        {
            _ = await _appService.TrySignInAnonymouslyAsync();
            await TryEnsureUserProfileAsync();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.GoToAsync("//MainPage");
                }
            });
        });
    }

    // Вход по email/паролю: проверяет поля, пробует войти, при успехе создаёт профиль и
    // переключает приложение на «авторизованный» Shell. Иначе показывает ошибку.
    private async Task LoginAsync()
    {
        if (!ValidateEmailAndPassword(requireConfirmPassword: false))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var success = await _appService.TryLogin(Email, Password);
            ErrorMessage = success ? string.Empty : "Login failed";
            if (!success)
            {
                return;
            }

            await TryEnsureUserProfileAsync();
            await NavigateToAuthenticatedShellAsync();
        });
    }

    // Регистрация: проверяет поля, создаёт аккаунт, затем сразу входит, создаёт профиль
    // с выбранным именем и переключает на «авторизованный» Shell. На ошибках показывает
    // сообщение или возвращает в режим входа.
    private async Task RegisterAsync()
    {
        if (!ValidateRegistrationFields())
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (!await _appService.TryRegister(Email, Password))
            {
                ErrorMessage = "Registration failed";
                return;
            }

            if (!await _appService.TryLogin(Email, Password))
            {
                ErrorMessage = string.Empty;
                IsRegisterMode = false;
                return;
            }

            await TryEnsureUserProfileAsync(UserName.Trim());

            ErrorMessage = string.Empty;
            UserName = string.Empty;
            IsRegisterMode = false;
            await NavigateToAuthenticatedShellAsync();
        });
    }

    // Помощник: выполняет работу, выставив IsBusy=true на время (для индикатора и
    // блокировки кнопок) и гарантированно сняв его в конце.
    private async Task RunBusyAsync(Func<Task> work)
    {
        IsBusy = true;
        try
        {
            await work();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Создаёт/обновляет профиль пользователя в Firebase (с необязательным желаемым
    // именем). Ошибки молча игнорируются, чтобы не мешать входу.
    private async Task TryEnsureUserProfileAsync(string? preferredUserName = null)
    {
        try
        {
            await _firebaseSync.EnsureUserAsync(preferredUserName);
        }
        catch
        {

        }
    }

    // Переключает приложение на «авторизованный» Shell (главное меню после входа).
    private static Task NavigateToAuthenticatedShellAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current is App app)
            {
                app.SetAuthenticatedShell();
            }
        });

    // Проверяет поля регистрации: сначала email и пароль (с подтверждением), затем имя
    // (длина 2..24 и допустимые символы). При ошибке заполняет ErrorMessage.
    private bool ValidateRegistrationFields()
    {
        if (!ValidateEmailAndPassword(requireConfirmPassword: true))
        {
            return false;
        }

        var name = UserName.Trim();
        if (name.Length < 2 || name.Length > 24)
        {
            ErrorMessage = "Username must be 2–24 characters";
            return false;
        }

        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch is ' ' or '_' or '-')
            {
                continue;
            }

            ErrorMessage = "Username: only letters, digits, space, _ and -";
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    // Проверяет email (есть @ и точка) и пароль (минимум 8 символов), а при
    // requireConfirmPassword — совпадение с подтверждением. При ошибке заполняет
    // ErrorMessage и возвращает false.
    private bool ValidateEmailAndPassword(bool requireConfirmPassword)
    {
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@') || !Email.Contains('.'))
        {
            ErrorMessage = "Invalid email";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters";
            return false;
        }

        if (requireConfirmPassword && Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords don't match";
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }
}
