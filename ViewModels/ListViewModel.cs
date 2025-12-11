using SigmaChess.Servises;
using SigmaChess.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SigmaChess.ViewModels
{
    internal class ListViewModel : ViewModelBase
    {
        #region get set
        
        private List<User> users;

        public List<User> Users
        {
            get { return users; }
            set { users = value;
                OnPropertyChanged();
            }
        }

        public ListViewModel()
        {
            Users = LocalDataService.GetInstance().GetUsers();
        }

        #endregion
    }
}