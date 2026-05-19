using System.Windows.Input;
using Microsoft.Maui.Controls;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

/// <summary>
/// ViewModel страницы входа/регистрации. Переключение режимов — <see cref="IsRegisterMode"/>.
/// Поддерживает query <c>?mode=register</c> через <see cref="IQueryAttributable"/>.
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

    public AuthViewModel()
        : this(AppService.GetInstance(), AppService.GetInstance().FirebaseSync)
    {
    }

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

    public ICommand GuestCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ShowLoginModeCommand { get; }
    public ICommand ShowRegisterModeCommand { get; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("mode", out var modeObj))
        {
            return;
        }

        var mode = modeObj?.ToString();
        IsRegisterMode = string.Equals(mode, "register", StringComparison.OrdinalIgnoreCase);
    }

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

    private async Task TryEnsureUserProfileAsync(string? preferredUserName = null)
    {
        try
        {
            await _firebaseSync.EnsureUserAsync(preferredUserName);
        }
        catch
        {
            // Без сети или при сбое RTDB — локальная игра возможна, профиль догрузится позже.
        }
    }

    private static Task NavigateToAuthenticatedShellAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current is App app)
            {
                app.SetAuthenticatedShell();
            }
        });

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
