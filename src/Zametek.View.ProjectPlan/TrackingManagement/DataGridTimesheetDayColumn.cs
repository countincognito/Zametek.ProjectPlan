using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Ursa.Controls;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // One editable day column for a resource timesheet grid. The grid's
    // DataContext is an IResourceTimesheetViewModel (which forwards the
    // shared day titles) and each row is an IResourceTimesheetRowViewModel,
    // whose cells drive the resource tracker write path.
    public class DataGridTimesheetDayColumn
        : DataGridTemplateColumn
    {
        private readonly int m_Index;

        public DataGridTimesheetDayColumn(int index)
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
                    [!TextBlock.TextProperty] = new ReflectionBinding($@"{nameof(IResourceTimesheetViewModel.DayTitles)}[{m_Index}].{nameof(IDayTitleViewModel.Title)}")
                    {
                        Mode = BindingMode.OneWay,
                    },
                    [!ToolTip.TipProperty] = new ReflectionBinding($@"{nameof(IResourceTimesheetViewModel.DayTitles)}[{m_Index}].{nameof(IDayTitleViewModel.Title)}")
                    {
                        Mode = BindingMode.OneWay,
                    },
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
                        [!TextBlock.TextProperty] = new ReflectionBinding($@"{nameof(IResourceTimesheetRowViewModel.Cells)}[{m_Index}].{nameof(ITimesheetCellViewModel.PercentageWorked)}")
                        {
                            Mode = BindingMode.OneWay,
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
                        Maximum = 200,
                        [!NumericIntUpDown.ValueProperty] = new ReflectionBinding($@"{nameof(IResourceTimesheetRowViewModel.Cells)}[{m_Index}].{nameof(ITimesheetCellViewModel.PercentageWorked)}")
                        {
                            Mode = BindingMode.TwoWay,
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
