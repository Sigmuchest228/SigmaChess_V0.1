using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class AuthPage : ContentPage
{
    public AuthPage()
    {
        InitializeComponent();
        BindingContext = new AuthViewModel();
    }
}
