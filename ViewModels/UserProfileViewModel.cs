using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

public class UserProfileViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;
    private readonly IPhotoSourcePicker _photoPicker;

    private ImageSource? _profileAvatarSource = ImageSource.FromFile("defaultsigma.jpg");

    private string _profileUserName = "—";

    private string _memberSinceDateText = "—";

    private string? _viewingUserId;

    private bool _playedGamesLoaded;

    private string _respectFromSigmasText = string.Empty;

    public UserProfileViewModel()
        : this(
            AppService.GetInstance(),
            AppService.GetInstance().FirebaseSync,
            AppService.GetInstance().PhotoPicker)
    {
    }

    public UserProfileViewModel(
        AppService appService,
        FirebaseSyncRepository firebaseSync,
        IPhotoSourcePicker photoPicker)
    {
        _appService = appService;
        _firebaseSync = firebaseSync;
        _photoPicker = photoPicker;

        ProfileStats = new ObservableCollection<ProfileStatRowViewModel>();
        PlayedGames = new ObservableCollection<PlayedGameRowViewModel>();

        OpenSettingsCommand = new Command(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(nameof(SettingsPage));
        });

        GoBackCommand = new Command(async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
            }
        });

        ChangeAvatarCommand = new Command(async () => await ChangeAvatarAsync());

        OpenFullPlayedGamesCommand = new Command(async () => await OpenFullPlayedGamesAsync());

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

    /// <summary>Вызывается из <see cref="UserProfilePage.ApplyQueryAttributes"/> при переходе с query.</summary>
    public void ApplyNavigationQuery(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("UserId", out var raw))
        {
            return;
        }

        var s = raw as string ?? raw?.ToString();
        if (string.IsNullOrWhiteSpace(s))
        {
            return;
        }

        _viewingUserId = s.Trim();
        OnPropertyChanged(nameof(IsOwnProfile));
        OnPropertyChanged(nameof(PageTitle));
    }

    /// <summary>Строка вида «Got respect from N sigmas» из <c>respectReceived</c>.</summary>
    public string RespectFromSigmasText
    {
        get => _respectFromSigmasText;
        private set
        {
            if (_respectFromSigmasText == value)
            {
                return;
            }

            _respectFromSigmasText = value;
            OnPropertyChanged();
        }
    }

    public bool IsOwnProfile =>
        string.IsNullOrWhiteSpace(_viewingUserId)
        || (_appService.CurrentUserId is not null
            && string.Equals(_viewingUserId, _appService.CurrentUserId, StringComparison.Ordinal));

    public string PageTitle => IsOwnProfile ? "Profile" : "Player";

    public ObservableCollection<ProfileStatRowViewModel> ProfileStats { get; }

    public ObservableCollection<PlayedGameRowViewModel> PlayedGames { get; }

    /// <summary>Uid профиля на экране (свой или из query).</summary>
    public string? ActiveProfileUid =>
        string.IsNullOrWhiteSpace(_viewingUserId) ? _appService.CurrentUserId : _viewingUserId;

    public bool HasPlayedGames => PlayedGames.Count > 0;

    public bool ShowPlayedGamesEmpty => _playedGamesLoaded && PlayedGames.Count == 0;

    public bool ShowSeeAllPlayedGames => _playedGamesLoaded && HasPlayedGames;

    public ICommand OpenFullPlayedGamesCommand { get; }

    public ICommand OpenReplayCommand { get; }

    public ImageSource? ProfileAvatarSource
    {
        get => _profileAvatarSource;
        private set
        {
            if (ReferenceEquals(_profileAvatarSource, value))
            {
                return;
            }

            _profileAvatarSource = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenSettingsCommand { get; }

    public ICommand GoBackCommand { get; }

    public ICommand ChangeAvatarCommand { get; }

    /// <summary>Отображаемое имя из RTDB (<c>UserName</c>).</summary>
    public string ProfileUserName
    {
        get => _profileUserName;
        private set
        {
            if (_profileUserName == value)
            {
                return;
            }

            _profileUserName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Дата регистрации (день, месяц прописью, год) из <c>RegisterDate</c> в локальном часовом поясе.</summary>
    public string MemberSinceDateText
    {
        get => _memberSinceDateText;
        private set
        {
            if (_memberSinceDateText == value)
            {
                return;
            }

            _memberSinceDateText = value;
            OnPropertyChanged();
        }
    }

    private static string FormatRegisterDateForDisplay(long? registerDateUnix)
    {
        if (registerDateUnix is null or < 1L)
        {
            return "—";
        }

        var raw = registerDateUnix.Value;
        // Новые узлы — миллисекунды; если когда-то записали секунды, значение будет намного меньше.
        var dto = raw < 10_000_000_000L
            ? DateTimeOffset.FromUnixTimeSeconds(raw)
            : DateTimeOffset.FromUnixTimeMilliseconds(raw);

        try
        {
            return dto.ToLocalTime().ToString("dd MMMM yyyy", CultureInfo.CurrentCulture);
        }
        catch
        {
            return "—";
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var profileUid = string.IsNullOrWhiteSpace(_viewingUserId)
            ? _appService.CurrentUserId
            : _viewingUserId;

        if (string.IsNullOrEmpty(profileUid))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ProfileAvatarSource = ImageSource.FromFile("defaultsigma.jpg");
                    ProfileUserName = "—";
                    MemberSinceDateText = "—";
                    RespectFromSigmasText = string.Empty;
                    ProfileStats.Clear();
                    PlayedGames.Clear();
                    _playedGamesLoaded = true;
                    NotifyPlayedGamesUi();
                }).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        _playedGamesLoaded = false;
        NotifyPlayedGamesUi();

        try
        {
            if (IsOwnProfile && !string.IsNullOrEmpty(_appService.CurrentUserId))
            {
                await _firebaseSync.EnsureUserProfileAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            var profile = IsOwnProfile && _appService.CurrentUserId is not null
                ? await _firebaseSync.GetUserProfileAsync(cancellationToken).ConfigureAwait(false)
                : await _firebaseSync.GetUserProfileByUidAsync(profileUid, cancellationToken).ConfigureAwait(false);

            var src = await UserAvatarPreview
                .LoadAsync(profileUid, profile?.AvatarUrl, cancellationToken, IsOwnProfile)
                .ConfigureAwait(false);

            var displayName = string.IsNullOrWhiteSpace(profile?.UserName)
                ? (IsOwnProfile ? "Player" : profileUid[..Math.Min(8, profileUid.Length)])
                : profile.UserName.Trim();

            var respectCount = 0;
            try
            {
                respectCount =
                    await _firebaseSync.GetRespectReceivedCountAsync(profileUid, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Нет правил / сеть — не блокируем отображение профиля.
            }

            var respectLine = $"Got respect from {respectCount} sigmas";
            var memberSince = FormatRegisterDateForDisplay(profile?.RegisterDate);

            await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ProfileAvatarSource = src;
                    ProfileUserName = displayName;
                    MemberSinceDateText = memberSince;
                    RespectFromSigmasText = respectLine;
                    ProfileStats.Clear();
                }).WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            await LoadPlayedGamesSectionAsync(profileUid, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ProfileAvatarSource = ImageSource.FromFile("defaultsigma.jpg");
                    ProfileUserName = "—";
                    MemberSinceDateText = "—";
                    RespectFromSigmasText = string.Empty;
                    ProfileStats.Clear();
                    PlayedGames.Clear();
                    _playedGamesLoaded = true;
                    NotifyPlayedGamesUi();
                }).WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task LoadPlayedGamesSectionAsync(string profileUid, CancellationToken cancellationToken)
    {
        try
        {
            var summaries =
                await _firebaseSync.LoadPlayedGameSummariesForProfileAsync(profileUid, 25, cancellationToken)
                    .ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PlayedGames.Clear();
                foreach (var s in summaries)
                {
                    PlayedGames.Add(PlayedGameRowViewModel.FromSummary(s));
                }

                _playedGamesLoaded = true;
                NotifyPlayedGamesUi();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PlayedGames.Clear();
                _playedGamesLoaded = true;
                NotifyPlayedGamesUi();
            }).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void NotifyPlayedGamesUi()
    {
        OnPropertyChanged(nameof(HasPlayedGames));
        OnPropertyChanged(nameof(ShowPlayedGamesEmpty));
        OnPropertyChanged(nameof(ShowSeeAllPlayedGames));
    }

    private async Task OpenFullPlayedGamesAsync()
    {
        var uid = ActiveProfileUid;
        if (Shell.Current is null || string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        await Shell.Current.GoToAsync($"PlayedGamesPage?UserId={Uri.EscapeDataString(uid)}");
    }

    private async Task ChangeAvatarAsync()
    {
        if (!IsOwnProfile)
        {
            return;
        }

        if (string.IsNullOrEmpty(_appService.CurrentUserId))
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                    await ConfirmPopup.ShowAsync("Profile", "Sign in to change your avatar.", "OK"))
                .ConfigureAwait(false);
            return;
        }

        var choice = await _photoPicker.PickSourceAsync().ConfigureAwait(false);

        Stream? stream = null;
        try
        {
            stream = choice switch
            {
                PickPhotoSource.Gallery => await PhotoMediaService.TryOpenGalleryPhotoAsync("Profile")
                    .ConfigureAwait(false),
                PickPhotoSource.Camera => await PhotoMediaService.TryOpenCameraPhotoAsync("Profile")
                    .ConfigureAwait(false),
                _ => null,
            };

            if (stream is null)
            {
                return;
            }

            var uid = _appService.CurrentUserId!;
            var cachePath = Path.Combine(FileSystem.CacheDirectory, $"avatar_local_{uid}.jpg");
            await using (var fs = File.Create(cachePath))
            {
                await stream.CopyToAsync(fs).ConfigureAwait(false);
            }

            string fullPickPath;
            try
            {
                fullPickPath = Path.GetFullPath(cachePath);
            }
            catch
            {
                fullPickPath = cachePath;
            }

            UserAvatarLocalStore.SetPendingLocalAvatarPath(fullPickPath);

            await _firebaseSync.PatchUserProfileFieldsAsync(new Dictionary<string, object?> { ["AvatarUrl"] = null })
                .ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() => ProfileAvatarSource = ImageSource.FromFile(fullPickPath))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                    await ConfirmPopup.ShowAsync("Profile", $"Could not update avatar: {ex.Message}", "OK"))
                .ConfigureAwait(false);
        }
        finally
        {
            stream?.Dispose();
        }
    }
}
