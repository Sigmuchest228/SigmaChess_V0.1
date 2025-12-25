using SigmaChess.ViewModels;
namespace SigmaChess.Views;

public partial class Login_Register : ContentPage
{
	public Login_Register()
	{
		InitializeComponent();
		BindingContext = new LoginViewModel();

    }
}