using System.Collections.ObjectModel;
using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

// ViewModel экрана списка сыгранных партий (страница PlayedGamesPage). Показывает
// завершённые партии пользователя (свои или чужие — по id профиля из навигации) и даёт
// открыть просмотр любой из них (реплей). Данные грузит из Firebase.
public class PlayedGamesPageViewModel : ViewModelBase
{
    private readonly AppService _appService;
    private readonly FirebaseSyncRepository _firebaseSync;

    // Чей профиль показываем (id). Если null — берём текущего пользователя.
    private string? _profileUid;

    // Конструктор без параметров (для интерфейса): берёт сервисы из общего AppService.
    public PlayedGamesPageViewModel()
        : this(AppService.GetInstance(), AppService.GetInstance().FirebaseSync)
    {
    }

    // Основной конструктор: сохраняет сервисы, создаёт коллекцию партий и команды
    // «назад» и «открыть реплей партии».
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

    // Список партий и флаги/тексты для интерфейса (есть ли партии, пусто ли, сообщение
    // о пустоте, заголовок).
    public ObservableCollection<PlayedGameRowViewModel> Games { get; }

    public bool HasGames => Games.Count > 0;

    public bool IsEmpty => Games.Count == 0;

    public string EmptyMessage => "No completed games yet.";

    public string PageTitle => "Played games";

    // Команды: назад и открыть реплей выбранной партии.
    public ICommand GoBackCommand { get; }

    public ICommand OpenReplayCommand { get; }

    // Принимает параметры навигации: если передан UserId — показываем партии этого
    // пользователя (иначе текущего).
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

    // Загружает сыгранные партии нужного пользователя из Firebase и наполняет список
    // строками. Если пользователь не определён или произошла ошибка — список очищается.
    // Обновление коллекции — в главном потоке.
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
