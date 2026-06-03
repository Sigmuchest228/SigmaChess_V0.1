using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SigmaChess.Services;

namespace SigmaChess;

// Точка сборки приложения .NET MAUI. Здесь приложение «собирается»: подключаются
// нужные библиотеки (Community Toolkit), регистрируются шрифты, в отладке — логирование.
// Этот класс вызывается платформенным кодом при старте и возвращает готовое приложение.
public static class MauiProgram
{
    // Создаёт и настраивает приложение: инициализирует общий AppService, подключает
    // MAUI и Community Toolkit, регистрирует шрифты (иконки и OpenSans), в DEBUG
    // добавляет отладочный лог, и возвращает собранное приложение.
    public static MauiApp CreateMauiApp()
    {
        AppService.GetInstance().Init();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()

            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
