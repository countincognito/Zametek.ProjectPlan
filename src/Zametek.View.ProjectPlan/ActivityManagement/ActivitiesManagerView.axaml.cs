using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Zametek.View.ProjectPlan
{
    public partial class ActivitiesManagerView
        : UserControl
    {
        public ActivitiesManagerView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
