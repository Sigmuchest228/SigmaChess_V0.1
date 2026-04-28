using Microsoft.Maui.Devices;

namespace SigmaChess.Services;

public class BoardLayoutService
{
    public double CalculateBoardExtent(DisplayInfo info)
    {
        var density = info.Density <= 0 ? 1 : info.Density;
        var width = info.Width / density;
        var height = info.Height / density;
        var reservedSpace = 220;
        var side = Math.Min(width - 24, height - reservedSpace);
        return Math.Clamp(side, 260, 640);
    }
}
