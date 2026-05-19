using CommunityToolkit.Maui.Views;

namespace SigmaChess.Views;

public partial class ConfirmPopup : Popup
{

    public bool IsConfirmed { get; private set; }

    public ConfirmPopup(string title, string message, string primaryText, string? secondaryText = null)
    {
        InitializeComponent();

        TitleLabel.Text = title;
        MessageLabel.Text = message;
        PrimaryButton.Text = primaryText;

        if (string.IsNullOrEmpty(secondaryText))
        {
            SecondaryButton.IsVisible = false;
        }
        else
        {
            SecondaryButton.Text = secondaryText;
        }
    }

    public static async Task<bool> ShowAsync(string title, string message, string primaryText, string? secondaryText = null)
    {

        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;
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
