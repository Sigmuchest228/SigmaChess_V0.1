using CommunityToolkit.Maui.Views;

namespace SigmaChess.Views;

/// <summary>
/// Универсальный «да/нет»-попап на CommunityToolkit. Заменяет нам системный
/// <c>DisplayAlert</c> единым стилем по всему приложению.
/// <para>
/// Свойство <see cref="IsConfirmed"/> намеренно НЕ называется <c>Result</c>,
/// потому что у базового <c>Popup.Result</c> есть собственное значение, и компилятор
/// предупредил бы о теневом перекрытии.
/// </para>
/// </summary>
public partial class ConfirmPopup : Popup
{
    /// <summary>true — пользователь нажал «primary» (подтвердил), false — закрыл/нажал secondary.</summary>
    public bool IsConfirmed { get; private set; }

    public ConfirmPopup(string title, string message, string primaryText, string? secondaryText = null)
    {
        InitializeComponent();

        TitleLabel.Text = title;
        MessageLabel.Text = message;
        PrimaryButton.Text = primaryText;

        // Если secondary не задан — прячем вторую кнопку, чтобы попап работал как «OK».
        if (string.IsNullOrEmpty(secondaryText))
        {
            SecondaryButton.IsVisible = false;
        }
        else
        {
            SecondaryButton.Text = secondaryText;
        }
    }

    /// <summary>
    /// Открыть попап и вернуть выбор пользователя
    /// (true = primary, false = secondary/закрытие тапом мимо).
    /// </summary>
    public static async Task<bool> ShowAsync(string title, string message, string primaryText, string? secondaryText = null)
    {
        // Берём активную страницу из Shell — на ней показываем попап.
        var page = Shell.Current?.CurrentPage;
        if (page is null)
        {
            return false;
        }

        var popup = new ConfirmPopup(title, message, primaryText, secondaryText);
        await page.ShowPopupAsync(popup);
        return popup.IsConfirmed;
    }

    private async void OnPrimaryClicked(object? sender, EventArgs e)
    {
        IsConfirmed = true;
        await CloseAsync();
    }

    private async void OnSecondaryClicked(object? sender, EventArgs e)
    {
        IsConfirmed = false;
        await CloseAsync();
    }
}
