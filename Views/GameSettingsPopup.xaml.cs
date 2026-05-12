using CommunityToolkit.Maui.Views;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>
/// Попап с настройками партии (auto-flip / highlight last move / auto-queen).
/// Все Switch'и биндятся прямо на <see cref="GameViewModel"/>.
/// </summary>
public partial class GameSettingsPopup : Popup
{
    public GameSettingsPopup(GameViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();
}
