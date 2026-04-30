using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

/// <summary>
/// Страница партии. Сама XAML описывает только обрамление (заголовок, статусные лейблы,
/// кнопки и квадратный Grid доски), а 64 клетки и подписи координат страница строит
/// программно при первом <see cref="OnAppearing"/>. Это позволяет:
///   - один раз создать UI-объекты (бордеры, лейблы) и переиспользовать их при перерисовке,
///   - перевешивать клетки при перевороте доски через <c>Grid.SetRow/SetColumn</c>,
///     не пересоздавая View'ы.
/// </summary>
public partial class GamePage : ContentPage
{
    // Кэшируем ссылки на View-элементы, чтобы переворот/обновления выполнялись по индексу,
    // без перебора всего визуального дерева.
    private readonly Border[,] _squares = new Border[8, 8];
    private readonly Label[] _rankLabels = new Label[8];
    private readonly Label[] _fileLabels = new Label[8];
    private bool _boardBuilt;

    public GamePage(GameViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
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

        if (!_boardBuilt)
        {
            BuildSquares(vm);
            BuildCoordinateLabels(vm);
            _boardBuilt = true;
        }

        ApplyOrientation(vm.IsBoardFlipped);

        // Сначала -=, потом += — чтобы не подписаться дважды при повторном входе на страницу.
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is GameViewModel vm)
        {
            // Отписка обязательна — иначе VM (Singleton) будет держать ссылку на страницу
            // и она не соберётся GC.
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    // Реагируем только на смену IsBoardFlipped — остальные свойства обновятся через биндинги.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is GameViewModel vm && e.PropertyName == nameof(GameViewModel.IsBoardFlipped))
        {
            ApplyOrientation(vm.IsBoardFlipped);
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

    // Создаёт 64 клетки доски: Border + Label (фигура), биндинги, DataTrigger для рамки выбора, тап.
    // ВАЖНО: каждый SetBinding получает СВОЙ Binding-объект (через `new Binding(...)`).
    // Если переиспользовать один экземпляр Binding между несколькими BindableObject —
    // MAUI кидает InvalidOperationException "Binding instances cannot be reused".
    private void BuildSquares(GameViewModel vm)
    {
        var selectedColor = Color.FromArgb("#E65100");

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

            var border = new Border
            {
                BindingContext = cell,
                Stroke = Colors.Transparent,
                StrokeThickness = 0,
                Padding = 0,
                Margin = 0,
                Content = label,
            };
            border.SetBinding(BackgroundColorProperty, nameof(BoardCellViewModel.SquareBackground));

            // DataTrigger: когда у ячейки IsSelected=true — рисуем оранжевую рамку вокруг.
            // Альтернативой был бы триггер в стиле/XAML, но программно проще держать всё рядом.
            var trigger = new DataTrigger(typeof(Border))
            {
                Binding = new Binding(nameof(BoardCellViewModel.IsSelected)),
                Value = true,
            };
            trigger.Setters.Add(new Setter { Property = Border.StrokeProperty, Value = selectedColor });
            trigger.Setters.Add(new Setter { Property = Border.StrokeThicknessProperty, Value = 3 });
            border.Triggers.Add(trigger);

            // Захватываем cell в локальную переменную, иначе все лямбды разделят ту же ссылку
            // и в обработчик попадёт последняя клетка цикла (классический foreach-closure-bug).
            var capturedCell = cell;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await vm.OnCellTappedAsync(capturedCell);
            border.GestureRecognizers.Add(tap);

            _squares[cell.Row, cell.Col] = border;
            BoardGrid.Children.Add(border);
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
                var border = _squares[r, c];
                if (border is null)
                {
                    continue;
                }

                Grid.SetRow(border, flipped ? 7 - r : r);
                Grid.SetColumn(border, flipped ? 7 - c : c);
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
