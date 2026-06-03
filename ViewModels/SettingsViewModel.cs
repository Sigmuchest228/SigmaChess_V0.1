using System.Diagnostics;
using System.Windows.Input;
using SigmaChess.Services;

namespace SigmaChess.ViewModels;

// ViewModel экрана настроек (страница SettingsPage). Сейчас содержит две команды:
// вернуться назад и выйти из аккаунта (полный логаут через AppService).
public class SettingsViewModel : ViewModelBase
{
    // Конструктор: создаёт команды «назад» и «выйти из аккаунта».
    public SettingsViewModel()
    {
        GoBackCommand = new Command(async () => await GoBackAsync());
        LogoutCommand = new Command(async () => await AppService.GetInstance().PerformFullLogoutAsync());
    }

    public ICommand GoBackCommand { get; }

    public ICommand LogoutCommand { get; }

    // Возврат на предыдущий экран. Если вернуться не удалось (нет стека) — уходит на
    // главный экран. Работает в главном потоке.
    private static async Task GoBackAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
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
                Debug.WriteLine($"Shell pop: {ex}");
                await Shell.Current.GoToAsync("//MainPage");
            }
        });
    }
}
