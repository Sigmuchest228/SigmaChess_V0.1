using SigmaChess.ViewModels;

namespace SigmaChess;

/// <summary>
/// Главная страница приложения (общая для гостя и залогиненного).
/// Перекрывает <see cref="OnAppearing"/>, чтобы пересчитать <see cref="MainPageViewModel.IsGuest"/>
/// после смены Shell — иначе кнопки «Log in / Sign up» могли бы остаться видны
/// уже залогиненному пользователю.
/// </summary>
public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainPageViewModel vm)
        {
            vm.RefreshAuthState();
        }
    }
}
