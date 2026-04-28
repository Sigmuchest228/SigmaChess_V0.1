using System.Windows.Input;

namespace SigmaChess.ViewModels;

public class MainPageViewModel : ViewModelBase
{
    public MainPageViewModel()
    {
        OpenLoginCommand = new Command(async () => await Shell.Current.GoToAsync("//AuthPage"));
        OpenSignupCommand = new Command(async () => await Shell.Current.GoToAsync("//AuthPage?mode=register"));

        OpenBotsCommand = new Command(async () => await Shell.Current.GoToAsync("GamePage"));
        OpenPuzzlesCommand = new Command(async () => await Shell.Current.GoToAsync("PuzzlesPage"));
        OpenFriendsCommand = new Command(async () => await Shell.Current.GoToAsync("FriendsPage"));
        OpenWatchCommand = new Command(async () => await Shell.Current.GoToAsync("WatchPage"));

        OpenHomeCommand = new Command(async () => await Shell.Current.GoToAsync("//MainPage"));
        OpenLearnCommand = new Command(async () => await Shell.Current.GoToAsync("LearnPage"));
        OpenMenuCommand = new Command(async () => await Shell.Current.GoToAsync("MenuPage"));
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
