using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SigmaChess.Models;
using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Auth.Repository;
using Firebase.Database;
using Firebase.Database.Query;

// name collision resolve
using FirebaseUser = Firebase.Auth.User;
using User = SigmaChess.Models.User;

namespace SigmaChess.Servises
{
    class LocalDataService
    {
        static FirebaseAuthClient? auth;
        static FirebaseClient? client;
        static public AuthCredential? loginAuthUser;

        #region instance
        private static LocalDataService instance;
        public LocalDataService()
        {
            //CreateFakeData();
        }
        public void InitAuth()
        {
            var config = new FirebaseAuthConfig()
            {
                ApiKey = "AIzaSyCl1Ix-ZEcM4JLBjFew5XsS1LTQIpg8j7U",
                AuthDomain = "sigmachess-75f04.firebaseapp.com", //כתובת התחברות
                Providers = new FirebaseAuthProvider[] //רשימת אפשריות להתחבר
              {
          new EmailProvider() //אנחנו נשתמש בשירות חינמי של התחברות עם מייל
              },
                UserRepository = new FileUserRepository("appUserData") //לא חובה, שם של קובץ בטלפון הפרטי שאפשר לשמור בו את מזהה ההתחברות כדי לא הכניס כל פעם את הסיסמא 
            };
            auth = new FirebaseAuthClient(config); //ההתחברות

            client =
              new FirebaseClient(@"https://sigmachess-75f04-default-rtdb.europe-west1.firebasedatabase.app", //כתובת מסד הנתונים
              new FirebaseOptions
              {
                  AuthTokenAsyncFactory = () => Task.FromResult(auth.User.Credential.IdToken)// מזהה ההתחברות של המשתמש עם השרת, הנתון נשמר במכשיר
              });
        }

        public static LocalDataService GetInstance()
        {
            if (instance == null)
            {
                instance = new LocalDataService();
                instance.InitAuth();
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
