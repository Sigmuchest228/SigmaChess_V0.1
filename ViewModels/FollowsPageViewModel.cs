using System.Collections.ObjectModel;
using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

public sealed class FollowsPageViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;

    private string _searchQuery = string.Empty;
    private bool _isBusy;
    private bool _searchRunning;
    private bool _showSearchOutcome;
    private HashSet<string> _followUids = [];

    public FollowsPageViewModel(AppService appService, FirebaseSyncRepository firebaseSync)
    {
        _appService = appService;
        _firebaseSync = firebaseSync;

        Follows = new ObservableCollection<FollowRowViewModel>();
        SearchResults = new ObservableCollection<SearchUserRowViewModel>();

        RefreshFollowsCommand = new Command(async () => await RefreshFollowsAsync());
        SearchUsersCommand = new Command(async () => await ExecuteSearchAsync());
        ClearSearchCommand = new Command(() =>
        {
            SearchQuery = string.Empty;
            SearchResults.Clear();
            SetShowSearchOutcome(false);
            OnPropertyChanged(nameof(HasSearchResults));
        });

        AddFollowCommand = new Command<string>(async uid => await AddFollowAndRefreshAsync(uid));
    }

    public ObservableCollection<FollowRowViewModel> Follows { get; }

    public ObservableCollection<SearchUserRowViewModel> SearchResults { get; }

    public string Title => "Follows";

    public string EmptyFollowsMessage => "You are not following anyone yet.";

    public string SearchPlaceholder => "Search by name or username";

    public string NoSearchHitsMessage => "No players match that search.";

    public bool ShowSearchPanel => _showSearchOutcome;

    public bool ShowNoSearchHits => _showSearchOutcome && !HasSearchResults;

    public bool HasFollows => Follows.Count > 0;

    public bool ShowEmptyFollowsMessage => !HasFollows;

    public int FollowsCount => Follows.Count;

    public bool HasSearchResults => SearchResults.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
            {
                return;
            }

            _searchQuery = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshFollowsCommand { get; }

    public Command SearchUsersCommand { get; }

    public ICommand ClearSearchCommand { get; }

    public Command<string> AddFollowCommand { get; }

    public Task LoadAsync(CancellationToken cancellationToken = default) => RefreshFollowsAsync(cancellationToken);

    private void SetShowSearchOutcome(bool value)
    {
        if (_showSearchOutcome == value)
        {
            return;
        }

        _showSearchOutcome = value;
        OnPropertyChanged(nameof(ShowSearchPanel));
        OnPropertyChanged(nameof(ShowNoSearchHits));
    }

    private static string FormatPuzzlesLine(int solved) => $"{solved} puzzles solved";

    private async Task RefreshFollowsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appService.CurrentUserId))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Follows.Clear();
                _followUids = [];
                OnPropertyChanged(nameof(HasFollows));
                OnPropertyChanged(nameof(ShowEmptyFollowsMessage));
                OnPropertyChanged(nameof(FollowsCount));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        IsBusy = true;
        try
        {
            await _firebaseSync.EnsureUserProfileAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var summaries = await _firebaseSync.LoadFollowsAsync(cancellationToken).ConfigureAwait(false);
            _followUids = summaries.Select(s => s.Uid).ToHashSet(StringComparer.Ordinal);

            var rows = new List<FollowRowViewModel>();
            foreach (var s in summaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var avatar = await UserAvatarPreview
                    .LoadAsync(s.Uid, s.AvatarUrl, cancellationToken, allowLocalPending: false).ConfigureAwait(false);
                var uidLocal = s.Uid;
                var rank = UserSigmaRank.GetRankTitle(s.PuzzlesSolved);
                var row = new FollowRowViewModel(
                    s.Uid,
                    s.DisplayName,
                    rank,
                    FormatPuzzlesLine(s.PuzzlesSolved),
                    () => OpenProfileAsync(uidLocal));
                row.Avatar = avatar;
                rows.Add(row);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Follows.Clear();
                foreach (var r in rows)
                {
                    Follows.Add(r);
                }

                OnPropertyChanged(nameof(HasFollows));
                OnPropertyChanged(nameof(ShowEmptyFollowsMessage));
                OnPropertyChanged(nameof(FollowsCount));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteSearchAsync(CancellationToken cancellationToken = default)
    {
        if (_searchRunning)
        {
            return;
        }

        var q = SearchQuery.Trim();
        if (q.Length < 2 || string.IsNullOrEmpty(_appService.CurrentUserId))
        {
            SetShowSearchOutcome(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SearchResults.Clear();
                OnPropertyChanged(nameof(HasSearchResults));
                OnPropertyChanged(nameof(ShowNoSearchHits));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        _searchRunning = true;
        try
        {
            SetShowSearchOutcome(true);

            var summaries = await _firebaseSync.SearchUsersByPrefixAsync(q, 24, cancellationToken).ConfigureAwait(false);
            var rows = new List<SearchUserRowViewModel>();
            foreach (var s in summaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var me = _appService.CurrentUserId!;
                var isFollowing = _followUids.Contains(s.Uid);
                var isSelf = string.Equals(s.Uid, me, StringComparison.Ordinal);
                var rank = UserSigmaRank.GetRankTitle(s.PuzzlesSolved);
                var row = new SearchUserRowViewModel(
                    s.Uid,
                    s.DisplayName,
                    rank,
                    FormatPuzzlesLine(s.PuzzlesSolved),
                    !isFollowing && !isSelf);
                row.Avatar = await UserAvatarPreview
                    .LoadAsync(s.Uid, s.AvatarUrl, cancellationToken, allowLocalPending: false).ConfigureAwait(false);
                rows.Add(row);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SearchResults.Clear();
                foreach (var r in rows)
                {
                    SearchResults.Add(r);
                }

                OnPropertyChanged(nameof(HasSearchResults));
                OnPropertyChanged(nameof(ShowNoSearchHits));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _searchRunning = false;
        }
    }

    private async Task AddFollowAndRefreshAsync(string? targetUid)
    {
        if (string.IsNullOrWhiteSpace(targetUid) || _isBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _firebaseSync.AddFollowAsync(targetUid).ConfigureAwait(false);
            await RefreshFollowsAsync().ConfigureAwait(false);
            await ExecuteSearchAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task OpenProfileAsync(string uid)
    {
        if (Shell.Current is null || string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        await Shell.Current.GoToAsync($"UserProfilePage?UserId={Uri.EscapeDataString(uid)}");
    }
}
