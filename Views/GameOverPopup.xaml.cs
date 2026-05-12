using CommunityToolkit.Maui.Views;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Попап итога партии: новая игра (с выбором времени) или выход на главную.</summary>
public partial class GameOverPopup : Popup
{
    private readonly GameViewModel _viewModel;

    public GameOverPopup(GameViewModel viewModel, string resultMessage)
    {
        InitializeComponent();
        _viewModel = viewModel;
        ResultLabel.Text = resultMessage;
    }

    private async void OnNewGameClicked(object? sender, EventArgs e)
    {
        await CloseAsync();
        await _viewModel.StartNewGameWithTimeSetupAsync();
    }

    private async void OnHomeClicked(object? sender, EventArgs e)
    {
        await CloseAsync();
        await _viewModel.NavigateToMainPageAsync();
    }
}
