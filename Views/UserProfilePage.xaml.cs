using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class UserProfilePage : ContentPage, IQueryAttributable
{
    private readonly UserProfileViewModel _viewModel;

    public UserProfilePage()
    {
        InitializeComponent();
        _viewModel = new UserProfileViewModel();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.ApplyNavigationQuery(query);

        _ = MainThread.InvokeOnMainThreadAsync(() => _viewModel.InitializeAsync());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
