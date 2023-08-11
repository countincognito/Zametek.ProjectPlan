using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace Zametek.View.ProjectPlan
{
    public class DataGridTrackingColumn
        : DataGridTemplateColumn
    {
        private readonly int m_Index;
        private readonly string m_DisplayName;

        public DataGridTrackingColumn(int index, string displayName)//!!)
        {
            m_Index = index;
            m_DisplayName = displayName;

            CanUserResize = true;
            CanUserReorder = false;
            CanUserSort = false;
            IsReadOnly = false;
            Width = new DataGridLength(75);

            CellTemplate = new FuncDataTemplate<object>((itemModel, namescope) =>
            {
                var mainDockPanel = new DockPanel
                {
                    [!ToolTip.TipProperty] = new Binding($@"Trackers[{m_Index}].DisplayName", BindingMode.OneWay),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                var percentageTextBlock = new TextBlock
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    TextAlignment = Avalonia.Media.TextAlignment.Right,
                    Background = Avalonia.Media.Brushes.Transparent,
                    Margin = new Avalonia.Thickness(0,0,11,0),
                    Padding = new Avalonia.Thickness(0),
                    [!TextBlock.TextProperty] = new Binding($@"Trackers[{m_Index}].PercentageComplete", BindingMode.OneWay)
                };

                var isIncludedCheckBox = new CheckBox
                {
                    [!CheckBox.IsCheckedProperty] = new Binding($@"Trackers[{m_Index}].IsIncluded", BindingMode.OneWay),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Background = Avalonia.Media.Brushes.White,
                    Margin = new Avalonia.Thickness(0,0,3,0),
                    Padding = new Avalonia.Thickness(0),
                    IsHitTestVisible = false
                };

                DockPanel.SetDock(isIncludedCheckBox, Avalonia.Controls.Dock.Right);

                mainDockPanel.Children.Add(isIncludedCheckBox);
                mainDockPanel.Children.Add(percentageTextBlock);
                return mainDockPanel;
            });

            CellEditingTemplate = new FuncDataTemplate<object>((itemModel, namescope) =>
            {
                var mainDockPanel = new DockPanel
                {
                    [!ToolTip.TipProperty] = new Binding($@"Trackers[{m_Index}].DisplayName", BindingMode.OneWay),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                mainDockPanel.Classes.Add(@"editable");

                var percentageNumericUpDown = new NumericUpDown
                {
                    [!NumericUpDown.ValueProperty] = new Binding($@"Trackers[{m_Index}].PercentageComplete", BindingMode.TwoWay)
                    {
                        FallbackValue = 0
                    },
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    ShowButtonSpinner = false,
                    Background = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = new Avalonia.Thickness(0),
                    Margin = new Avalonia.Thickness(0,0,7,0),
                    Padding = new Avalonia.Thickness(0),
                    Minimum = 0,
                    Maximum = 100,
                };

                var isIncludedCheckBox = new CheckBox
                {
                    [!CheckBox.IsCheckedProperty] = new Binding($@"Trackers[{m_Index}].IsIncluded", BindingMode.TwoWay),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Background = Avalonia.Media.Brushes.White,
                    Margin = new Avalonia.Thickness(0,0,3,0),
                    Padding = new Avalonia.Thickness(0)
                };

                DockPanel.SetDock(isIncludedCheckBox, Avalonia.Controls.Dock.Right);

                mainDockPanel.Children.Add(isIncludedCheckBox);
                mainDockPanel.Children.Add(percentageNumericUpDown);
                return mainDockPanel;
            });

            HeaderTemplate = new FuncDataTemplate<object>((itemModel, namescope) =>
            {
                var mainDockPanel = new DockPanel
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

                ToolTip.SetTip(mainDockPanel, $@"{m_DisplayName}");

                var titleTextBlock = new TextBlock
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0),
                    Padding = new Avalonia.Thickness(0),
                    Text = $@"{m_DisplayName}"
                };

                mainDockPanel.Children.Add(titleTextBlock);
                return mainDockPanel;
            });
        }
    }
}
