using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using SigmaChess.Models;

namespace SigmaChess.ViewModels
{
    public class GameViewModel
    {
        public User WhitePlayer { get; }
        public User BlackPlayer { get; }

        public ObservableCollection<SigmaChess.Models.Cell> Cells { get; }

        public GameViewModel()
        {
            WhitePlayer = new User { UserName = "Player 1", Elo = 1450 };
            BlackPlayer = new User { UserName = "Player 2", Elo = 1520 };

            var board = new Board();

            Cells = new ObservableCollection<SigmaChess.Models.Cell>();

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    Cells.Add(board[r, c]);
        }
    }
}
