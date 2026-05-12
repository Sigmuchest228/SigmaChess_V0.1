using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class FollowsPage : ContentPage
{
    private readonly FollowsPageViewModel _viewModel;

    public FollowsPage(FollowsPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
