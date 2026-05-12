using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace SigmaChess.Services;

/// <summary>Градиентные «пресеты» обоев: хранятся в <c>WallpaperPreset</c> как <c>__grad:…</c>.</summary>
public static class WallpaperGradientDefinitions
{
    /// <summary>Префикс значения <see cref="UserProfileRtdbDto.WallpaperPreset"/> для слоя-подложки без картинки.</summary>
    public const string PresetPrefix = "__grad:";

    /// <summary>Тот же тон, что <c>GlassPageFallback</c> — подложка без линейного градиента.</summary>
    public static Brush DefaultFallbackBrush { get; } = new SolidColorBrush(Color.FromArgb("#12161F"));

    public static bool IsGradientPreset(string? presetValue)
    {
        return !string.IsNullOrWhiteSpace(presetValue)
               && presetValue.Trim().StartsWith(PresetPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetGradientBrush(string presetValue, out Brush brush)
    {
        brush = DefaultFallbackBrush;
        var t = presetValue.Trim();
        if (!t.StartsWith(PresetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var id = t.Substring(PresetPrefix.Length).Trim();
        if (string.Equals(id, "classic", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(id))
        {
            brush = DefaultFallbackBrush;
            return true;
        }

        if (BrushMap.TryGetValue(id.ToLowerInvariant(), out var linear))
        {
            brush = linear;
            return true;
        }

        brush = DefaultFallbackBrush;
        return true;
    }

    public static Brush PreviewBrushForId(string gradientId)
    {
        var id = string.IsNullOrWhiteSpace(gradientId) ? "classic" : gradientId.Trim().ToLowerInvariant();
        return id switch
        {
            "classic" => DefaultFallbackBrush,
            _ when BrushMap.TryGetValue(id, out var lb) => lb,
            _ => DefaultFallbackBrush
        };
    }

    /// <summary>Порядок плиток в UI.</summary>
    public static IReadOnlyList<(string Id, string DisplayName)> Catalog { get; } =
    [
        ("classic", "Classic"),
        ("aurora", "Aurora"),
        ("sunset", "Sunset"),
        ("ocean", "Ocean"),
        ("ember", "Ember"),
        ("violet", "Violet"),
        ("mint", "Mint"),
    ];

    public static string SentinelPresetKey(string gradientId)
    {
        var id = string.IsNullOrWhiteSpace(gradientId) ? "classic" : gradientId.Trim();
        return $"{PresetPrefix}{id.ToLowerInvariant()}";
    }

    private static Dictionary<string, LinearGradientBrush> BrushMap { get; } = BuildBrushes();

    private static Dictionary<string, LinearGradientBrush> BuildBrushes()
    {
        LinearGradientBrush L(Color a, Color b, double x1 = 0, double y1 = 0, double x2 = 1, double y2 = 1)
        {
            return new LinearGradientBrush
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                GradientStops =
                [
                    new GradientStop(a, 0f),
                    new GradientStop(b, 1f),
                ]
            };
        }

        Color C(string hex) => Color.FromArgb(hex);

        return new Dictionary<string, LinearGradientBrush>(StringComparer.OrdinalIgnoreCase)
        {
            ["aurora"] = L(C("#1a0b2e"), C("#5c2580"), 0, 0, 1, 1),
            ["sunset"] = L(C("#2d1b69"), C("#f37a5c"), 0, 1, 1, 0),
            ["ocean"] = L(C("#0b2f4a"), C("#148c8e"), 0, 0, 1, 1),
            ["ember"] = L(C("#1f0a06"), C("#c23b22"), 0, 0, 1, 1),
            ["violet"] = L(C("#120822"), C("#7028ca"), 0.2, 0, 0.9, 1),
            ["mint"] = L(C("#0d1f1a"), C("#34a892"), 0, 1, 1, 0),
        };
    }
}
