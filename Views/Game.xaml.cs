using SigmaChess.Models;
using SigmaChess.ViewModels;
namespace SigmaChess.Views;

public partial class Game : ContentPage
{
    BoardViewModel vm;
    Dictionary<string, Button> map = new Dictionary<string, Button>();
    Dictionary<Dictionary<int, int>, Piece> chessMap = new Dictionary<Dictionary<int, int>, Piece>();
    public Game()
    {
        InitializeComponent();
        vm = new BoardViewModel();
        map["xy00"] = xy00; map["xy10"] = xy10; map["xy20"] = xy20; map["xy30"] = xy30;
        map["xy40"] = xy40; map["xy50"] = xy50; map["xy60"] = xy60; map["xy70"] = xy70;

        map["xy01"] = xy01; map["xy11"] = xy11; map["xy21"] = xy21; map["xy31"] = xy31;
        map["xy41"] = xy41; map["xy51"] = xy51; map["xy61"] = xy61; map["xy71"] = xy71;

        map["xy02"] = xy02; map["xy12"] = xy12; map["xy22"] = xy22; map["xy32"] = xy32;
        map["xy42"] = xy42; map["xy52"] = xy52; map["xy62"] = xy62; map["xy72"] = xy72;

        map["xy03"] = xy03; map["xy13"] = xy13; map["xy23"] = xy23; map["xy33"] = xy33;
        map["xy43"] = xy43; map["xy53"] = xy53; map["xy63"] = xy63; map["xy73"] = xy73;

        map["xy04"] = xy04; map["xy14"] = xy14; map["xy24"] = xy24; map["xy34"] = xy34;
        map["xy44"] = xy44; map["xy54"] = xy54; map["xy64"] = xy64; map["xy74"] = xy74;

        map["xy05"] = xy05; map["xy15"] = xy15; map["xy25"] = xy25; map["xy35"] = xy35;
        map["xy45"] = xy45; map["xy55"] = xy55; map["xy65"] = xy65; map["xy75"] = xy75;

        map["xy06"] = xy06; map["xy16"] = xy16; map["xy26"] = xy26; map["xy36"] = xy36;
        map["xy46"] = xy46; map["xy56"] = xy56; map["xy66"] = xy66; map["xy76"] = xy76;

        map["xy07"] = xy07; map["xy17"] = xy17; map["xy27"] = xy27; map["xy37"] = xy37;
        map["xy47"] = xy47; map["xy57"] = xy57; map["xy67"] = xy67; map["xy77"] = xy77;


        PlaceObjectOnBoard(7, 7, "horse");
    }

    private void Button_Square_Clicked(object sender, EventArgs e)
    {
        var button = (Button)sender;

        // Get row and column indices
        int y = Grid.GetRow(button);
        int x = Grid.GetColumn(button);
    }
    public void PlaceObjectOnBoard(int x, int y, string img)
    {
        map["xy"+x.ToString()+y.ToString()].ImageSource = "horse";
    }
    //private void BuildBoard()
    //{
    //    BoardGrid.RowDefinitions.Clear();
    //    BoardGrid.ColumnDefinitions.Clear();
    //    BoardGrid.Children.Clear();

    //    for (int i = 0; i < 8; i++)
    //    {
    //        BoardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
    //        BoardGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
    //    }

    //    for (int r = 0; r < 8; r++)
    //        for (int c = 0; c < 8; c++)
    //        {
    //            var cell = new Border
    //            {
    //                BackgroundColor = (r + c) % 2 == 0
    //                    ? Colors.Bisque
    //                    : Colors.SaddleBrown,
    //                Stroke = Colors.Black,
    //                StrokeThickness = 0.5
    //            };

    //            BoardGrid.Add(cell, c, r);
    //        }
    //}
}