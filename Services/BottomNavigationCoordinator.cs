using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SigmaChess.ViewModels;
using SigmaChess.Views;

namespace SigmaChess.Services;

public enum BottomNavSection
{
    None,
    Home,
    Puzzles,
    Follows,
}

/// <summary>Общая нижняя навигация: Home учитывает партию на GamePage (тот же попап, что «назад»).</summary>
public sealed class BottomNavigationCoordinator : INotifyPropertyChanged
{
    private readonly GameViewModel _game;

    private BottomNavSection _section = BottomNavSection.Home;

    public BottomNavigationCoordinator(GameViewModel game)
    {
        _game = game;

        NavigateHomeCommand = new Command(async () => await NavigateHomeAsync());
        NavigatePuzzlesCommand = new Command(async () => await NavigatePuzzlesAsync());
        NavigateFollowsCommand = new Command(async () => await NavigateFollowsAsync());
        NavigateSettingsCommand = new Command(async () => await NavigateSettingsAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand NavigateHomeCommand { get; }

    public ICommand NavigatePuzzlesCommand { get; }

    public ICommand NavigateFollowsCommand { get; }

    public ICommand NavigateSettingsCommand { get; }

    public bool IsHomeSelected => _section == BottomNavSection.Home;

    public bool IsPuzzlesSelected => _section == BottomNavSection.Puzzles;

    public bool IsFollowsSelected => _section == BottomNavSection.Follows;

    /// <summary>Синхронизация подсветки вкладки с текущей страницей Shell (в т.ч. после push/pop).</summary>
    public void SyncFromShell()
    {
        void Apply()
        {
            var page = Shell.Current?.CurrentPage;
            BottomNavSection? next = page switch
            {
                MainPage or GamePage => BottomNavSection.Home,
                PuzzlesPage => BottomNavSection.Puzzles,
                FollowsPage => BottomNavSection.Follows,
                PlayedGamesPage => BottomNavSection.None,
                SettingsPage => BottomNavSection.None,
                _ => null,
            };

            if (next is { } s)
            {
                ApplySection(s);
            }
        }

        if (MainThread.IsMainThread)
        {
            Apply();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(Apply);
        }
    }

    private void ApplySection(BottomNavSection s)
    {
        if (_section == s)
        {
            return;
        }

        _section = s;
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsPuzzlesSelected));
        OnPropertyChanged(nameof(IsFollowsSelected));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async Task NavigateHomeAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            if (Shell.Current.CurrentPage is GamePage)
            {
                await _game.ConfirmLeaveGameAndGoHomeAsync().ConfigureAwait(false);
                return;
            }

            await Shell.Current.GoToAsync("//MainPage");
        }).ConfigureAwait(false);
    }

    private async Task NavigatePuzzlesAsync()
    {
        if (!await EnsureAuthenticatedForRestrictedAsync().ConfigureAwait(false))
        {
            return;
        }

        await ShellGoAsync("//PuzzlesPage").ConfigureAwait(false);
    }

    private async Task NavigateFollowsAsync()
    {
        if (!await EnsureAuthenticatedForRestrictedAsync().ConfigureAwait(false))
        {
            return;
        }

        await ShellGoAsync("//FollowsPage").ConfigureAwait(false);
    }

    private async Task NavigateSettingsAsync()
    {
        if (!await EnsureAuthenticatedForRestrictedAsync().ConfigureAwait(false))
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            try
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings nav: {ex}");
                await Shell.Current.GoToAsync("//MainPage");
            }
        }).ConfigureAwait(false);
    }

    private static async Task ShellGoAsync(string route)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(route);
        }).ConfigureAwait(false);
    }

    /// <summary>Разделы нижней панели на гостевом Shell недоступны без входа.</summary>
    private static async Task<bool> EnsureAuthenticatedForRestrictedAsync()
    {
        if (Shell.Current is not AppShellNotAuth)
        {
            return true;
        }

        var goLogin = await ConfirmPopup.ShowAsync(
            "Account required",
            "Sign in or sign up to open this section.",
            "Log in",
            "Cancel");

        if (!goLogin || Shell.Current is null)
        {
            return false;
        }

        await Shell.Current.GoToAsync("//AuthPage");
        return false;
    }
}
