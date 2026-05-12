using System.Windows.Input;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

/// <summary>Общие настройки приложения (обои и выход из аккаунта).</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly LogoutSessionService _logout;

    public SettingsViewModel(LogoutSessionService logout)
    {
        _logout = logout;

        OpenWallpapersCommand = new Command(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(nameof(ProfileWallpaperSettingsPage));
        });

        GoBackCommand = new Command(async () => await ShellNavigationHelper.PopOrMainAsync());

        LogoutCommand = new Command(async () => await _logout.PerformFullLogoutAsync());
    }

    public ICommand OpenWallpapersCommand { get; }

    public ICommand GoBackCommand { get; }

    public ICommand LogoutCommand { get; }
}
