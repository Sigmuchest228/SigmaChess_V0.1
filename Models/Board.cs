using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    public class Board
    {
        public const int Size = 8;

        private readonly BoardCell[,] _cells = new BoardCell[Size, Size];

        public BoardCell this[int r, int c] => _cells[r, c];

        public Board()
        {
            InitCells();
        }

        private void InitCells()
        {
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    _cells[r, c] = new BoardCell(r, c);
        }

        public bool Inside(int r, int c)
        {
            return r >= 0 && r < Size && c >= 0 && c < Size;
        }
    }
}