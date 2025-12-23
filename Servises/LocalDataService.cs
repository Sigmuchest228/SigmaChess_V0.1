using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SigmaChess.Models;

namespace SigmaChess.Servises
{
    class LocalDataService
    {
        #region instance
        private static LocalDataService instance;
        public LocalDataService()
        {
            CreateFakeData();
        }
        public static LocalDataService GetInstance()
        {
            if (instance == null)
            {
                instance = new LocalDataService();
            }
            return instance;
        }
        #endregion

        public List<User> users = new List<User>();
        private void CreateFakeData()
        {
            User user1 = new User()
            {
                Id = 1,
                UserName = "Sergay",
                Elo = 452,
                RegisterDate = DateTime.Now.AddMinutes(-5)
            };
            User user2 = new User()
            {
                Id = 2,
                UserName = "Artyom",
                Elo = 1208,
                RegisterDate = DateTime.Now.AddMinutes(-30)
            };
            User user3 = new User()
            {
                Id = 42,
                UserName = "Carlson Magnusen",
                Elo = 1042,
                RegisterDate = DateTime.Now.AddMinutes(-42)
            };

            users.Add(user1);
            users.Add(user2);
            users.Add(user3);
        }

        public async Task<List<User>> GetUsers()
        {
            return users;
        }
        public void RemoveUser(User usr)
        {
            users.Remove(usr);
        }
        public void AddUser(User usr)
        {
            users.Add(usr);
        }


    }
}
