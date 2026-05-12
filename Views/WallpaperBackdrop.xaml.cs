using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SigmaChess.Services;

namespace SigmaChess.Views;

/// <summary>Подложка под стеклянный UI: сплошной фон, выбранные обои и лёгкая вуаль.</summary>
public partial class WallpaperBackdrop : Grid
{
    private static readonly Color DefaultFallbackBg = Color.FromArgb("#12161F");

    private IUserAppearanceService? _appearance;
    private PropertyChangedEventHandler? _handler;

    public WallpaperBackdrop()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _appearance = Handler?.MauiContext?.Services.GetService<IUserAppearanceService>();
        if (_appearance is null)
        {
            return;
        }

        _handler = (_, args) =>
        {
            if (args.PropertyName == nameof(IUserAppearanceService.WallpaperBackground))
            {
                MainThread.BeginInvokeOnMainThread(ApplyImageSource);
            }
            else if (args.PropertyName == nameof(IUserAppearanceService.BackdropBaseBrush))
            {
                MainThread.BeginInvokeOnMainThread(ApplyBaseBackdrop);
            }
        };

        _appearance.PropertyChanged += _handler;
        ApplyBaseBackdrop();
        ApplyImageSource();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_appearance is not null && _handler is not null)
        {
            _appearance.PropertyChanged -= _handler;
        }

        _appearance = null;
        _handler = null;
    }

    private void ApplyBaseBackdrop()
    {
        if (_appearance is null)
        {
            return;
        }

        if (_appearance.BackdropBaseBrush is { } brush)
        {
            BaseBackdropBox.ClearValue(BoxView.BackgroundColorProperty);
            BaseBackdropBox.Background = brush;
        }
        else
        {
            BaseBackdropBox.ClearValue(BoxView.BackgroundProperty);
            BaseBackdropBox.BackgroundColor = DefaultFallbackBg;
        }
    }

    private void ApplyImageSource()
    {
        var src = _appearance?.WallpaperBackground;
        WallpaperImage.Source = src;
        WallpaperImage.IsVisible = src is not null;
    }
}
