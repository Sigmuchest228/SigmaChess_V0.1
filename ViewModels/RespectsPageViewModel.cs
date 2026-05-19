using System.Collections.ObjectModel;
using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

public class RespectsPageViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;

    private string _searchQuery = string.Empty;
    private bool _searchRunning;
    private bool _showSearchOutcome;
    private HashSet<string> _respectUids = [];

    public RespectsPageViewModel()
        : this(AppService.GetInstance(), AppService.GetInstance().FirebaseSync)
    {
    }

    public RespectsPageViewModel(AppService appService, FirebaseSyncRepository firebaseSync)
    {
        _appService = appService;
        _firebaseSync = firebaseSync;

        RespectList = new ObservableCollection<RespectRowViewModel>();
        SearchResults = new ObservableCollection<SearchUserRowViewModel>();

        RefreshRespectsCommand = new Command(async () => await RefreshRespectsAsync());
        SearchUsersCommand = new Command(async () => await ExecuteSearchAsync());
        ClearSearchCommand = new Command(() =>
        {
            SearchQuery = string.Empty;
            SearchResults.Clear();
            SetShowSearchOutcome(false);
            OnPropertyChanged(nameof(HasSearchResults));
        });

        AddRespectCommand = new Command<string>(async uid => await AddRespectAndRefreshAsync(uid));
        RemoveRespectCommand = new Command<string>(async uid => await RemoveRespectAndRefreshAsync(uid));
    }

    public ObservableCollection<RespectRowViewModel> RespectList { get; }

    public ObservableCollection<SearchUserRowViewModel> SearchResults { get; }

    public string Title => "Respect";

    public string EmptyRespectListMessage => "Your respect list is empty.";

    public string SearchPlaceholder => "Search by name or username";

    public string NoSearchHitsMessage => "No players match that search.";

    public bool ShowSearchPanel => _showSearchOutcome;

    public bool ShowNoSearchHits => _showSearchOutcome && !HasSearchResults;

    public bool HasRespects => RespectList.Count > 0;

    public bool ShowEmptyRespectList => !HasRespects;

    public int RespectCount => RespectList.Count;

    public bool HasSearchResults => SearchResults.Count > 0;

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

    public ICommand RefreshRespectsCommand { get; }

    public Command SearchUsersCommand { get; }

    public ICommand ClearSearchCommand { get; }

    public Command<string> AddRespectCommand { get; }

    public Command<string> RemoveRespectCommand { get; }

    public Task LoadAsync(CancellationToken cancellationToken = default) => RefreshRespectsAsync(cancellationToken);

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

    private async Task RefreshRespectsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appService.CurrentUserId))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RespectList.Clear();
                _respectUids = [];
                OnPropertyChanged(nameof(HasRespects));
                OnPropertyChanged(nameof(ShowEmptyRespectList));
                OnPropertyChanged(nameof(RespectCount));
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        IsBusy = true;
        try
        {
            await _firebaseSync.EnsureUserAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var summaries = await _firebaseSync.LoadRespectsAsync(cancellationToken).ConfigureAwait(false);
            _respectUids = summaries.Select(s => s.Uid).ToHashSet(StringComparer.Ordinal);

            var rows = new List<RespectRowViewModel>();
            foreach (var s in summaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var avatar = await UserAvatarPreview
                    .LoadAsync(s.Uid, s.AvatarUrl, cancellationToken, allowLocalPending: false).ConfigureAwait(false);
                var uidLocal = s.Uid;
                var row = new RespectRowViewModel(
                    s.Uid,
                    s.DisplayName,
                    () => OpenProfileAsync(uidLocal));
                row.Avatar = avatar;
                rows.Add(row);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RespectList.Clear();
                foreach (var r in rows)
                {
                    RespectList.Add(r);
                }

                OnPropertyChanged(nameof(HasRespects));
                OnPropertyChanged(nameof(ShowEmptyRespectList));
                OnPropertyChanged(nameof(RespectCount));
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
                var isRespected = _respectUids.Contains(s.Uid);
                var isSelf = string.Equals(s.Uid, me, StringComparison.Ordinal);
                var uidLocal = s.Uid;
                var row = new SearchUserRowViewModel(
                    s.Uid,
                    s.DisplayName,
                    !isRespected && !isSelf,
                    () => OpenProfileAsync(uidLocal));
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

    private async Task AddRespectAndRefreshAsync(string? targetUid)
    {
        if (string.IsNullOrWhiteSpace(targetUid) || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _firebaseSync.AddRespectAsync(targetUid).ConfigureAwait(false);
            await RefreshRespectsAsync().ConfigureAwait(false);
            await ExecuteSearchAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RemoveRespectAndRefreshAsync(string? targetUid)
    {
        if (string.IsNullOrWhiteSpace(targetUid) || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _firebaseSync.RemoveRespectAsync(targetUid).ConfigureAwait(false);
            await RefreshRespectsAsync().ConfigureAwait(false);
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
