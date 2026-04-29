using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class WatchPage : ContentPage
{
    public WatchPage(WatchPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
