using System.Windows.Input;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

public class MainPageViewModel : ViewModelBase
{
    private const string AuthLoginRoute = nameof(AuthPage);
    private const string AuthSignupRoute = nameof(AuthPage) + "?mode=register";
    private const string GameRoute = nameof(GamePage);
    private const string PuzzlesRoute = nameof(PuzzlesPage);
    private const string FriendsRoute = nameof(FriendsPage);
    private const string WatchRoute = nameof(WatchPage);
    private const string LearnRoute = nameof(LearnPage);
    private const string MenuRoute = nameof(MenuPage);

    public MainPageViewModel()
    {
        OpenLoginCommand = new Command(async () => await NavigateAsync(AuthLoginRoute));
        OpenSignupCommand = new Command(async () => await NavigateAsync(AuthSignupRoute));

        OpenBotsCommand = new Command(async () => await NavigateAsync("//GamePage"));
        OpenPuzzlesCommand = new Command(async () => await NavigateAsync(PuzzlesRoute));
        OpenFriendsCommand = new Command(async () => await NavigateAsync(FriendsRoute));
        OpenWatchCommand = new Command(async () => await NavigateAsync(WatchRoute));

        OpenHomeCommand = new Command(async () => await NavigateAsync("//MainPage"));
        OpenLearnCommand = new Command(async () => await NavigateAsync(LearnRoute));
        OpenMenuCommand = new Command(async () => await NavigateAsync(MenuRoute));
    }

    private static async Task NavigateAsync(string route)
    {
        if (Shell.Current is null)
        {
            return;
        }

        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation failed for route '{route}': {ex}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.DisplayAlert("Navigation error", $"Could not open: {route}", "OK");
                }
            });
        }
    }

    public ICommand OpenLoginCommand { get; }

    public ICommand OpenSignupCommand { get; }

    public ICommand OpenBotsCommand { get; }

    public ICommand OpenPuzzlesCommand { get; }

    public ICommand OpenFriendsCommand { get; }

    public ICommand OpenWatchCommand { get; }

    public ICommand OpenHomeCommand { get; }

    public ICommand OpenLearnCommand { get; }

    public ICommand OpenMenuCommand { get; }
}
