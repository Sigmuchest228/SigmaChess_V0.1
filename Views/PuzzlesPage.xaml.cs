using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Страница «Puzzles» (заглушка). VM приходит из DI.</summary>
public partial class PuzzlesPage : ContentPage
{
    public PuzzlesPage(PuzzlesPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
