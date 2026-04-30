using Android.App;
using Android.Runtime;

namespace SigmaChess
{
    // Стандартный Android-Application класс, создаваемый шаблоном MAUI.
    // Здесь мы только пробрасываем CreateMauiApp в общий MauiProgram — никакой собственной
    // логики не добавляем.
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
