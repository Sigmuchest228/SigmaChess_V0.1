using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SigmaChess
{
    // Стандартная стартовая обвязка MAUI для Android: это та самая Activity,
    // которую система запускает при тапе по иконке (MainLauncher = true).
    // Все ConfigurationChanges перечислены, чтобы Android при ротации/смене темы
    // не пересоздавал Activity — тогда MAUI сам корректно обновит UI.
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
