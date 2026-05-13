using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

public sealed class PuzzlesPageViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;

    private string _rankTitle = "—";
    private int _puzzlesSolved;
    private bool _loadedOnce;

    public PuzzlesPageViewModel(AppService appService, FirebaseSyncRepository firebaseSync)
    {
        _appService = appService;
        _firebaseSync = firebaseSync;

        PuzzleRows = new ObservableCollection<PuzzleRowViewModel>();

        OpenPuzzleCommand = new Command<string>(
            async id =>
            {
                if (Shell.Current is null || string.IsNullOrWhiteSpace(id))
                {
                    return;
                }

                await Shell.Current.GoToAsync(
                    $"PuzzleSolvePage?PuzzleId={Uri.EscapeDataString(id.Trim())}");
            });
    }

    public ObservableCollection<PuzzleRowViewModel> PuzzleRows { get; }

    public string Title => "Puzzles";

    public string RankTitle
    {
        get => _rankTitle;
        private set => SetField(ref _rankTitle, value, nameof(RankTitle));
    }

    public int PuzzlesSolved
    {
        get => _puzzlesSolved;
        private set => SetField(ref _puzzlesSolved, value, nameof(PuzzlesSolved));
    }

    public bool HasPuzzles => PuzzleRows.Count > 0;

    public bool ShowEmpty => PuzzleRows.Count == 0 && _loadedOnce;

    public ICommand OpenPuzzleCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var uid = _appService.CurrentUserId;
        if (string.IsNullOrEmpty(uid))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PuzzleRows.Clear();
                RankTitle = "—";
                PuzzlesSolved = 0;
                _loadedOnce = true;
                NotifyPuzzleListProps();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await _firebaseSync.EnsureUserProfileAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var profile = await _firebaseSync.GetUserProfileByUidAsync(uid, cancellationToken).ConfigureAwait(false);
            var solvedCount = UserSigmaRank.NormalizePuzzlesSolved(profile?.PuzzlesSolved);
            var rank = UserSigmaRank.GetRankTitle(solvedCount);

            var solvedIds = await _firebaseSync.GetSolvedPuzzleIdsAsync(uid, cancellationToken).ConfigureAwait(false);
            var catalog = await _firebaseSync.LoadChessPuzzlesOrderedAsync(cancellationToken).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RankTitle = rank;
                PuzzlesSolved = solvedCount;
                PuzzleRows.Clear();
                foreach (var (id, dto) in catalog)
                {
                    var title = string.IsNullOrWhiteSpace(dto.Title) ? id : dto.Title.Trim();
                    var solved = solvedIds.Contains(id);
                    var subtitle = solved ? "Solved" : "Tap to solve";
                    PuzzleRows.Add(new PuzzleRowViewModel(id, title, subtitle, solved));
                }

                _loadedOnce = true;
                NotifyPuzzleListProps();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PuzzleRows.Clear();
                _loadedOnce = true;
                NotifyPuzzleListProps();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void NotifyPuzzleListProps()
    {
        OnPropertyChanged(nameof(HasPuzzles));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    private bool SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
