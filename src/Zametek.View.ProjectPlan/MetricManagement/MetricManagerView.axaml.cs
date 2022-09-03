using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Zametek.View.ProjectPlan
{
    public partial class MetricManagerView
        : UserControl
    {
        public MetricManagerView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
