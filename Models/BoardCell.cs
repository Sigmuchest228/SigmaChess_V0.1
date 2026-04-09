using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;

namespace SigmaChess.Models
{
    public class BoardCell
    {
        public int Row { get; }
        public int Col { get; }

        public bool IsWhite => (Row + Col) % 2 == 0;

        public Color Color => IsWhite
            ? Colors.Bisque
            : Colors.SaddleBrown;

        public BoardCell(int row, int col)
        {
            Row = row;
            Col = col;
        }
    }
}