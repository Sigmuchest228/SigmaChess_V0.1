using CommunityToolkit.Maui.Views;
using SigmaChess.Engine;

namespace SigmaChess.Views;

public partial class PromotionPopup : Popup
{

    public PieceType Choice { get; private set; } = PieceType.Queen;

    public PromotionPopup(PieceColor color)
    {
        InitializeComponent();

        QueenButton.Text = SymbolFor(color, PieceType.Queen);
        RookButton.Text = SymbolFor(color, PieceType.Rook);
        BishopButton.Text = SymbolFor(color, PieceType.Bishop);
        KnightButton.Text = SymbolFor(color, PieceType.Knight);
    }

    public static async Task<PieceType> ShowAsync(PieceColor color)
    {
        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return PieceType.Queen;
        }

        var popup = new PromotionPopup(color);
        await page.ShowPopupAsync(popup);
        return popup.Choice;
    }

    private async void OnQueenClicked(object? sender, EventArgs e) => await CloseWith(PieceType.Queen);

    private async void OnRookClicked(object? sender, EventArgs e) => await CloseWith(PieceType.Rook);

    private async void OnBishopClicked(object? sender, EventArgs e) => await CloseWith(PieceType.Bishop);

    private async void OnKnightClicked(object? sender, EventArgs e) => await CloseWith(PieceType.Knight);

    private async Task CloseWith(PieceType type)
    {
        Choice = type;
        await CloseAsync();
    }

    private static string SymbolFor(PieceColor color, PieceType type) => (color, type) switch
    {
        (PieceColor.White, PieceType.Queen) => "\u2655",
        (PieceColor.White, PieceType.Rook) => "\u2656",
        (PieceColor.White, PieceType.Bishop) => "\u2657",
        (PieceColor.White, PieceType.Knight) => "\u2658",
        (PieceColor.Black, PieceType.Queen) => "\u265B",
        (PieceColor.Black, PieceType.Rook) => "\u265C",
        (PieceColor.Black, PieceType.Bishop) => "\u265D",
        (PieceColor.Black, PieceType.Knight) => "\u265E",
        _ => string.Empty,
    };
}
