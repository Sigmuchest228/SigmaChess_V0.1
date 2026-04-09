using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using SigmaChess.Models;

namespace SigmaChess.ViewModels
{
    public class BoardViewModel
    {
        public ObservableCollection<BoardCell> Cells { get; }

        public BoardViewModel()
        {
            Cells = new ObservableCollection<BoardCell>();

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    Cells.Add(new BoardCell(r, c));
        }
    }
}