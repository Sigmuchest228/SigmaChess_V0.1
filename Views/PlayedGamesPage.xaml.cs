using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class PlayedGamesPage : ContentPage, IQueryAttributable
{
    private readonly PlayedGamesPageViewModel _viewModel;

    public PlayedGamesPage()
    {
        InitializeComponent();
        _viewModel = new PlayedGamesPageViewModel();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.ApplyNavigationQuery(query);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
