using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Styling;
using Ursa.Controls;
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
                        Background = Avalonia.Media.Brushes.Transparent,
                        [!TextBlock.TextProperty] = new Binding($@"Trackers.Day{m_Index:D2}.TargetResourceActivitiesString", BindingMode.OneWay),
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
                    [!ItemsControl.ItemsSourceProperty] = new Binding($@"Trackers.Day{m_Index:D2}.TargetResourceActivities", BindingMode.OneWay),
                    [!MultiComboBox.SelectedItemsProperty] = new Binding($@"Trackers.Day{m_Index:D2}.SelectedTargetResourceActivities", BindingMode.OneWay),
                    //[!ItemsControl.DisplayMemberBindingProperty] = new Binding($@"DisplayName", BindingMode.OneWay), // This didn't work.
                    IsDropDownOpen = true,
                };

                comboBox.Styles.Add(
                    new Style(x => x.OfType<MultiComboBoxItem>())
                    {
                        Setters =
                        {
                            new Setter(MultiComboBoxItem.IsSelectedProperty, new Binding($@"IsSelected", BindingMode.TwoWay)),
                        },
                    });

                comboBox.SelectedItemTemplate = new FuncDataTemplate<SelectableResourceActivityViewModel>((value, namescope) =>
                {
                    var templateGrid = new Grid
                    {
                        Background = Avalonia.Media.Brushes.White,
                    };

                    templateGrid.Children.Add(
                        new TextBlock
                        {
                            [!TextBlock.TextProperty] = new Binding($@"DisplayName", BindingMode.OneWay),
                        });
                    return templateGrid;
                });

                comboBox.ItemTemplate = new FuncDataTemplate<SelectableResourceActivityViewModel>((value, namescope) =>
                {
                    var templateGrid = new Grid
                    {
                        Background = Avalonia.Media.Brushes.White,
                    };

                    templateGrid.Children.Add(
                        new TextBlock
                        {
                            [!TextBlock.TextProperty] = new Binding($@"DisplayName", BindingMode.OneWay),
                        });
                    templateGrid.Children.Add(
                        new NumericIntUpDown
                        {
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            ShowButtonSpinner = false,
                            Foreground = Avalonia.Media.Brushes.Black,
                            Background = Avalonia.Media.Brushes.White,
                            Margin = new Avalonia.Thickness(0),
                            Padding = new Avalonia.Thickness(0),
                            Minimum = 0,
                            Maximum = 100,
                            //[!NumericIntUpDown.ValueProperty] = new Binding($@"Trackers.Day{m_Index:D2}", BindingMode.TwoWay)
                            //{
                            //    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                            //},
                        });
                    return templateGrid;
                });

                mainGrid.Children.Add(comboBox);

                return mainGrid;
            });

            CanUserResize = false;
            CanUserReorder = false;
            CanUserSort = false;
            Width = new DataGridLength(295);
            Header = header;
            CellTemplate = cellTemplate;
            CellEditingTemplate = cellEditingTemplate;
        }
    }
}
