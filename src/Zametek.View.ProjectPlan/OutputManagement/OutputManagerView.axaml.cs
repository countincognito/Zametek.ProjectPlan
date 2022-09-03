using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Zametek.View.ProjectPlan
{
    public partial class OutputManagerView
        : UserControl
    {
        public OutputManagerView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
