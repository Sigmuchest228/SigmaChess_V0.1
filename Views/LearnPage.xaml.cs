using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Страница «Learn» (заглушка). VM приходит из DI.</summary>
public partial class LearnPage : ContentPage
{
    public LearnPage(LearnPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
