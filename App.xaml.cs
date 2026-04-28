namespace SigmaChess
{
    public partial class App : Application
    {
        private readonly AppShell _appShell;

        public App(AppShell appShell)
        {
            _appShell = appShell;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            _appShell.FlowDirection = FlowDirection.LeftToRight;
            return new Window(_appShell);
        }
    }
}
