using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Zametek.View.ProjectPlan
{
    public partial class ConfirmationDialog : Window
    {
        public bool Result { get; private set; }

        // Parameterless ctor required by the Avalonia XAML runtime loader.
        public ConfirmationDialog()
        {
            InitializeComponent();
        }

        public ConfirmationDialog(string title, string message)
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;
        }

        private void YesButton_Click(object? sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void NoButton_Click(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }
}
