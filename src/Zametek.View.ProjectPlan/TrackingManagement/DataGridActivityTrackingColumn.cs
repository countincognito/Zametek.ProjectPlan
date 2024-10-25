using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Ursa.Controls;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DataGridActivityTrackingColumn
        : DataGridTemplateColumn
    {
        private readonly int m_Index;

        public DataGridActivityTrackingColumn(int index)
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
                        [!TextBlock.TextProperty] = new Binding($@"{nameof(IManagedActivityViewModel.TrackerSet)}.Day{m_Index:D2}", BindingMode.OneWay)
                        {
                            StringFormat = @"{0:#0'%'}",
                        },
                    });

                return mainGrid;
            });

            var cellEditingTemplate = new FuncDataTemplate<object>((itemModel, namescope) =>
            {
                var mainGrid = new Grid
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                };

                mainGrid.Children.Add(
                    new NumericIntUpDown
                    {
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        ShowButtonSpinner = false,
                        Margin = new Avalonia.Thickness(0),
                        Padding = new Avalonia.Thickness(0),
                        Minimum = 0,
                        Maximum = 100,
                        [!NumericIntUpDown.ValueProperty] = new Binding($@"{nameof(IManagedActivityViewModel.TrackerSet)}.Day{m_Index:D2}", BindingMode.TwoWay)
                        {
                            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                        },
                    });

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
