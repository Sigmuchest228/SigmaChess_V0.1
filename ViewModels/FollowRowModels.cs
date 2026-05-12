using System.Windows.Input;

namespace SigmaChess.ViewModels;

public sealed class FollowRowViewModel : ViewModelBase
{
    private ImageSource? _avatar;

    public FollowRowViewModel(string uid, string displayName, string rankTitle, string puzzlesLine, Func<Task> openProfile)
    {
        Uid = uid;
        DisplayName = displayName;
        RankTitle = rankTitle;
        PuzzlesLine = puzzlesLine;
        TapCommand = new Command(async () => await openProfile());
    }

    public string Uid { get; }

    public string DisplayName { get; }

    public string RankTitle { get; }

    public string PuzzlesLine { get; }

    public ImageSource? Avatar
    {
        get => _avatar;
        set
        {
            if (ReferenceEquals(_avatar, value))
            {
                return;
            }

            _avatar = value;
            OnPropertyChanged();
        }
    }

    public ICommand TapCommand { get; }
}

public sealed class SearchUserRowViewModel : ViewModelBase
{
    private ImageSource? _avatar;

    public SearchUserRowViewModel(string uid, string displayName, string rankTitle, string puzzlesLine, bool showFollowButton)
    {
        Uid = uid;
        DisplayName = displayName;
        RankTitle = rankTitle;
        PuzzlesLine = puzzlesLine;
        ShowFollowButton = showFollowButton;
    }

    public string Uid { get; }

    public string DisplayName { get; }

    public string RankTitle { get; }

    public string PuzzlesLine { get; }

    public bool ShowFollowButton { get; }

    public ImageSource? Avatar
    {
        get => _avatar;
        set
        {
            if (ReferenceEquals(_avatar, value))
            {
                return;
            }

            _avatar = value;
            OnPropertyChanged();
        }
    }
}
