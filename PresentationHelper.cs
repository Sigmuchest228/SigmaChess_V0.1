namespace SigmaChess;

/// <summary>Текущая страница Shell (или корень окна).</summary>
public static class PresentationHelper
{
    public static Page? CurrentPage =>
        Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    public static ContentPage? CurrentContentPage => CurrentPage as ContentPage;
}
