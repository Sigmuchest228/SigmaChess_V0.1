using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class FriendsPage : ContentPage
{
    public FriendsPage(FriendsPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
