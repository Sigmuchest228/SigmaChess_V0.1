using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class GamePage : ContentPage
{
    public GamePage()
    {
        InitializeComponent();
        BindingContext = new GameViewModel();
    }
}
