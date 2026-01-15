using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class Main : ContentPage
{
	public Main()
	{
		InitializeComponent();
		BindingContext = new MainViewModel();
	}
}