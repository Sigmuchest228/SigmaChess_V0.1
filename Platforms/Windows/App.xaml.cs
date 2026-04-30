using Microsoft.UI.Xaml;

namespace SigmaChess.WinUI
{
    // Стандартная стартовая обвязка MAUI для Windows (WinUI 3). Шаблон создаёт этот класс
    // как точку входа WinUI-приложения; нам остаётся только связать его с общим MauiProgram,
    // никакой собственной логики Windows-специфики мы здесь не пишем.
    public partial class App : MauiWinUIApplication
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
