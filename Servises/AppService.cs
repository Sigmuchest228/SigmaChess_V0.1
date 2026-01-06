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
    class AppService
    {
        FirebaseAuthClient? auth;
        FirebaseClient? client;
        public AuthCredential? loginAuthUser;
        private string loggedInUserID;

        #region instance
        private static AppService instance;
        public AppService()
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

        public async Task<bool> TryRegister(string userNameString, string passwordString)
        {
            try
            {
                var respond = await auth.CreateUserWithEmailAndPasswordAsync(userNameString, passwordString);
                // User is signed up and logged in
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> TryLogin(string userNameString, string passwordString)
        {
            if (userNameString == null || passwordString == null)
            {
                return false;
            }
            try
            {
                var authUser = await auth.SignInWithEmailAndPasswordAsync(userNameString, passwordString);
                loginAuthUser = authUser.AuthCredential;
                loggedInUserID = auth.User.Uid;


                // Authentication successful 
                // We keep the token or Credential in loginAuthUser, so we can erase it later in logout
                // You can access the authenticated user's details via authUser.User
                // you should create a new user or person
                // Person person = new Person(){Email=authUser.User.info.Email, ...
                // Don't put the password in the Person :)

                // ((App)Application.Current).SetAuthenticatedShell();

                return true;
            }




            catch (FirebaseAuthException ex)
            {
                // Authentication failed
                return false;
            }
        }

        public bool Logout()
        {
            try
            {
                auth.SignOut();
                loginAuthUser = null;
                loggedInUserID = null;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static AppService GetInstance()
        {
            if (instance == null)
            {
                instance = new AppService();
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
