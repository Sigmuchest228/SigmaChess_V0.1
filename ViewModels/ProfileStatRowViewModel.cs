namespace SigmaChess.ViewModels;

/// <summary>Строка статистики для главной и профиля (Rank / Puzzles solved).</summary>
public sealed class ProfileStatRowViewModel : ViewModelBase
{
    public ProfileStatRowViewModel(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }
}
