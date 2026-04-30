using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>
/// Страница входа/регистрации. Вся логика лежит в <see cref="AuthViewModel"/>;
/// сама страница только связывает XAML с ViewModel и делегирует ему всё через
/// биндинги. Параметр конструктора берётся из DI (см. MauiProgram).
/// </summary>
public partial class AuthPage : ContentPage
{
    public AuthPage(AuthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
