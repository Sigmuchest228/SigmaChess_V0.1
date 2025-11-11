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
        public List<Friend> friends = new List<Friend>();
        private void CreateFakeData()
        {
            Friend friend1 = new Friend()
            {
                Id = "1",
                Name = "Sergay",
                Elo = "452",
                RegisterDate = DateTime.Now.AddMinutes(-5)
            };
            Friend friend2 = new Friend()
            {
                Id = "2",
                Name = "Artyom",
                Elo = "1208",
                RegisterDate = DateTime.Now.AddMinutes(-30)
            };
            Friend friend3 = new Friend()
            {
                Id = "42",
                Name = "Carlson Magnusen",
                Elo = "3642",
                RegisterDate = DateTime.Now.AddMinutes(-42)
            };

            friends.Add(friend1);
            friends.Add(friend2);
            friends.Add(friend3);
        }

        public List<Friend> GetMessages()
        {
            return friends;
        }
        public void RemoveMessage(Friend friend)
        {
            friends.Remove(friend);
        }
        public void AddMessage(Friend friend)
        {
            friends.Add(friend);
        }


    }
}
