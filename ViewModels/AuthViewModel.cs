using System.Windows.Input;
using SigmaChess;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

/// <summary>
/// ViewModel страницы входа/регистрации. Один и тот же экран служит обоим режимам;
/// переключение между ними — через <see cref="IsRegisterMode"/>.
/// <para>
/// Реализует <see cref="IQueryAttributable"/>, чтобы Shell мог открыть страницу
/// сразу в нужном режиме через query-параметр <c>?mode=register</c>.
/// </para>
/// </summary>
public class AuthViewModel : ViewModelBase, IQueryAttributable
{
    private readonly AppService _appService;
    private bool _isRegisterMode;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;

    public AuthViewModel(AppService appService)
    {
        _appService = appService;
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

    public ICommand LoginCommand { get; }

    public ICommand RegisterCommand { get; }

    public ICommand ShowLoginModeCommand { get; }

    public ICommand ShowRegisterModeCommand { get; }

    // Логин: валидируем поля, дёргаем сервис, при успехе — переключаем Shell на «авторизованный»
    // и переходим на главную. Переключение Shell обязательно делать на UI-потоке.
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
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (Application.Current is App app)
                {
                    app.SetAuthenticatedShell();
                }
            });
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

    // Регистрация: при успехе переводим экран в режим логина, чтобы пользователь сразу залогинился.
    private async Task RegisterAsync()
    {
        if (!ValidateEmailAndPassword(requireConfirmPassword: true))
        {
            return;
        }

        var success = await _appService.TryRegister(Email, Password);
        if (!success)
        {
            ErrorMessage = "Registration failed";
            return;
        }

        ErrorMessage = string.Empty;
        IsRegisterMode = false;
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
    /// Принимает query-параметры из Shell. Поддерживается только <c>mode=register</c>;
    /// при остальных значениях остаёмся в режиме логина.
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
