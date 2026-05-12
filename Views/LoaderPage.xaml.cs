using SigmaChess;

namespace SigmaChess.Views;

/// <summary>Первый экран сессии: короткая задержка, затем переход на Main или Auth.</summary>
public partial class LoaderPage : ContentPage
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2.5);
    private bool _started;

    public LoaderPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started)
        {
            return;
        }

        _started = true;

        try
        {
            var skipDelay = Shell.Current is AppShellAuth && App.SkipLoaderDelayOnceForAuthShell;
            if (skipDelay)
            {
                App.SkipLoaderDelayOnceForAuthShell = false;
                await MainThread.InvokeOnMainThreadAsync(NavigateNextAsync).ConfigureAwait(false);
                return;
            }

            await Task.Delay(Delay).ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(NavigateNextAsync).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Loader: {ex}");
        }
    }

    private static async Task NavigateNextAsync()
    {
        if (Shell.Current is null)
        {
            return;
        }

        var target = Shell.Current is AppShellAuth ? "//MainPage" : "//AuthPage";
        try
        {
            await Shell.Current.GoToAsync(target);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Loader navigate: {ex}");
        }
    }
}
