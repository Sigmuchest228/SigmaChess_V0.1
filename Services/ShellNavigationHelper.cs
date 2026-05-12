namespace SigmaChess.Services;

/// <summary>Сначала относительный «назад», при ошибке — абсолютный переход на главную вкладку.</summary>
public static class ShellNavigationHelper
{
    public static Task PopOrMainAsync(CancellationToken cancellationToken = default) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shell pop: {ex}");
                await Shell.Current.GoToAsync("//MainPage");
            }
        }).WaitAsync(cancellationToken);
}
