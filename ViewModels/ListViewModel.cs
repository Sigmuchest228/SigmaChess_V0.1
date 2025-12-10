using SigmaChess.Servises;
using SigmaChess.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SigmaChess.ViewModels
{
    internal class ListViewModel
    {
        #region get set
        private string friendN;
        public string FriendN
        {
            get { return friendN; }
            set
            {
                if (null!=value)
                {
                friendN = value;
                }
            }
        }

        #endregion
    }
}