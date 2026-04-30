using System;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace SigmaChess
{
    // Стандартная стартовая обвязка MAUI для Tizen (Samsung-устройства).
    // Шаблон создаёт класс Program, который запускает MauiApp — мы только цепляем
    // общий MauiProgram, кастомизаций нет.
    internal class Program : MauiApplication
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        static void Main(string[] args)
        {
            var app = new Program();
            app.Run(args);
        }
    }
}
