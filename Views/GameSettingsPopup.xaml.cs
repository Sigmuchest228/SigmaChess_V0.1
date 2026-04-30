using CommunityToolkit.Maui.Views;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>
/// Попап с настройками партии (auto-flip / highlight last move / auto-queen)
/// и кнопкой «New game». Все Switch'и биндятся прямо на <see cref="GameViewModel"/>,
/// поэтому код-бихайнд минимальный — только обработка кликов.
/// </summary>
public partial class GameSettingsPopup : Popup
{
    private readonly GameViewModel _viewModel;

    public GameSettingsPopup(GameViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        // BindingContext = VM, поэтому в XAML биндинги вида {Binding AutoFlipEnabled}
        // уезжают напрямую к свойствам GameViewModel.
        BindingContext = viewModel;
    }

    private async void OnNewGameClicked(object? sender, EventArgs e)
    {
        // Сначала закрываем попап настроек, чтобы диалог подтверждения «Новая партия?»
        // не появился поверх него (иначе пользователь увидит два модалки одновременно).
        await CloseAsync();

        if (_viewModel.NewGameCommand.CanExecute(null))
        {
            _viewModel.NewGameCommand.Execute(null);
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();
}
