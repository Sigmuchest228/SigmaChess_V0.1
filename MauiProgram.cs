using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SigmaChess.Engine;
using SigmaChess.Services;
using SigmaChess.ViewModels;
using SigmaChess.Views;

namespace SigmaChess
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
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
            builder.Services.AddSingleton<AppService>();
            builder.Services.AddSingleton<BoardLayoutService>();
            builder.Services.AddTransient<global::SigmaChess.Engine.GameController>();

            builder.Services.AddTransient<AuthViewModel>();
            builder.Services.AddTransient<MainPageViewModel>();
            builder.Services.AddTransient<GameViewModel>();
            builder.Services.AddTransient<PuzzlesPageViewModel>();
            builder.Services.AddTransient<FriendsPageViewModel>();
            builder.Services.AddTransient<WatchPageViewModel>();
            builder.Services.AddTransient<LearnPageViewModel>();
            builder.Services.AddTransient<MenuPageViewModel>();

            builder.Services.AddTransient<AuthPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<PuzzlesPage>();
            builder.Services.AddTransient<FriendsPage>();
            builder.Services.AddTransient<WatchPage>();
            builder.Services.AddTransient<LearnPage>();
            builder.Services.AddTransient<MenuPage>();
            builder.Services.AddTransient<GamePage>();
            builder.Services.AddSingleton<AppShell>();

            return builder.Build();
        }
    }
}
