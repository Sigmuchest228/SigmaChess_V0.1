namespace SigmaChess.ViewModels;

/// <summary>
/// Заглушка для раздела «Friends». Сейчас просто статические заголовки;
/// реальный функционал (список друзей, приглашения) появится позже.
/// </summary>
public class FriendsPageViewModel : ViewModelBase
{
    public string Title => "Friends";
    public string Subtitle => "No friends yet";
}
