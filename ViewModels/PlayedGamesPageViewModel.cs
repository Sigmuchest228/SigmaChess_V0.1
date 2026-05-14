using System.Collections.ObjectModel;
using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

public class PlayedGamesPageViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;

    private string? _profileUid;

    public PlayedGamesPageViewModel()
        : this(AppService.GetInstance(), AppService.GetInstance().FirebaseSync)
    {
    }

    public PlayedGamesPageViewModel(AppService appService, FirebaseSyncRepository firebaseSync)
    {
        _appService = appService;
        _firebaseSync = firebaseSync;

        Games = new ObservableCollection<PlayedGameRowViewModel>();

        GoBackCommand = new Command(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
            }
        });

        OpenReplayCommand = new Command<string>(
            async gameId =>
            {
                if (Shell.Current is null || string.IsNullOrWhiteSpace(gameId))
                {
                    return;
                }

                await Shell.Current.GoToAsync(
                    $"GameReplayPage?GameId={Uri.EscapeDataString(gameId.Trim())}");
            });
    }

    public ObservableCollection<PlayedGameRowViewModel> Games { get; }

    public bool HasGames => Games.Count > 0;

    public bool IsEmpty => Games.Count == 0;

    public string EmptyMessage => "No completed games yet.";

    public string PageTitle => "Played games";

    public ICommand GoBackCommand { get; }

    public ICommand OpenReplayCommand { get; }

    /// <summary>Optional query <c>UserId</c>; иначе текущий пользователь.</summary>
    public void ApplyNavigationQuery(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("UserId", out var raw))
        {
            return;
        }

        var s = raw as string ?? raw?.ToString();
        if (!string.IsNullOrWhiteSpace(s))
        {
            _profileUid = s.Trim();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var uid = _profileUid ?? _appService.CurrentUserId;
        if (string.IsNullOrEmpty(uid))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Games.Clear();
                OnPropertyChanged(nameof(HasGames));
                OnPropertyChanged(nameof(IsEmpty));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var summaries = await _firebaseSync.LoadPlayedGameSummariesForProfileAsync(uid, 120, cancellationToken)
                .ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Games.Clear();
                foreach (var s in summaries)
                {
                    Games.Add(PlayedGameRowViewModel.FromSummary(s));
                }

                OnPropertyChanged(nameof(HasGames));
                OnPropertyChanged(nameof(IsEmpty));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Games.Clear();
                OnPropertyChanged(nameof(HasGames));
                OnPropertyChanged(nameof(IsEmpty));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
