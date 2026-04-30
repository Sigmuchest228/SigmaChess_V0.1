using Microsoft.Maui.Devices;

namespace SigmaChess.Services;

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
}
