using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Страница «Watch» (заглушка). VM приходит из DI.</summary>
public partial class WatchPage : ContentPage
{
    public WatchPage(WatchPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
