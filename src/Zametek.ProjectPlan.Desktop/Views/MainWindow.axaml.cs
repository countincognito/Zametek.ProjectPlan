using Avalonia.Controls;

namespace Zametek.ProjectPlan.Desktop
{
    public partial class MainWindow
        : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Closing the window routes through the Closing handlers wired up in App, so choosing
            // File | Exit applies the unsaved-changes confirmation exactly as the title bar's close
            // button does - which is what Exit did directly before the shell stopped being a window.
            MainViewControl.ExitRequested += (_, _) => Close();
        }

        /// <summary>
        /// Forwarded to the shell, which needs it before it is loaded.
        /// </summary>
        /// <remarks>
        /// The shell applies this to the view model in its Loaded handler, and Loaded does not fire
        /// until the window is shown, so setting it on a constructed but unshown window is in time.
        /// </remarks>
        public string InitialTheme
        {
            get => MainViewControl.InitialTheme;
            set => MainViewControl.InitialTheme = value;
        }
    }
}
