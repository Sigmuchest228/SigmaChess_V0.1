using System.ComponentModel;
using Microsoft.Maui.Controls;
using SigmaChess.ViewModels;

namespace SigmaChess.Views;

public partial class GameReplayPage : ContentPage, IQueryAttributable
{
    private readonly Grid[,] _squares = new Grid[8, 8];
    private readonly Label[] _rankLabels = new Label[8];
    private readonly Label[] _fileLabels = new Label[8];
    private bool _boardBuilt;

    public GameReplayPage(GameReplayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is GameReplayViewModel vm)
        {
            vm.ApplyNavigationQuery(query);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is not GameReplayViewModel vm)
        {
            return;
        }

        await vm.InitializeAsync();

        if (!_boardBuilt)
        {
            BuildSquares(vm);
            BuildCoordinateLabels(vm);
            _boardBuilt = true;
        }

        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        vm.RefreshBoard();

        BoardGrid.InvalidateMeasure();
        BoardWithCoords.InvalidateMeasure();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is GameReplayViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GameReplayViewModel vm)
        {
            return;
        }

        if (e.PropertyName is nameof(GameReplayViewModel.BoardExtent))
        {
            BoardGrid.InvalidateMeasure();
            BoardWithCoords.InvalidateMeasure();
        }
    }

    private void BuildSquares(GameReplayViewModel vm)
    {
        foreach (var cell in vm.Cells)
        {
            var label = new Label
            {
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            };
            label.SetBinding(Label.TextProperty, nameof(BoardCellViewModel.PieceSymbol));
            label.SetBinding(Label.FontSizeProperty, new Binding(nameof(GameReplayViewModel.PieceFontSize), source: vm));
            label.SetBinding(Label.RotationProperty, nameof(BoardCellViewModel.PieceGlyphRotation));

            var square = new Grid
            {
                BindingContext = cell,
                Padding = 0,
                Margin = 0,
            };
            square.SetBinding(BackgroundColorProperty, nameof(BoardCellViewModel.SquareBackground));
            square.Children.Add(label);

            _squares[cell.Row, cell.Col] = square;
            Grid.SetRow(square, cell.Row);
            Grid.SetColumn(square, cell.Col);
            BoardGrid.Children.Add(square);
        }
    }

    private void BuildCoordinateLabels(GameReplayViewModel vm)
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
            label.SetBinding(Label.FontSizeProperty, new Binding(nameof(GameReplayViewModel.CoordFontSize), source: vm));
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
            label.SetBinding(Label.FontSizeProperty, new Binding(nameof(GameReplayViewModel.CoordFontSize), source: vm));
            _fileLabels[c] = label;
            BoardWithCoords.Children.Add(label);
        }

        for (var r = 0; r < 8; r++)
        {
            var label = _rankLabels[r];
            Grid.SetColumn(label, 0);
            Grid.SetRow(label, r);
        }

        for (var c = 0; c < 8; c++)
        {
            var label = _fileLabels[c];
            Grid.SetRow(label, 8);
            Grid.SetColumn(label, c + 1);
        }
    }
}
