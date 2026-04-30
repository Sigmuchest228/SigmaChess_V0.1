using Foundation;

namespace SigmaChess
{
    // Стандартный AppDelegate для iOS (MAUI-шаблон). Точку входа в приложение
    // обеспечивает сам шаблон — мы здесь ничего не меняем, только указываем,
    // какой MauiApp создать.
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
