using CommunityToolkit.Maui.Views;

namespace SigmaChess.Views;

/// <summary>Стекляный список опций вместо системного <see cref="Picker"/>.</summary>
public partial class GlassOptionListPopup : Popup
{
    public int? SelectedIndex { get; private set; }

    public GlassOptionListPopup(string title, IReadOnlyList<string> options)
    {
        InitializeComponent();
        TitleLabel.Text = title;

        for (var i = 0; i < options.Count; i++)
        {
            var idx = i;
            var text = options[i];
            var btn = new Button
            {
                Text = text,
                Padding = new Thickness(14, 12),
                BorderWidth = 0,
                CornerRadius = 14,
                FontSize = 15,
                LineBreakMode = LineBreakMode.WordWrap,
            };
            btn.SetDynamicResource(VisualElement.BackgroundProperty, "GlassNavButtonBrush");
            btn.SetDynamicResource(Button.TextColorProperty, "GlassTitleText");
            btn.Clicked += async (_, _) =>
            {
                SelectedIndex = idx;
                await CloseAsync();
            };
            OptionsStack.Children.Add(btn);
        }
    }

    /// <summary>Индекс выбранной строки или <c>null</c>, если отмена/фон.</summary>
    public static async Task<int?> ShowAsync(Page page, string title, IReadOnlyList<string> options,
        CancellationToken cancellationToken = default)
    {
        var popup = new GlassOptionListPopup(title, options);
        await page.ShowPopupAsync(popup).WaitAsync(cancellationToken);
        return popup.SelectedIndex;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        SelectedIndex = null;
        await CloseAsync();
    }
}
