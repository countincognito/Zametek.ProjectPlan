using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using ReactiveUI;
using System;
using System.Collections;
using System.Reactive.Linq;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DataGridColumnSyncher
         : AvaloniaObject
    {
        static DataGridColumnSyncher()
        {
            ItemsSourceProperty.Changed.ObserveOn(RxApp.MainThreadScheduler).Subscribe(x => HandleItemsSourceChanged(x.Sender, x.NewValue.GetValueOrDefault<IEnumerable?>()));
            StartColumnIndexProperty.Changed.ObserveOn(RxApp.MainThreadScheduler).Subscribe(x => HandleStartColumnIndexChanged(x.Sender, x.NewValue.GetValueOrDefault<int?>()));
            EndColumnIndexProperty.Changed.ObserveOn(RxApp.MainThreadScheduler).Subscribe(x => HandleEndColumnIndexChanged(x.Sender, x.NewValue.GetValueOrDefault<int?>()));
            ColumnTypeProperty.Changed.ObserveOn(RxApp.MainThreadScheduler).Subscribe(x => HandleColumnTypeChanged(x.Sender, x.NewValue.GetValueOrDefault<Type?>()));
        }



        public static readonly AttachedProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, IEnumerable?>("ItemsSource", default, false, BindingMode.OneWay);

        public static void SetItemsSource(AvaloniaObject element, IEnumerable? value)
        {
            element.SetValue(ItemsSourceProperty, value);
        }

        public static IEnumerable? GetItemsSource(AvaloniaObject element)
        {
            return element.GetValue(ItemsSourceProperty);
        }

        private static void HandleItemsSourceChanged(AvaloniaObject element, IEnumerable? newValue)
        {
            if (element is DataGrid dg)
            {
                int? initialColumnCount = GetInitialCount(dg);

                if (initialColumnCount is null)
                {
                    initialColumnCount = dg.Columns.Count;
                    SetInitialCount(dg, initialColumnCount);
                }

                // Now set the Items value.
                dg.ItemsSource = null;

                if (newValue is IEnumerable newItemsSource)
                {
                    IEnumerable? nextItemsSource = newItemsSource;

                    newItemsSource.TypeSwitchOn()
                        .Case<DataGridCollectionView>(x => nextItemsSource = x.SourceCollection);

                    if (nextItemsSource is not null)
                    {
                        var dv = new DataGridCollectionView(nextItemsSource);
                        dg.ItemsSource = dv;
                        dv?.Refresh();
                    }
                }
            }
        }



        public static readonly AttachedProperty<int?> StartColumnIndexProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, int?>("StartColumnIndex", default, false, BindingMode.OneWay);

        public static void SetStartColumnIndex(AvaloniaObject element, int? value)
        {
            element.SetValue(StartColumnIndexProperty, value);
        }

        public static int? GetStartColumnIndex(AvaloniaObject element)
        {
            return element.GetValue(StartColumnIndexProperty);
        }

        private static void HandleStartColumnIndexChanged(AvaloniaObject element, int? newValue)
        {
            if (element is DataGrid dg)
            {
                int? endColumnIndex = GetEndColumnIndex(dg);
                UpdateColumns(dg, newValue, endColumnIndex);
            }
        }



        public static readonly AttachedProperty<int?> EndColumnIndexProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, int?>("EndColumnIndex", default, false, BindingMode.OneWay);

        public static void SetEndColumnIndex(AvaloniaObject element, int? value)
        {
            element.SetValue(EndColumnIndexProperty, value);
        }

        public static int? GetEndColumnIndex(AvaloniaObject element)
        {
            return element.GetValue(EndColumnIndexProperty);
        }

        private static void HandleEndColumnIndexChanged(AvaloniaObject element, int? newValue)
        {
            if (element is DataGrid dg)
            {
                int? startColumnIndex = GetStartColumnIndex(dg);
                UpdateColumns(dg, startColumnIndex, newValue);
            }
        }



        private static void UpdateColumns(
            DataGrid dg,
            int? startColumnIndex,
            int? endColumnIndex)
        {
            ArgumentNullException.ThrowIfNull(dg);
            int initialColumnCount = GetInitialCount(dg).GetValueOrDefault();
            int currentColumnCount = dg.Columns.Count;

            Type? columnType = GetColumnType(dg);
            IDateTimeCalculator? dateTimeCalculator = GetDateTimeCalculator(dg);
            DateTimeOffset? projectStart = GetProjectStart(dg);
            bool showDates = GetShowDates(dg);

            IEnumerable? oldItemsSource = GetItemsSource(dg);
            SetItemsSource(dg, null);

            if (columnType is not null)
            {
                // Remove all non-initial columns.
                for (int i = currentColumnCount; i > initialColumnCount; i--)
                {
                    int indexOfColumnToRemove = i - 1;
                    dg.Columns.RemoveAt(indexOfColumnToRemove);
                }

                if (startColumnIndex is not null
                    && endColumnIndex is not null
                    && endColumnIndex >= startColumnIndex)
                {
                    // Add new columns
                    for (int i = startColumnIndex.GetValueOrDefault(); i <= endColumnIndex.GetValueOrDefault(); i++)
                    {
                        int indexOfNewColumn = i;
                        string displayName = $@"{i}";

                        if (dateTimeCalculator is not null
                            && projectStart is not null
                            && showDates)
                        {
                            displayName = dateTimeCalculator
                                .AddDays(projectStart.GetValueOrDefault(), i)
                                .ToString(DateTimeCalculator.DateFormat);
                        }

                        DataGridColumn column = (DataGridColumn)Activator.CreateInstance(columnType, indexOfNewColumn, displayName)!;
                        dg.Columns.Add(column);
                    }
                }
            }

            SetItemsSource(dg, oldItemsSource);
        }



        public static readonly AttachedProperty<Type?> ColumnTypeProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, Type?>("ColumnType", default, false, BindingMode.OneTime);

        public static void SetColumnType(AvaloniaObject element, Type? value)
        {
            element.SetValue(ColumnTypeProperty, value);
        }

        public static Type? GetColumnType(AvaloniaObject element)
        {
            return element.GetValue(ColumnTypeProperty);
        }

        private static void HandleColumnTypeChanged(AvaloniaObject element, Type? newValue)
        {
            // Check columnTypeValue is actually a DataGridColumn.
            if (newValue is not null
                && !typeof(DataGridColumn).IsAssignableFrom(newValue))
            {
                throw new ArgumentException(Resource.ProjectPlan.Messages.Message_ColumnTypeMustBeDerivedFromDataGridColumn);
            }
        }



        public static readonly AttachedProperty<IDateTimeCalculator?> DateTimeCalculatorProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, IDateTimeCalculator?>("DateTimeCalculator", default, false, BindingMode.OneTime);

        public static void SetDateTimeCalculator(AvaloniaObject element, IDateTimeCalculator? value)
        {
            element.SetValue(DateTimeCalculatorProperty, value);
        }

        public static IDateTimeCalculator? GetDateTimeCalculator(AvaloniaObject element)
        {
            return element.GetValue(DateTimeCalculatorProperty);
        }



        public static readonly AttachedProperty<bool> ShowDatesProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, bool>("ShowDates", default, false, BindingMode.OneWay);

        public static void SetShowDates(AvaloniaObject element, bool value)
        {
            element.SetValue(ShowDatesProperty, value);
        }

        public static bool GetShowDates(AvaloniaObject element)
        {
            return element.GetValue(ShowDatesProperty);
        }



        public static readonly AttachedProperty<DateTimeOffset?> ProjectStartProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, DateTimeOffset?>("ProjectStart", default, false, BindingMode.OneWay);

        public static void SetProjectStart(AvaloniaObject element, DateTimeOffset? value)
        {
            element.SetValue(ProjectStartProperty, value);
        }

        public static DateTimeOffset? GetProjectStart(AvaloniaObject element)
        {
            return element.GetValue(ProjectStartProperty);
        }



        private static readonly AttachedProperty<int?> InitialCountProperty = AvaloniaProperty
            .RegisterAttached<DataGridColumnSyncher, DataGrid, int?>("InitialCount", default, false);

        private static void SetInitialCount(AvaloniaObject element, int? value)
        {
            element.SetValue(InitialCountProperty, value);
        }

        private static int? GetInitialCount(AvaloniaObject element)
        {
            return element.GetValue(InitialCountProperty);
        }
    }
}
