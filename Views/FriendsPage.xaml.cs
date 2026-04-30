using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Страница «Friends» (заглушка). VM приходит из DI.</summary>
public partial class FriendsPage : ContentPage
{
    public FriendsPage(FriendsPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
