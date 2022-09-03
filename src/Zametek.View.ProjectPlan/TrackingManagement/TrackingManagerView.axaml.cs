using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Zametek.View.ProjectPlan
{
    public partial class TrackingManagerView
        : UserControl
    {
        public TrackingManagerView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
