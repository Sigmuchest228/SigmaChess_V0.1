using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using SigmaChess.Services;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>
/// Страница партии. Сама XAML описывает только обрамление (заголовок, статусные лейблы,
/// кнопки и квадратный Grid доски), а 64 клетки и подписи координат страница строит
/// программно при первом <see cref="OnAppearing"/>. Это позволяет:
///   - один раз создать UI-объекты (сетки клеток, лейблы) и переиспользовать их при перерисовке,
///   - перевешивать клетки при перевороте доски через <c>Grid.SetRow/SetColumn</c>,
///     не пересоздавая View'ы.
/// </summary>
public partial class GamePage : ContentPage
{
    // Кэшируем ссылки на View-элементы, чтобы переворот/обновления выполнялись по индексу,
    // без перебора всего визуального дерева.
    private readonly Grid[,] _squares = new Grid[8, 8];
    private readonly Label[] _rankLabels = new Label[8];
    private readonly Label[] _fileLabels = new Label[8];
    private bool _boardBuilt;

    public GamePage()
    {
        InitializeComponent();
        BindingContext = AppService.GetInstance().GameViewModel;
    }

    /// <summary>
    /// Первая инициализация (ленивая): дёргает VM, строит клетки/координаты,
    /// подписывается на PropertyChanged для авто-переворота. Безопасно вызывается повторно
    /// (флаг <c>_boardBuilt</c> и метод <see cref="GameViewModel.EnsureInitializedAsync"/>
    /// идемпотентны).
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is not GameViewModel vm)
        {
            return;
        }

        await vm.EnsureInitializedAsync();

        if (vm.ShouldOfferTimeSetupOnAppear())
        {
            var popup = new NewGameSetupPopup();
            await this.ShowPopupAsync(popup);
            var result = await popup.WaitForResultAsync();
            if (result is null)
            {
                await vm.NavigateToMainPageAsync();
                return;
            }

            vm.ApplyTimeControl(result);
            vm.StartNewGameAfterSetup();
        }
        else
        {
            vm.RefreshBoard();
        }

        PlaceBoardForLayoutMode(vm);

        if (!_boardBuilt)
        {
            BuildSquares(vm);
            BuildCoordinateLabels(vm);
            _boardBuilt = true;
        }

        vm.OnGamePageAppeared();

        ApplyOrientation(vm.IsBoardFlipped);

        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        BoardGrid.InvalidateMeasure();
        BoardWithCoords.InvalidateMeasure();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is GameViewModel vm)
        {
            vm.OnGamePageDisappeared();
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void PlaceBoardForLayoutMode(GameViewModel vm)
    {
        var targetHost = vm.IsFaceToFaceLayout ? FaceToFaceBoardHost : CasualBoardHost;
        if (ReferenceEquals(BoardWithCoords.Parent, targetHost))
        {
            return;
        }

        if (BoardWithCoords.Parent is Layout layout)
        {
            layout.Children.Remove(BoardWithCoords);
        }

        targetHost.Children.Add(BoardWithCoords);
    }

    // Реагируем на смену ориентации доски и режима раскладки (доска переносится между хостами).
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GameViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(GameViewModel.IsBoardFlipped))
        {
            ApplyOrientation(vm.IsBoardFlipped);
        }

        if (e.PropertyName == nameof(GameViewModel.LayoutMode))
        {
            PlaceBoardForLayoutMode(vm);
            BoardGrid.InvalidateMeasure();
            BoardWithCoords.InvalidateMeasure();
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (BindingContext is not GameViewModel vm)
        {
            return;
        }

        // Передаём ту же VM, чтобы биндинги внутри попапа управляли реальными настройками.
        await this.ShowPopupAsync(new GameSettingsPopup(vm));
    }

    // Создаёт 64 клетки доски: Grid + Label (фигура), фон клетки, тап.
    // ВАЖНО: каждый SetBinding получает СВОЙ Binding-объект (через `new Binding(...)`).
    // Если переиспользовать один экземпляр Binding между несколькими BindableObject —
    // MAUI кидает InvalidOperationException "Binding instances cannot be reused".
    //
    // Контейнер — Grid, не Border: на WinUI у Border даже при StrokeThickness=0 и прозрачном
    // Stroke иногда остаётся «рваная» тёмная линия по периметру; выделение и так задаётся
    // цветом фона в <see cref="BoardCellViewModel.SquareBackground"/> (IsSelected).
    private void BuildSquares(GameViewModel vm)
    {
        foreach (var cell in vm.Cells)
        {
            var label = new Label
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            };
            label.SetBinding(Label.TextProperty, nameof(BoardCellViewModel.PieceSymbol));
            // Размер шрифта берём с GameViewModel (он зависит от размера доски).
            // Здесь нужен кастомный source, поэтому используем new Binding(...).
            label.SetBinding(Label.FontSizeProperty, new Binding(nameof(GameViewModel.PieceFontSize), source: vm));
            label.SetBinding(Label.RotationProperty, nameof(BoardCellViewModel.PieceGlyphRotation));

            var square = new Grid
            {
                BindingContext = cell,
                Padding = 0,
                Margin = 0,
            };
            square.SetBinding(BackgroundColorProperty, nameof(BoardCellViewModel.SquareBackground));
            square.Children.Add(label);

            // Захватываем cell в локальную переменную, иначе все лямбды разделят ту же ссылку
            // и в обработчик попадёт последняя клетка цикла (классический foreach-closure-bug).
            var capturedCell = cell;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await vm.OnCellTappedAsync(capturedCell);
            square.GestureRecognizers.Add(tap);

            _squares[cell.Row, cell.Col] = square;
            // Сразу задаём ячейку сетки; иначе по умолчанию все дети попадают в (0,0) до первого
            // ApplyOrientation — на WinUI при повторном появлении страницы это даёт «одна клетка».
            Grid.SetRow(square, cell.Row);
            Grid.SetColumn(square, cell.Col);
            BoardGrid.Children.Add(square);
        }
    }

    // Подписи рангов (8..1) и файлов (a..h) вокруг доски. Цифры по левому краю, буквы внизу.
    // Размер шрифта тоже берём из VM, чтобы он подстраивался под размер доски.
    private void BuildCoordinateLabels(GameViewModel vm)
    {
        var coordColor = Color.FromArgb("#444");

        for (var r = 0; r < 8; r++)
        {
            var label = new Label
            {
                Text = (8 - r).ToString(),
                TextColor = coordColor,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(0, 0, 4, 0),
            };
            label.SetBinding(Label.FontSizeProperty, new Binding(nameof(GameViewModel.CoordFontSize), source: vm));
            _rankLabels[r] = label;
            BoardWithCoords.Children.Add(label);
        }

        for (var c = 0; c < 8; c++)
        {
            var label = new Label
            {
                Text = ((char)('a' + c)).ToString(),
                TextColor = coordColor,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
            };
            label.SetBinding(Label.FontSizeProperty, new Binding(nameof(GameViewModel.CoordFontSize), source: vm));
            _fileLabels[c] = label;
            BoardWithCoords.Children.Add(label);
        }
    }

    // Перевешивает существующие View-элементы по строкам/колонкам Grid в зависимости от
    // ориентации доски. flipped=true — чёрные снизу, белые сверху (для второго игрока).
    // Сама доска использует Grid 8x8, координаты лежат в "обрамляющем" Grid с лишней колонкой/рядом.
    private void ApplyOrientation(bool flipped)
    {
        for (var r = 0; r < 8; r++)
        {
            for (var c = 0; c < 8; c++)
            {
                var square = _squares[r, c];
                if (square is null)
                {
                    continue;
                }

                Grid.SetRow(square, flipped ? 7 - r : r);
                Grid.SetColumn(square, flipped ? 7 - c : c);
            }
        }

        for (var r = 0; r < 8; r++)
        {
            var label = _rankLabels[r];
            if (label is null)
            {
                continue;
            }

            // Цифры всегда в колонке 0 (слева от доски), а строка соответствует ряду.
            Grid.SetColumn(label, 0);
            Grid.SetRow(label, flipped ? 7 - r : r);
        }

        for (var c = 0; c < 8; c++)
        {
            var label = _fileLabels[c];
            if (label is null)
            {
                continue;
            }

            // Буквы всегда в строке 8 (под доской), колонка = файл + 1 (т. к. колонка 0 у цифр).
            Grid.SetRow(label, 8);
            Grid.SetColumn(label, (flipped ? 7 - c : c) + 1);
        }
    }
}
