using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Devices;
using SigmaChess.ViewModels;
using SigmaChess.Views;
using SigmaChess;

namespace SigmaChess.Services;

#region BoardLayoutService

// Режим раскладки игровой страницы: колонка ходов сбоку, полоска ходов снизу
// (обычная игра) или режим «лицом к лицу» (двое за одним устройством).
public enum GamePageBoardExtentMode
{

    SideMoveColumn,

    CasualBottomMoveStrip,

    FaceToFace,
}

// Сервис, который считает размер (сторону) шахматной доски под конкретный экран.
// Берёт размеры экрана и плотность пикселей, вычитает поля, координатную полоску и
// зарезервированное место под другие элементы, и возвращает сторону доски, ограниченную
// разумным минимумом и максимумом. CalculateBoardExtentForGamePage учитывает выбранный
// режим раскладки игровой страницы.
public class BoardLayoutService
{

    private const double PageHorizontalPadding = 24;
    private const double CoordStripWidth = 28;
    private const double VerticalReserve = 280;

    private const double GamePageMoveListColumn = 140;

    private const double CasualBottomMoveHistoryReserve = 150;

    private const double FaceToFaceHorizontalReserve = 32;

    private const double FaceToFaceVerticalReserve = 238;

    private const double GamePageBottomPanelExtra = 76;

    public double CalculateBoardExtent(DisplayInfo info)
    {

        var density = info.Density <= 0 ? 1 : info.Density;
        var width = info.Width / density;
        var height = info.Height / density;
        var maxSquareFromWidth = width - PageHorizontalPadding - CoordStripWidth;
        var maxSquareFromHeight = height - VerticalReserve;

        var side = Math.Min(maxSquareFromWidth, maxSquareFromHeight);

        return Math.Clamp(side, 260, 640);
    }

    public double CalculateBoardExtentForGamePage(DisplayInfo info, GamePageBoardExtentMode mode)
    {
        var density = info.Density <= 0 ? 1 : info.Density;
        var width = info.Width / density;
        var height = info.Height / density;

        var widthReserve = mode switch
        {
            GamePageBoardExtentMode.SideMoveColumn => GamePageMoveListColumn,
            GamePageBoardExtentMode.CasualBottomMoveStrip => FaceToFaceHorizontalReserve,
            GamePageBoardExtentMode.FaceToFace => FaceToFaceHorizontalReserve,
        };

        var maxSquareFromWidth = width - PageHorizontalPadding - CoordStripWidth - widthReserve;

        var maxSquareFromHeight = mode switch
        {
            GamePageBoardExtentMode.FaceToFace => height - FaceToFaceVerticalReserve - GamePageBottomPanelExtra,
            GamePageBoardExtentMode.CasualBottomMoveStrip =>
                height - VerticalReserve - GamePageBottomPanelExtra - CasualBottomMoveHistoryReserve,
            GamePageBoardExtentMode.SideMoveColumn => height - VerticalReserve - GamePageBottomPanelExtra,
        };

        var side = Math.Min(maxSquareFromWidth, maxSquareFromHeight);
        var minSide = mode == GamePageBoardExtentMode.FaceToFace ? 230 : 220;
        return Math.Clamp(side, minSide, 640);
    }
}

#endregion

#region BottomNavigationCoordinator

// Раздел нижнего меню, который сейчас активен: ничего, «Домой» или «Уважение».
public enum BottomNavSection
{
    None,
    Home,
    Respect,
}

// Координатор нижней панели навигации. Хранит команды переходов (Домой, Уважение,
// Настройки) и отслеживает, какой раздел сейчас выбран, чтобы интерфейс подсвечивал
// активную кнопку. SyncFromShell определяет раздел по текущей странице оболочки.
// Переходы в защищённые разделы сначала проверяют, что пользователь вошёл в аккаунт;
// уход с игровой страницы домой требует подтверждения через GameViewModel.
public class BottomNavigationCoordinator : INotifyPropertyChanged
{
    private readonly GameViewModel _game;

    private BottomNavSection _section = BottomNavSection.Home;

    public BottomNavigationCoordinator(GameViewModel game)
    {
        _game = game;

        NavigateHomeCommand = new Command(async () => await NavigateHomeAsync());
        NavigateRespectsCommand = new Command(async () => await NavigateRespectsAsync());
        NavigateSettingsCommand = new Command(async () => await NavigateSettingsAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand NavigateHomeCommand { get; }

    public ICommand NavigateRespectsCommand { get; }

    public ICommand NavigateSettingsCommand { get; }

    public bool IsHomeSelected => _section == BottomNavSection.Home;

    public bool IsRespectSelected => _section == BottomNavSection.Respect;

    public void SyncFromShell()
    {
        void Apply()
        {
            var page = Shell.Current?.CurrentPage;
            BottomNavSection? next = page switch
            {
                MainPage or GamePage => BottomNavSection.Home,
                RespectsPage => BottomNavSection.Respect,
                PlayedGamesPage => BottomNavSection.None,
                SettingsPage => BottomNavSection.None,
                _ => null,
            };

            if (next is { } s)
            {
                ApplySection(s);
            }
        }

        if (MainThread.IsMainThread)
        {
            Apply();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(Apply);
        }
    }

    private void ApplySection(BottomNavSection s)
    {
        if (_section == s)
        {
            return;
        }

        _section = s;
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsRespectSelected));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async Task NavigateHomeAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            if (Shell.Current.CurrentPage is GamePage)
            {
                await _game.ConfirmLeaveGameAndGoHomeAsync().ConfigureAwait(false);
                return;
            }

            await Shell.Current.GoToAsync("//MainPage");
        }).ConfigureAwait(false);
    }

    private async Task NavigateRespectsAsync()
    {
        if (!await EnsureAuthenticatedForRestrictedAsync().ConfigureAwait(false))
        {
            return;
        }

        await ShellGoAsync("//RespectsPage").ConfigureAwait(false);
    }

    private async Task NavigateSettingsAsync()
    {
        if (!await EnsureAuthenticatedForRestrictedAsync().ConfigureAwait(false))
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            try
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings nav: {ex}");
                await Shell.Current.GoToAsync("//MainPage");
            }
        }).ConfigureAwait(false);
    }

    private static async Task ShellGoAsync(string route)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return;
            }

            await Shell.Current.GoToAsync(route);
        }).ConfigureAwait(false);
    }

    private static async Task<bool> EnsureAuthenticatedForRestrictedAsync()
    {
        if (Shell.Current is not AppShellNotAuth)
        {
            return true;
        }

        var goLogin = await ConfirmPopup.ShowAsync(
            "Account required",
            "Sign in or sign up to open this section.",
            "Log in",
            "Cancel");

        if (!goLogin || Shell.Current is null)
        {
            return false;
        }

        await Shell.Current.GoToAsync("//AuthPage");
        return false;
    }
}

#endregion
