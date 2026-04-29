using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class PuzzlesPage : ContentPage
{
    public PuzzlesPage(PuzzlesPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
