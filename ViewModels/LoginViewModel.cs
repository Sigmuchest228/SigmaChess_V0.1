using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SigmaChess.ViewModels
{
    internal class LoginViewModel : ViewModelBase
    {
        #region get&set

        private string errorMessage;
        public string ErrorMessage
        {
            get { return errorMessage; }
            set
            {
                if (value != null)
                {
                    errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private string userInput;
        public string UserInput
        {
            get { return userInput; }
            set
            {
                userInput = value;
                if (!string.IsNullOrEmpty(userInput) && 5 > userInput.Length)
                {
                    ErrorMessage = "The field has less than 5 characters";
                }
                else
                {
                    ErrorMessage = string.Empty;
                }
                OnPropertyChanged();
            }
        }

        private string passwordInput;
        public string PasswordInput
        {
            get { return passwordInput; }
            set
            {
                passwordInput = value;

                if (!string.IsNullOrEmpty(passwordInput))
                {
                    if (passwordInput.Length < 8)
                    {
                        ErrorMessage = "Password must be at least 8 characters";
                    }
                    else if (!passwordInput.Any(char.IsUpper))
                    {
                        ErrorMessage = "Password must contain at least one uppercase letter";
                    }
                    else if (!passwordInput.Any(ch => !char.IsLetterOrDigit(ch)))
                    {
                        ErrorMessage = "Password must contain at least one special character";
                    }
                    else
                    {
                        ErrorMessage = string.Empty; // passed all checks!
                    }
                }
                else
                {
                    ErrorMessage = string.Empty;
                }

                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands
        public ICommand GoToRegisterCommand { get; set; }
        #endregion

        #region constructor
        public LoginViewModel()
        {
            // Defining the Command for an async Function
            GoToRegisterCommand = new Command(async () => await GotoRegisterPage());
        }
        #endregion

        #region Methods
        private async Task GotoRegisterPage()
        {
            string tempError;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                tempError = "Login failed";
            }
            await Shell.Current.GoToAsync("//Register_Login");
        }
        #endregion

    }
}
