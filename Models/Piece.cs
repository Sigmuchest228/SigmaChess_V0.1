using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    public abstract class Piece
    {
        public bool IsWhite { get; }

        public BoardCell Cell { get; private set; }

        protected Piece(bool isWhite)
        {
            IsWhite = isWhite;
        }

        public void SetCell(BoardCell cell)
        {
            Cell = cell;
        }

        public abstract List<BoardCell> GetLegalMoves(Board board);

        public virtual char Symbol => '?';
    }
}
