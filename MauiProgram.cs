using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SigmaChess.Services;

namespace SigmaChess;

/// <summary>
/// Единая точка инициализации MAUI: CommunityToolkit, шрифты, логгер.
/// Сервисы и VM для UI — через <see cref="AppService.GetInstance"/> (без DI контейнера для страниц).
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppService.GetInstance().Init();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // CommunityToolkit нужен ради Popup (ConfirmPopup/PromotionPopup/GameSettingsPopup).
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
