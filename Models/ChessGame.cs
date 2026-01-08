using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    class ChessGame
    {
        public string Id { get; set; }
        public User Player1{ get; set; }
        public User Player2{ get; set; }
        public DateTime DateTime { get; set; }
        public List<Move> Moves { get; set; }

    }
}
