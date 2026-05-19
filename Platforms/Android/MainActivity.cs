using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace SigmaChess
{

    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            ApplyDarkStatusBar();
        }

        private void ApplyDarkStatusBar()
        {
            var window = Window;
            if (window is null)
            {
                return;
            }

            window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#12161F"));

            var decor = window.DecorView;
            if (decor is null)
            {
                return;
            }

            var controller = new WindowInsetsControllerCompat(window, decor);
            controller.AppearanceLightStatusBars = false;
        }
    }
}
