using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Страница шахматных задач.</summary>
public partial class PuzzlesPage : ContentPage
{
    private readonly PuzzlesPageViewModel _viewModel;

    public PuzzlesPage(PuzzlesPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
