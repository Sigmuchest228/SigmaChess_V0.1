using CommunityToolkit.Maui.Views;
using SigmaChess.Engine;

namespace SigmaChess.Views;

/// <summary>
/// Попап выбора фигуры превращения пешки. Показывает 4 кнопки (ферзь/ладья/слон/конь)
/// в цвете ходящей стороны, ждёт выбор и возвращает его через <see cref="Choice"/>.
/// <para>
/// Тап мимо для закрытия отключён (см. XAML, <c>CanBeDismissedByTappingOutsideOfPopup="False"</c>),
/// потому что без выбора партия зависнет в подвешенном состоянии: пешка стоит на 8-й
/// горизонтали без типа.
/// </para>
/// </summary>
public partial class PromotionPopup : Popup
{
    /// <summary>Выбор пользователя. По умолчанию — ферзь (на случай, если попап закроют программно).</summary>
    public PieceType Choice { get; private set; } = PieceType.Queen;

    public PromotionPopup(PieceColor color)
    {
        InitializeComponent();

        QueenButton.Text = SymbolFor(color, PieceType.Queen);
        RookButton.Text = SymbolFor(color, PieceType.Rook);
        BishopButton.Text = SymbolFor(color, PieceType.Bishop);
        KnightButton.Text = SymbolFor(color, PieceType.Knight);
    }

    /// <summary>
    /// Открыть попап, дождаться выбора и вернуть его. Если активная страница не найдена
    /// (теоретически невозможно во время игры) — возвращает Queen как безопасный дефолт.
    /// </summary>
    public static async Task<PieceType> ShowAsync(PieceColor color)
    {
        var page = Shell.Current?.CurrentPage;
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

    // Юникод-символы тех же фигур, что и на доске. Дублируется здесь намеренно:
    // BoardCellViewModel — это отдельный слой, и попап не должен от него зависеть.
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
