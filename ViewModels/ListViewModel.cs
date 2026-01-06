using SigmaChess.Servises;
using SigmaChess.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace SigmaChess.ViewModels
{
    internal class ListViewModel : ViewModelBase
    {
        #region get set
        
        private ObservableCollection<User> users;

        public ObservableCollection<User> Users
        {
            get { return users; }
            set { users = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region commands
        public ICommand DeleteItemCommand { get; set; }
        #endregion

        #region Consturctor
        public ListViewModel()
        {
            InitAsyncMethods();
            DeleteItemCommand = new Command((item) => DeleteItem(item)); // Currently this is a sync function , we will change it to async later
        }
        #endregion

        #region functions
        public async Task InitAsyncMethods()
        {
            Users = new ObservableCollection<User>(await AppService.GetInstance().GetUsers());
        }
        public void DeleteItem(object obgUser)
        {
            User userToDelete = (User)obgUser;

            Users.Remove((User)obgUser); // Remove the iem from the ObservableCollection on THIS PAGE only
            OnPropertyChanged();
            // We must also update the servie
            AppService.GetInstance().RemoveUser(userToDelete); // Currently this is a sync function , we will change it to async later
        }
        #endregion
    }
}