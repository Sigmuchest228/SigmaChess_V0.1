using SigmaChess.ViewModels;

namespace SigmaChess;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainPageViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainPageViewModel vm)
        {
            vm.RefreshAuthState();
            await Task.WhenAll(vm.RefreshRespectsSummaryAsync(), vm.SyncFirebaseProfileIfNeededAsync());
        }
    }
}
