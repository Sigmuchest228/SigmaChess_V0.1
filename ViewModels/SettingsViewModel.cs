using System.Diagnostics;
using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

/// <summary>Настройки приложения и выход из аккаунта.</summary>
public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
        GoBackCommand = new Command(async () => await GoBackAsync());
        LogoutCommand = new Command(async () => await AppService.GetInstance().PerformFullLogoutAsync());
    }

    public ICommand GoBackCommand { get; }

    public ICommand LogoutCommand { get; }

    private static async Task GoBackAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Shell pop: {ex}");
                await Shell.Current.GoToAsync("//MainPage");
            }
        });
    }
}
