using SigmaChess.ViewModels;

namespace SigmaChess;

/// <summary>
/// Главная страница приложения (общая для гостя и залогиненного).
/// Перекрывает <see cref="OnAppearing"/>, чтобы пересчитать <see cref="MainPageViewModel.IsGuest"/>
/// после смены корня навигации (логин/логаут).
/// </summary>
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
            await vm.SyncFirebaseProfileIfNeededAsync();
        }
    }
}
