using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    class Move
    {
        public int Id { get; set; }
        public string FromPos { get; set; } //b1
        public string ToPos { get; set; } //c3
        public float TimePerMove { get; set; }
        public User User { get; set; }
    }
}
