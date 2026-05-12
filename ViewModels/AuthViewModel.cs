using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

/// <summary>
/// ViewModel страницы входа/регистрации. Один и тот же экран служит обоим режимам;
/// переключение между ними — через <see cref="IsRegisterMode"/>.
/// <para>
/// Реализует <see cref="IQueryAttributable"/> для режима регистрации через query <c>?mode=register</c> при навигации Shell.
/// </para>
/// </summary>
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

    /// <summary>
    /// true — экран в режиме регистрации, false — в режиме логина.
    /// При смене сбрасывается ошибка и обновляются зависимые свойства, на которые
    /// привязан XAML (заголовок страницы, инверсный флаг IsLoginMode).
    /// </summary>
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

    public bool IsLoginMode => !IsRegisterMode;

    public string PageTitle => IsRegisterMode ? "Create Account" : "Login";

    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Отображаемое имя в RTDB; заполняется только при регистрации.</summary>
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

    /// <summary>Текст ошибки под формой. Пустая строка прячет блок (через Converter в XAML).</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }
    public ICommand GuestCommand { get; }

    public ICommand LoginCommand { get; }


    public ICommand RegisterCommand { get; }

    public ICommand ShowLoginModeCommand { get; }

    public ICommand ShowRegisterModeCommand { get; }

    private async Task LoginAsGuestAsync()
    {
        _ = await _appService.TrySignInAnonymouslyAsync();
        try
        {
            await _firebaseSync.EnsureUserProfileAsync();
        }
        catch
        {
            // Без сети гость всё равно может играть локально; облако — по возможности.
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//MainPage");
            }
        });
    }
    // Логин: при успехе переключаем корень навигации на авторизованный и остаёмся на главной (новый стек).
    private async Task LoginAsync()
    {
        if (!ValidateEmailAndPassword(requireConfirmPassword: false))
        {
            return;
        }

        var success = await _appService.TryLogin(Email, Password);
        ErrorMessage = success ? string.Empty : "Login failed";
        if (success)
        {
            try
            {
                await _firebaseSync.EnsureUserProfileAsync();
            }
            catch
            {
                // Профиль догрузится при первом сохранении партии.
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (Application.Current is App app)
                {
                    app.SetAuthenticatedShell();
                }
            });
        }
    }

    // Регистрация: Firebase Auth + вход + профиль RTDB + авторизованный корень навигации.
    private async Task RegisterAsync()
    {
        if (!ValidateRegistrationFields())
        {
            return;
        }

        var success = await _appService.TryRegister(Email, Password);
        if (!success)
        {
            ErrorMessage = "Registration failed";
            return;
        }

        // Сразу входим теми же учётными данными: в Authentication уже есть uid, создаём профиль в RTDB.
        var loggedIn = await _appService.TryLogin(Email, Password);
        if (!loggedIn)
        {
            ErrorMessage = string.Empty;
            IsRegisterMode = false;
            return;
        }

        try
        {
            await _firebaseSync.EnsureUserProfileAsync(UserName.Trim());
        }
        catch
        {
            // Профиль догрузится при первом сохранении партии или при следующем открытии главной.
        }

        ErrorMessage = string.Empty;
        UserName = string.Empty;
        IsRegisterMode = false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current is App app)
            {
                app.SetAuthenticatedShell();
            }
        });
    }

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

        // Буквы/цифры/пробел/подчёркивание/дефис (латиница и кириллица).
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

    // Минимальная валидация на стороне клиента: формат email, длина пароля, совпадение паролей при регистрации.
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

    /// <summary>
    /// Принимает query-параметры при навигации (если используются). Поддерживается <c>mode=register</c>.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("mode", out var modeObj))
        {
            return;
        }

        var mode = modeObj?.ToString();
        IsRegisterMode = string.Equals(mode, "register", StringComparison.OrdinalIgnoreCase);
    }
}
