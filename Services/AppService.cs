using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Auth.Repository;
using Firebase.Database;

namespace SigmaChess.Services;

public class AppService
{
    private readonly FirebaseAuthClient _auth;
    private readonly FirebaseClient _client;

    public AppService()
    {
        var config = new FirebaseAuthConfig
        {
            ApiKey = "AIzaSyCl1Ix-ZEcM4JLBjFew5XsS1LTQIpg8j7U",
            AuthDomain = "sigmachess-75f04.firebaseapp.com",
            Providers = [new EmailProvider()],
            UserRepository = new FileUserRepository("appUserData")
        };

        _auth = new FirebaseAuthClient(config);
        _client = new FirebaseClient(
            "https://sigmachess-75f04-default-rtdb.europe-west1.firebasedatabase.app",
            new FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(_auth.User.Credential.IdToken)
            });
    }

    public async Task<bool> TryRegister(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        try
        {
            await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryLogin(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        try
        {
            await _auth.SignInWithEmailAndPasswordAsync(email, password);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Logout()
    {
        try
        {
            _auth.SignOut();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
