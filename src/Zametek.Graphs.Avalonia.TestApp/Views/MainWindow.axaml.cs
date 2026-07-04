using Avalonia.Controls;
using System;

namespace Zametek.Graphs.Avalonia.TestApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Dispose the shell view-model (and through it every tab's interactive graph + host) when the
        // window closes.
        protected override void OnClosed(EventArgs e)
        {
            (DataContext as IDisposable)?.Dispose();
            base.OnClosed(e);
        }
    }
}
