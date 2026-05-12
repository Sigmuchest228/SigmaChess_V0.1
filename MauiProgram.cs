using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        builder.Services.AddSingleton<FirebaseSyncRepository>();
        builder.Services.AddSingleton<IPhotoSourcePicker, PhotoSourcePicker>();
        builder.Services.AddSingleton<IUserAppearanceService, UserAppearanceService>();
        builder.Services.AddSingleton<LogoutSessionService>();
        builder.Services.AddSingleton<BoardLayoutService>();
        builder.Services.AddSingleton<global::SigmaChess.Engine.GameController>();

        // ---------- ViewModels ----------
        // Transient: легковесные VM-ы — на каждое открытие страницы получает свежий экземпляр.
        // Singleton GameViewModel: ровно по той же причине, что и GameController — нам нужно,
        // чтобы настройки доски и состояние партии сохранялись между навигациями.
        builder.Services.AddTransient<AuthViewModel>();
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddSingleton<GameViewModel>();
        builder.Services.AddSingleton<BottomNavigationCoordinator>();
        builder.Services.AddTransient<PuzzlesPageViewModel>();
        builder.Services.AddTransient<FollowsPageViewModel>();
        builder.Services.AddTransient<PlayedGamesPageViewModel>();
        builder.Services.AddTransient<GameReplayViewModel>();
        builder.Services.AddTransient<PuzzleSolveViewModel>();
        builder.Services.AddTransient<UserProfileViewModel>();
        // Singleton: коллекция пользовательских обоев и пресетов должна сохраняться между входами на страницу.
        builder.Services.AddSingleton<ProfileWallpaperSettingsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ---------- Страницы ----------
        // Transient: Shell и RegisterRoute создают страницы через DI по шаблонам и при push-маршрутах.
        builder.Services.AddTransient<LoaderPage>();
        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<PuzzlesPage>();
        builder.Services.AddTransient<FollowsPage>();
        builder.Services.AddTransient<PlayedGamesPage>();
        builder.Services.AddTransient<GameReplayPage>();
        builder.Services.AddTransient<PuzzleSolvePage>();
        builder.Services.AddTransient<GamePage>();
        builder.Services.AddTransient<UserProfilePage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ProfileWallpaperSettingsPage>();

        var app = builder.Build();
        App.Services = app.Services;
        return app;
    }
}
