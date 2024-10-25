using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Ursa.Controls;
using Zametek.Contract.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DataGridResourceTrackingColumn
        : DataGridTemplateColumn
    {
        private readonly int m_Index;

        public DataGridResourceTrackingColumn(int index)
        {
            m_Index = index;

            var header = new Grid();
            header.Children.Add(
                new TextBlock
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Avalonia.Thickness(0),
                    Padding = new Avalonia.Thickness(0),
                    [!TextBlock.TextProperty] = new Binding($@"Day{m_Index:D2}Title", BindingMode.OneWay),
                    [!ToolTip.TipProperty] = new Binding($@"Day{m_Index:D2}Title", BindingMode.OneWay)
                });

            var cellTemplate = new FuncDataTemplate<object>((itemModel, namescope) =>
            {
                var mainGrid = new Grid
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };
                mainGrid.Classes.Add("editable");

                mainGrid.Children.Add(
                    new TextBlock
                    {
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        TextAlignment = Avalonia.Media.TextAlignment.Left,
                        Margin = new Avalonia.Thickness(0),
                        Padding = new Avalonia.Thickness(3),
                        [!TextBlock.TextProperty] = new Binding($@"{nameof(IManagedResourceViewModel.TrackerSet)}.Day{m_Index:D2}.{nameof(IResourceActivitySelectorViewModel.TargetResourceActivitiesString)}", BindingMode.OneWay),
                        [!ToolTip.TipProperty] = new Binding($@"{nameof(IManagedResourceViewModel.TrackerSet)}.Day{m_Index:D2}.{nameof(IResourceActivitySelectorViewModel.TargetResourceActivitiesString)}", BindingMode.OneWay),
                    });

                return mainGrid;
            });

            var cellEditingTemplate = new FuncDataTemplate<object>((itemModel, namescope) =>
            {
                var mainGrid = new Grid
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                var comboBox = new MultiComboBox
                {
                    MaxHeight = 200,
                    Width = double.NaN,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    [!ItemsControl.ItemsSourceProperty] = new Binding($@"{nameof(IManagedResourceViewModel.TrackerSet)}.Day{m_Index:D2}.{nameof(IResourceActivitySelectorViewModel.TargetResourceActivities)}", BindingMode.OneWay),
                    [!MultiComboBox.SelectedItemsProperty] = new Binding($@"{nameof(IManagedResourceViewModel.TrackerSet)}.Day{m_Index:D2}.{nameof(IResourceActivitySelectorViewModel.SelectedTargetResourceActivities)}", BindingMode.OneWay),
                    //[!ItemsControl.DisplayMemberBindingProperty] = new Binding($@"DisplayName", BindingMode.OneWay), // This didn't work.
                    IsDropDownOpen = true,
                };

                comboBox.SelectedItemTemplate = new FuncDataTemplate<SelectableResourceActivityViewModel>((value, namescope) =>
                {
                    var templateGrid = new Grid
                    {
                    };

                    templateGrid.Children.Add(
                        new TextBlock
                        {
                            [!TextBlock.TextProperty] = new Binding(nameof(ISelectableResourceActivityViewModel.Id), BindingMode.OneWay),
                        });
                    return templateGrid;
                });

                comboBox.ItemTemplate = new FuncDataTemplate<SelectableResourceActivityViewModel>((value, namescope) =>
                {
                    var templatePanel = new DockPanel
                    {
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    };

                    templatePanel.Children.Add(
                        new NumericIntUpDown
                        {
                            [DockPanel.DockProperty] = Avalonia.Controls.Dock.Left,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                            ShowButtonSpinner = false,
                            Width = 40,
                            MaxWidth = 40,
                            Margin = new Avalonia.Thickness(0),
                            Padding = new Avalonia.Thickness(0),
                            Minimum = 0,
                            Maximum = 100,
                            [!NumericIntUpDown.ValueProperty] = new Binding(nameof(ISelectableResourceActivityViewModel.PercentageWorked), BindingMode.TwoWay)
                            {
                                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                            },
                        });

                    templatePanel.Children.Add(
                        new TextBlock
                        {
                            [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right,
                            [!TextBlock.TextProperty] = new Binding(nameof(ISelectableResourceActivityViewModel.Id), BindingMode.OneWay),
                            Width = 35,
                            Padding = new Avalonia.Thickness(3, 0),
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                            [!ToolTip.TipProperty] = new Binding(nameof(ISelectableResourceActivityViewModel.Name), BindingMode.OneWay),
                        });

                    templatePanel.Children.Add(new Grid());

                    return templatePanel;
                });

                mainGrid.Children.Add(comboBox);

                return mainGrid;
            });

            CanUserResize = false;
            CanUserReorder = false;
            CanUserSort = false;
            Width = new DataGridLength(120);
            Header = header;
            CellTemplate = cellTemplate;
            CellEditingTemplate = cellEditingTemplate;
        }
    }
}
