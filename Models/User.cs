using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmaChess.Models
{
    public class User
    {
            public Guid Id { get; set; } = Guid.NewGuid();

            public string UserName { get; set; } = "";

            public int Elo { get; set; } = 1200;

            public DateTime RegisterDate { get; set; } = DateTime.Now;
        
    }
}
