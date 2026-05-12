using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Отдельный шаблон для градиентных плиток и для превью с <see cref="ImageSource"/>.</summary>
public sealed class WallpaperTileTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GradientTemplate { get; set; }

    public DataTemplate? ImageTileTemplate { get; set; }

    public DataTemplate? AddTileTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
        item is WallpaperGridItem wi
            ? wi.Kind switch
            {
                WallpaperGridKind.Gradient => GradientTemplate!,
                WallpaperGridKind.AddTile => AddTileTemplate!,
                _ => ImageTileTemplate!,
            }
            : ImageTileTemplate!;
}
