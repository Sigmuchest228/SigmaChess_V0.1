using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class ProfileWallpaperSettingsPage : ContentPage
{
    private readonly ProfileWallpaperSettingsViewModel _viewModel;

    public ProfileWallpaperSettingsPage(ProfileWallpaperSettingsViewModel viewModel)
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
