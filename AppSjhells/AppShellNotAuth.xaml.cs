namespace SigmaChess;

/// <summary>
/// Гостевой Shell. Содержит только публичные разделы (главная и т. д.); закрытые
/// разделы перехватываются <see cref="ViewModels.MainPageViewModel"/> и предлагают
/// пользователю войти через ConfirmPopup.
/// </summary>
public partial class AppShellNotAuth : Shell
{
    public AppShellNotAuth()
    {
        InitializeComponent();
    }
}
