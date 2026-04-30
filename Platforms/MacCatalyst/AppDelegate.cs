using Foundation;

namespace SigmaChess
{
    // Стандартная стартовая обвязка MAUI для Mac Catalyst (тот же UIKit, что и iOS).
    // Полностью аналогична iOS-версии, разделена для совместимости MAUI.
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
