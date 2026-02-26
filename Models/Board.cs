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

        private readonly Cell[,] _cells = new Cell[Size, Size];

        public Cell this[int r, int c] => _cells[r, c];

        public Board()
        {
            InitCells();
            SetupPieces();
        }

        private void InitCells()
        {
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    _cells[r, c] = new Cell(r, c);
        }

        public bool Inside(int r, int c)
        {
            return r >= 0 && r < Size && c >= 0 && c < Size;
        }

        private void SetupPieces()
        {
            // Пешки
            //for (int c = 0; c < 8; c++)
            //{
            //    Place(new Pawn(true), 6, c);
            //    Place(new Pawn(false), 1, c);
            //}

            // Ладьи
            //Place(new Rook(true), 7, 0);
            //Place(new Rook(true), 7, 7);
            //Place(new Rook(false), 0, 0);
            //Place(new Rook(false), 0, 7);

            // Коней, слонов, ферзя, короля добавим позже
        }

        public void Place(Piece piece, int r, int c)
        {
            var cell = _cells[r, c];
            cell.Piece = piece;
            piece.SetCell(cell);
        }

        public bool TryMove(Cell from, Cell to)
        {
            if (from.Piece == null) return false;

            var legal = from.Piece.GetLegalMoves(this);

            if (!legal.Contains(to)) return false;

            Move(from, to);
            return true;
        }

        private void Move(Cell from, Cell to)
        {
            to.Piece = from.Piece;
            to.Piece.SetCell(to);
            from.Piece = null;
        }
    }
}
