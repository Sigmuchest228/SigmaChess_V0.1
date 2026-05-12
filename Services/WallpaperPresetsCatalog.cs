using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace SigmaChess.Services;

/// <summary>Пресеты обоев из манифеста Raw (<c>wallpaper_presets_manifest.json</c>).</summary>
public static class WallpaperPresetsCatalog
{
    private static readonly SemaphoreSlim LoadGate = new(1, 1);

    private static volatile bool _loaded;

    /// <summary>Имена файлов пресетов в бандле; наполняется один раз из JSON на UI-потоке.</summary>
    public static ObservableCollection<string> PresetFileNames { get; } = new();

    /// <summary>Logical name для <see cref="Microsoft.Maui.Storage.FileSystem.OpenAppPackageFileAsync"/>.</summary>
    public static string LogicalName(string fileName) => $"WallpapersPresets/{fileName}";

    /// <summary>Читает манифест и заполняет <see cref="PresetFileNames"/> (повторные вызовы — no-op).</summary>
    public static async Task EnsurePresetFileNamesLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        await LoadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded)
            {
                return;
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync("wallpaper_presets_manifest.json")
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var list = JsonConvert.DeserializeObject<List<string>>(json) ?? [];

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PresetFileNames.Clear();
                foreach (var name in list)
                {
                    PresetFileNames.Add(name);
                }
            }).WaitAsync(cancellationToken).ConfigureAwait(false);

            _loaded = true;
        }
        finally
        {
            LoadGate.Release();
        }
    }
}
