using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Devices;
using SigmaChess.ViewModels;
using SigmaChess.Views;
using SigmaChess;

namespace SigmaChess.Services;

#region BoardLayoutService

/// <summary>
/// Считает сторону доски в DIP под текущий экран. Делегирован, потому что эти константы
/// «согласованы» с layout'ом GamePage (отступ страницы, ширина полосы координат,
/// высота, занятая хедером/статусом/подсказками).
/// </summary>
public class BoardLayoutService
{
    // Согласовано с GameViewModel: единый Grid доски + полоса координат 28 px;
    // отступ страницы по ширине 12+12. По высоте резерв под хедер, статус,
    // полосу файлов снизу и нижний слот действий.
    private const double PageHorizontalPadding = 24;
    private const double CoordStripWidth = 28;
    private const double VerticalReserve = 280;

    /// <summary>Резерв под колонку записи ходов справа и нижнюю панель на странице партии.</summary>
    private const double GamePageMoveListColumn = 140;

    /// <summary>
    /// Режим «за столом»: история ходов на всю ширину (не в узких боковых колонках) —
    /// оставляем небольшой запас только к полям страницы и полосам координат.
    /// </summary>
    private const double FaceToFaceHorizontalReserve = 32;

    /// <summary>Вертикальный резерв чуть ниже, т.к. списки ходов ограничены по высоте и не забирают всю полосу.</summary>
    private const double FaceToFaceVerticalReserve = 238;

    private const double GamePageBottomPanelExtra = 76;

    /// <summary>
    /// Возвращает максимальную сторону доски, при которой она помещается в экран
    /// и по ширине, и по высоте, ограниченную диапазоном [260, 640].
    /// </summary>
    public double CalculateBoardExtent(DisplayInfo info)
    {
        // info.Width/Height в пикселях, делим на плотность чтобы получить DIP.
        // Защита от нулевой плотности (бывает в эмуляторах/тестах).
        var density = info.Density <= 0 ? 1 : info.Density;
        var width = info.Width / density;
        var height = info.Height / density;
        var maxSquareFromWidth = width - PageHorizontalPadding - CoordStripWidth;
        var maxSquareFromHeight = height - VerticalReserve;
        // Доска квадратная — берём минимум из двух осей.
        var side = Math.Min(maxSquareFromWidth, maxSquareFromHeight);
        // Снизу — чтобы фигуры не превратились в точки, сверху — чтобы на больших мониторах
        // доска не занимала пол-экрана.
        return Math.Clamp(side, 260, 640);
    }

    /// <summary>Как <see cref="CalculateBoardExtent"/>, но с учётом колонки истории ходов и нижней панели GamePage.</summary>
    public double CalculateBoardExtentForGamePage(DisplayInfo info, bool faceToFaceLayout = false)
    {
        var density = info.Density <= 0 ? 1 : info.Density;
        var width = info.Width / density;
        var height = info.Height / density;
        var widthReserve = faceToFaceLayout ? FaceToFaceHorizontalReserve : GamePageMoveListColumn;
        var maxSquareFromWidth = width - PageHorizontalPadding - CoordStripWidth - widthReserve;
        var verticalReserve = faceToFaceLayout ? FaceToFaceVerticalReserve : VerticalReserve;
        var maxSquareFromHeight = height - verticalReserve - GamePageBottomPanelExtra;
        var side = Math.Min(maxSquareFromWidth, maxSquareFromHeight);
        var minSide = faceToFaceLayout ? 230 : 220;
        return Math.Clamp(side, minSide, 640);
    }
}

#endregion

#region BottomNavigationCoordinator

public enum BottomNavSection
{
    None,
    Home,
    Respect,
}

/// <summary>Общая нижняя навигация: Home учитывает партию на GamePage (тот же попап, что «назад»).</summary>
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

    /// <summary>Синхронизация подсветки вкладки с текущей страницей Shell (в т.ч. после push/pop).</summary>
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

    /// <summary>Разделы нижней панели на гостевом Shell недоступны без входа.</summary>
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
