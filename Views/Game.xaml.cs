using SigmaChess.ViewModels;
namespace SigmaChess.Views;

public partial class Game : ContentPage
{
	public Game()
	{
		InitializeComponent();
        BuildBoard();
	}
    private void BuildBoard()
    {
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();
        BoardGrid.Children.Clear();

        for (int i = 0; i < 8; i++)
        {
            BoardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var cell = new Border
                {
                    BackgroundColor = (r + c) % 2 == 0
                        ? Colors.Bisque
                        : Colors.SaddleBrown,
                    Stroke = Colors.Black,
                    StrokeThickness = 0.5
                };

                BoardGrid.Add(cell, c, r);
            }
    }
}