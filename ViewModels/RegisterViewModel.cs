using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SigmaChess.ViewModels
{
    internal class RegisterViewModel : ViewModelBase
    {
        #region get and set
        // get and set for MessageForEldan
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

        // get and set for UserInput
        public string userInput;
        public string UserInput
        {
            get { return userInput; }
            set
            {
                userInput = value;
                if (userInput != null && userInput.Length > 5)
                {
                    ErrorMessage = "The field has more than 5 characters";
                }
                else
                {
                    ErrorMessage = "The field has 5 or fewer characters";
                }
                OnPropertyChanged();
            }
        }
        #endregion

        #region Commands
        public ICommand ResetCommand { get; set; }
        public ICommand GotoAnotherPageCommand { get; set; }
        #endregion

        # region constructor
        public RegisterViewModel()
        {

            // Defining the Command for a non async Function
            ResetCommand = new Command(ResetField);
            // Defining the Command for an async Function
            GotoAnotherPageCommand = new Command(async () => await GotoLoginPage());
        }
        #endregion

        #region  Methods
        private void ResetField()
        {
            UserInput = "";
            ErrorMessage = "";  
        }

        private async Task GotoLoginPage()
        {
            await Shell.Current.GoToAsync("//Login_Register");
        }
        #endregion

    }
}

