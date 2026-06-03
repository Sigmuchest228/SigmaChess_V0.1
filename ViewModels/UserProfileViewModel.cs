using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using SigmaChess.Services;
using SigmaChess.Views;

namespace SigmaChess.ViewModels;

// ViewModel экрана профиля (страница UserProfilePage). Показывает данные пользователя:
// аватар, имя, дату регистрации, сколько «уважения» получено и последние сыгранные
// партии. Может показывать как свой профиль (тогда доступна смена аватара и настройки),
// так и чужой (id приходит через навигацию). Данные берёт из Firebase, фото — через
// IPhotoSourcePicker.
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

    // Конструктор без параметров (для интерфейса): берёт сервисы из общего AppService.
    public UserProfileViewModel()
        : this(
            AppService.GetInstance(),
            AppService.GetInstance().FirebaseSync,
            AppService.GetInstance().PhotoPicker)
    {
    }

    // Основной конструктор: сохраняет сервисы, создаёт коллекции (статистика, партии) и
    // команды (настройки, назад, сменить аватар, все партии, открыть реплей).
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

    // Принимает параметры навигации: если передан UserId — показываем профиль этого
    // пользователя (иначе свой). Обновляет зависимые свойства (свой ли профиль,
    // заголовок).
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

    // Текст «получил уважение от N сигм».
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

    // Свой ли это профиль: да, если id для просмотра не задан или совпадает с текущим
    // пользователем. От этого зависят заголовок и доступность смены аватара.
    public bool IsOwnProfile =>
        string.IsNullOrWhiteSpace(_viewingUserId)
        || (_appService.CurrentUserId is not null
            && string.Equals(_viewingUserId, _appService.CurrentUserId, StringComparison.Ordinal));

    // Заголовок экрана: «Profile» для своего, «Player» для чужого.
    public string PageTitle => IsOwnProfile ? "Profile" : "Player";

    // Коллекции для интерфейса: строки статистики и последние сыгранные партии.
    public ObservableCollection<ProfileStatRowViewModel> ProfileStats { get; }

    public ObservableCollection<PlayedGameRowViewModel> PlayedGames { get; }

    // Id профиля, который реально показываем (чужой или текущий пользователь).
    public string? ActiveProfileUid =>
        string.IsNullOrWhiteSpace(_viewingUserId) ? _appService.CurrentUserId : _viewingUserId;

    // Флаги для блока партий: есть ли партии, показывать ли «пусто», показывать ли
    // ссылку «все партии». Учитывают, загрузились ли партии (_playedGamesLoaded).
    public bool HasPlayedGames => PlayedGames.Count > 0;

    public bool ShowPlayedGamesEmpty => _playedGamesLoaded && PlayedGames.Count == 0;

    public bool ShowSeeAllPlayedGames => _playedGamesLoaded && HasPlayedGames;

    // Команды: открыть полный список партий и открыть реплей выбранной партии.
    public ICommand OpenFullPlayedGamesCommand { get; }

    public ICommand OpenReplayCommand { get; }

    // Картинка аватара профиля.
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

    // Команды: открыть настройки, назад, сменить аватар.
    public ICommand OpenSettingsCommand { get; }

    public ICommand GoBackCommand { get; }

    public ICommand ChangeAvatarCommand { get; }

    // Имя пользователя в профиле.
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

    // Текст даты регистрации («участник с ...»).
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

    // Форматирует дату регистрации (хранится как Unix-время в секундах) в читаемую
    // строку. Если даты нет или она некорректна — возвращает «—».
    private static string FormatRegisterDateForDisplay(int? registerDateUnix)
    {
        if (registerDateUnix is null or < 1)
        {
            return "—";
        }

        var dto = DateTimeOffset.FromUnixTimeSeconds(registerDateUnix.Value);

        try
        {
            return dto.ToLocalTime().ToString("dd MMMM yyyy", CultureInfo.CurrentCulture);
        }
        catch
        {
            return "—";
        }
    }

    // Загружает данные профиля при открытии экрана. Определяет, чей профиль показывать,
    // тянет из Firebase профиль, аватар, имя и количество полученного уважения, затем
    // загружает раздел сыгранных партий. Если профиля нет или произошла ошибка —
    // показывает значения по умолчанию. Обновление интерфейса — в главном потоке.
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
                await _firebaseSync.EnsureUserAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            var profile = IsOwnProfile && _appService.CurrentUserId is not null
                ? await _firebaseSync.GetUserAsync(cancellationToken).ConfigureAwait(false)
                : await _firebaseSync.GetUserByUidAsync(profileUid, cancellationToken).ConfigureAwait(false);

            var src = await UserAvatarPreview
                .LoadAsync(profileUid, cancellationToken, preferLocalStore: true)
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

    // Загружает раздел сыгранных партий профиля (до 25 последних) и наполняет список.
    // При ошибке оставляет список пустым. Обновление — в главном потоке.
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

    // Сообщает интерфейсу, что флаги блока сыгранных партий могли измениться.
    private void NotifyPlayedGamesUi()
    {
        OnPropertyChanged(nameof(HasPlayedGames));
        OnPropertyChanged(nameof(ShowPlayedGamesEmpty));
        OnPropertyChanged(nameof(ShowSeeAllPlayedGames));
    }

    // Открывает полный список сыгранных партий этого профиля (переход на PlayedGamesPage).
    private async Task OpenFullPlayedGamesAsync()
    {
        var uid = ActiveProfileUid;
        if (Shell.Current is null || string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        await Shell.Current.GoToAsync($"PlayedGamesPage?UserId={Uri.EscapeDataString(uid)}");
    }

    // Смена аватара (только в своём профиле). Проверяет, что пользователь вошёл,
    // спрашивает источник (галерея/камера), получает фото, сохраняет его локально и
    // показывает. Ошибки выводит попапом, поток фото в конце закрывается.
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
            var fullPickPath = await UserAvatarLocalStore
                .SaveLocalAvatarAsync(uid, stream, CancellationToken.None)
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
