using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    internal class User
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public int Elo { get; set; }
        public DateTime RegisterDate { get; set; }
    }
}
