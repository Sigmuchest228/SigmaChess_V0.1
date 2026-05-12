using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class PlayedGamesPage : ContentPage, IQueryAttributable
{
    private readonly PlayedGamesPageViewModel _viewModel;

    public PlayedGamesPage(PlayedGamesPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
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
