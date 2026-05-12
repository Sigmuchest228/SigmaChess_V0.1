namespace SigmaChess.ViewModels;

public sealed class PuzzleRowViewModel : ViewModelBase
{
    public PuzzleRowViewModel(string puzzleId, string title, string subtitle, bool isSolved)
    {
        PuzzleId = puzzleId;
        Title = title;
        Subtitle = subtitle;
        IsSolved = isSolved;
    }

    public string PuzzleId { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public bool IsSolved { get; }
}
