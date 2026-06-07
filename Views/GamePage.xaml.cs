using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using SigmaChess.Services;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class GamePage : ContentPage
{

    private readonly Grid[,] _squares = new Grid[8, 8];
    private readonly Label[] _rankLabels = new Label[8];
    private readonly Label[] _fileLabels = new Label[8];
    private bool _boardBuilt;

    public GamePage()
    {
        InitializeComponent();
        BindingContext = AppService.GetInstance().GameViewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is not GameViewModel vm)
        {
            return;
        }

        await vm.EnsureInitializedAsync();

        // Новый заход на экран: попап настройки, без восстановления старой партии.
        if (vm.ShouldOfferTimeSetupOnAppear())
        {
            vm.PrepareForSetupPopup();
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

        await this.ShowPopupAsync(new GameSettingsPopup(vm));
    }

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

            var capturedCell = cell;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await vm.OnCellTappedAsync(capturedCell);
            square.GestureRecognizers.Add(tap);

            _squares[cell.Row, cell.Col] = square;

            Grid.SetRow(square, cell.Row);
            Grid.SetColumn(square, cell.Col);
            BoardGrid.Children.Add(square);
        }
    }

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

            Grid.SetRow(label, 8);
            Grid.SetColumn(label, (flipped ? 7 - c : c) + 1);
        }
    }
}
