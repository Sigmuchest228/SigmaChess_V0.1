using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class Register_Login : ContentPage
{
	public Register_Login()
	{
		InitializeComponent();
		BindingContext = new RegisterViewModel();
	}
}