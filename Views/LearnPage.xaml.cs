using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class LearnPage : ContentPage
{
    public LearnPage(LearnPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
