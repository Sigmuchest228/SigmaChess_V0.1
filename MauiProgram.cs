using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SigmaChess.Engine;
using SigmaChess.Services;
using SigmaChess.ViewModels;
using SigmaChess.Views;

namespace SigmaChess;

/// <summary>
/// Единая точка инициализации MAUI: подключаем CommunityToolkit, шрифты, логгер и
/// регистрируем все сервисы/VM/страницы в DI-контейнере. Без этого DI не сможет
/// разрулить параметры конструкторов страниц (например, <c>GamePage(GameViewModel)</c>).
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
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
        // ---------- Сервисы ----------
        // Singleton: сервисы и контроллер игры живут в единственном экземпляре на всё приложение.
        // У AppService — ленивая инициализация Firebase (см. EnsureInitialized).
        // BoardLayoutService — чистая утилита без состояния.
        // GameController — хранит партию, поэтому при возврате на GamePage партия НЕ сбрасывается.
        builder.Services.AddSingleton<AppService>();
        builder.Services.AddSingleton<BoardLayoutService>();
        builder.Services.AddSingleton<GameController>();

        // ---------- ViewModels ----------
        // Transient: легковесные VM-ы — на каждое открытие страницы получает свежий экземпляр.
        // Singleton GameViewModel: ровно по той же причине, что и GameController — нам нужно,
        // чтобы настройки доски и состояние партии сохранялись между навигациями.
        builder.Services.AddTransient<AuthViewModel>();
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddSingleton<GameViewModel>();
        builder.Services.AddTransient<PuzzlesPageViewModel>();
        builder.Services.AddTransient<FriendsPageViewModel>();
        builder.Services.AddTransient<WatchPageViewModel>();
        builder.Services.AddTransient<LearnPageViewModel>();
        builder.Services.AddTransient<MenuPageViewModel>();

        // ---------- Страницы ----------
        // Все страницы регистрируем как Transient — Shell сам управляет их жизненным циклом
        // (создаёт при заходе в таб, освобождает при выгрузке таб-стека).
        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<PuzzlesPage>();
        builder.Services.AddTransient<FriendsPage>();
        builder.Services.AddTransient<WatchPage>();
        builder.Services.AddTransient<LearnPage>();
        builder.Services.AddTransient<MenuPage>();
        builder.Services.AddTransient<GamePage>();
        builder.Services.AddTransient<BotsGamePage>();

        return builder.Build();
    }
}
