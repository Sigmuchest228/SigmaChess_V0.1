using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Страница «Menu» (заглушка). VM приходит из DI.</summary>
public partial class MenuPage : ContentPage
{
    public MenuPage(MenuPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
