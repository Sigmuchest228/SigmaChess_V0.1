using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    class Friend
    {
        public string Id { get; set; }
        public string? Name { get; set; }
        public string? Elo { get; set; }

        public DateTime RegisterDate { get; set; }
    }
}
