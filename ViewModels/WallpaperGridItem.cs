using Microsoft.Maui.Controls;

namespace SigmaChess.ViewModels;

public enum WallpaperGridKind
{
    Preset,
    UserPhoto,
    Gradient,
    AddTile,
}

/// <summary>Элемент сетки пресетов, градиентов и пользовательских фото на странице обоев.</summary>
public sealed class WallpaperGridItem
{
    public WallpaperGridKind Kind { get; init; }

    /// <summary>Имя файла пресета в бандле (без каталога).</summary>
    public string? PresetFileName { get; init; }

    /// <summary>Идентификатор темы градиента (classic, aurora, …).</summary>
    public string? GradientId { get; init; }

    /// <summary>Превью-плитка для вида <see cref="WallpaperGridKind.Gradient"/>.</summary>
    public Brush? TilePreviewBrush { get; init; }

    /// <summary>Локальный кэш пользовательского снимка.</summary>
    public string? LocalCachePath { get; init; }

    /// <summary>URL из сохранённого профиля (если уже был задан ранее); новые локальные снимки в облако не выгружаются.</summary>
    public string? RemoteDownloadUrl { get; set; }

    /// <summary>Миниатюра для пресета/фото; для градиента не задаётся.</summary>
    public ImageSource? Thumb { get; init; }
}
