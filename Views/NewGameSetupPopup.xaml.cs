using CommunityToolkit.Maui.Views;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>Попап выбора контроля времени и режима раскладки перед стартом партии.</summary>
public partial class NewGameSetupPopup : Popup
{
    private static readonly string[] LayoutOptionLabels =
    [
        "Screen — clocks above/below board",
        "Board sides — OTB layout",
    ];

    private static readonly string[] TimePresetLabels =
    [
        "No clock",
        "1 min",
        "3 min",
        "5 min",
        "10 min",
        "15 min",
        "30 min",
        "45 min",
        "60 min",
    ];

    private readonly TaskCompletionSource<NewGameSetupResult?> _completion = new();

    private int _layoutSelectedIndex;
    private int _sharedTimeSelectedIndex = 4;
    private int _whiteTimeSelectedIndex = 4;
    private int _blackTimeSelectedIndex = 4;

    public NewGameSetupPopup()
    {
        InitializeComponent();

        _layoutSelectedIndex = 0;
        _sharedTimeSelectedIndex = 4;
        _whiteTimeSelectedIndex = 4;
        _blackTimeSelectedIndex = 4;

        SyncChoiceLabels();
        UpdateSameTimeVisibility();
    }

    private static Page? HostPage =>
        Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    private void SyncChoiceLabels()
    {
        LayoutChoiceLabel.Text = LayoutOptionLabels[_layoutSelectedIndex];
        SharedTimeChoiceLabel.Text = TimePresetLabels[_sharedTimeSelectedIndex];
        WhiteTimeChoiceLabel.Text = TimePresetLabels[_whiteTimeSelectedIndex];
        BlackTimeChoiceLabel.Text = TimePresetLabels[_blackTimeSelectedIndex];
    }

    /// <summary>Выбор строки через нативный action sheet: родительский попап не закрывается (повторный ShowPopupAsync после CloseAsync давал disposed).</summary>
    private Task<int?> PickOptionAsync(string title, IReadOnlyList<string> options) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = HostPage;
            if (page is null)
            {
                return (int?)null;
            }

            const string cancel = "Cancel";
            var picked = await page.DisplayActionSheet(title, cancel, null, options.ToArray());
            if (string.IsNullOrEmpty(picked) || string.Equals(picked, cancel, StringComparison.Ordinal))
            {
                return (int?)null;
            }

            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i], picked, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return (int?)null;
        });

    private async void OnLayoutPickTapped(object? sender, TappedEventArgs e)
    {
        var idx = await PickOptionAsync("Mode", LayoutOptionLabels).ConfigureAwait(true);
        if (idx is null)
        {
            return;
        }

        _layoutSelectedIndex = idx.Value;
        LayoutChoiceLabel.Text = LayoutOptionLabels[_layoutSelectedIndex];
    }

    private async void OnSharedTimePickTapped(object? sender, TappedEventArgs e)
    {
        var idx = await PickOptionAsync("Preset", TimePresetLabels).ConfigureAwait(true);
        if (idx is null)
        {
            return;
        }

        _sharedTimeSelectedIndex = idx.Value;
        SharedTimeChoiceLabel.Text = TimePresetLabels[_sharedTimeSelectedIndex];
    }

    private async void OnWhiteTimePickTapped(object? sender, TappedEventArgs e)
    {
        var idx = await PickOptionAsync("Preset", TimePresetLabels).ConfigureAwait(true);
        if (idx is null)
        {
            return;
        }

        _whiteTimeSelectedIndex = idx.Value;
        WhiteTimeChoiceLabel.Text = TimePresetLabels[_whiteTimeSelectedIndex];
    }

    private async void OnBlackTimePickTapped(object? sender, TappedEventArgs e)
    {
        var idx = await PickOptionAsync("Preset", TimePresetLabels).ConfigureAwait(true);
        if (idx is null)
        {
            return;
        }

        _blackTimeSelectedIndex = idx.Value;
        BlackTimeChoiceLabel.Text = TimePresetLabels[_blackTimeSelectedIndex];
    }

    public Task<NewGameSetupResult?> WaitForResultAsync() => _completion.Task;

    private void OnSameTimeToggled(object? sender, ToggledEventArgs e)
    {
        UpdateSameTimeVisibility();
    }

    private void UpdateSameTimeVisibility()
    {
        var same = SameTimeSwitch.IsToggled;
        SharedTimeSection.IsVisible = same;
        SeparateTimeSection.IsVisible = !same;
    }

    private static int PresetMinutes(int selectedIndex) =>
        selectedIndex switch
        {
            1 => 1,
            2 => 3,
            3 => 5,
            4 => 10,
            5 => 15,
            6 => 30,
            7 => 45,
            8 => 60,
            _ => 5
        };

    private static int MinutesFromPickerAndCustom(int pickerIndex, string? customText)
    {
        if (!string.IsNullOrWhiteSpace(customText)
            && int.TryParse(customText.Trim(), out var custom)
            && custom is >= 1 and <= 180)
        {
            return custom;
        }

        if (pickerIndex <= 0)
        {
            return 5;
        }

        return PresetMinutes(pickerIndex);
    }

    private void Complete(NewGameSetupResult result)
    {
        _completion.TrySetResult(result);
        _ = CloseAsync();
    }

    private void OnStartClicked(object? sender, EventArgs e)
    {
        var layoutMode = _layoutSelectedIndex == 1
            ? GameLayoutMode.FaceToFace
            : GameLayoutMode.Casual;

        if (SameTimeSwitch.IsToggled)
        {
            var idx = Math.Clamp(_sharedTimeSelectedIndex, 0, TimePresetLabels.Length - 1);
            if (idx == 0)
            {
                Complete(new NewGameSetupResult(
                    Unlimited: true,
                    SameTimeForBoth: true,
                    WhiteMinutes: 5,
                    BlackMinutes: 5,
                    LayoutMode: layoutMode));
                return;
            }

            var minutes = MinutesFromPickerAndCustom(idx, SharedCustomMinutes.Text);
            minutes = Math.Clamp(minutes, 1, 180);
            Complete(new NewGameSetupResult(
                Unlimited: false,
                SameTimeForBoth: true,
                WhiteMinutes: minutes,
                BlackMinutes: minutes,
                LayoutMode: layoutMode));
            return;
        }

        var wi = Math.Clamp(_whiteTimeSelectedIndex, 0, TimePresetLabels.Length - 1);
        var bi = Math.Clamp(_blackTimeSelectedIndex, 0, TimePresetLabels.Length - 1);

        if (wi == 0 && bi == 0)
        {
            Complete(new NewGameSetupResult(
                Unlimited: true,
                SameTimeForBoth: false,
                WhiteMinutes: 5,
                BlackMinutes: 5,
                LayoutMode: layoutMode));
            return;
        }

        var wMin = wi == 0
            ? MinutesFromPickerAndCustom(bi, BlackCustomMinutes.Text)
            : MinutesFromPickerAndCustom(wi, WhiteCustomMinutes.Text);
        var bMin = bi == 0
            ? MinutesFromPickerAndCustom(wi, WhiteCustomMinutes.Text)
            : MinutesFromPickerAndCustom(bi, BlackCustomMinutes.Text);

        wMin = Math.Clamp(wMin, 1, 180);
        bMin = Math.Clamp(bMin, 1, 180);

        Complete(new NewGameSetupResult(
            Unlimited: false,
            SameTimeForBoth: false,
            WhiteMinutes: wMin,
            BlackMinutes: bMin,
            LayoutMode: layoutMode));
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        _completion.TrySetResult(null);
        _ = CloseAsync();
    }
}
