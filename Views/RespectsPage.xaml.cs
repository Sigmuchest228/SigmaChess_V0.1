using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class RespectsPage : ContentPage
{
    private readonly RespectsPageViewModel _viewModel;

    public RespectsPage()
    {
        InitializeComponent();
        _viewModel = new RespectsPageViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
