using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    public class Cell
    {
        public int Row { get; }
        public int Col { get; }

        public bool IsWhite => (Row + Col) % 2 == 0;

        private Piece? _piece;
        public Piece? Piece
        {
            get => _piece;
            set
            {
                if (_piece == value) return;
                _piece = value;
                PieceChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? PieceChanged;

        public Cell(int row, int col)
        {
            Row = row;
            Col = col;
        }
    }
}
