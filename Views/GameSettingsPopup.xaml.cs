using CommunityToolkit.Maui.Views;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class GameSettingsPopup : Popup
{
    public GameSettingsPopup(GameViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();
}
