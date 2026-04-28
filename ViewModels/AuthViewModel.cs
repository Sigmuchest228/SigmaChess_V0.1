using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

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
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

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
