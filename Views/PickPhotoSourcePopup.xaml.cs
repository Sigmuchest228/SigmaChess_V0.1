using CommunityToolkit.Maui.Views;
using SigmaChess.Services;

namespace SigmaChess.Views;

public partial class PickPhotoSourcePopup : Popup
{
    public PickPhotoSource Choice { get; private set; } = PickPhotoSource.Cancel;

    public PickPhotoSourcePopup()
    {
        InitializeComponent();
    }

    public static async Task<PickPhotoSource> ShowAsync()
    {
        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return PickPhotoSource.Cancel;
        }

        var popup = new PickPhotoSourcePopup();
        await page.ShowPopupAsync(popup);
        return popup.Choice;
    }

    private async void OnGalleryClicked(object? sender, EventArgs e)
    {
        Choice = PickPhotoSource.Gallery;
        await CloseAsync();
    }

    private async void OnCameraClicked(object? sender, EventArgs e)
    {
        Choice = PickPhotoSource.Camera;
        await CloseAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        Choice = PickPhotoSource.Cancel;
        await CloseAsync();
    }
}
