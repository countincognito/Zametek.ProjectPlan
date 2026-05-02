using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DataGridPersistColumnOrderBehavior
        : Behavior<DataGrid>
    {
        private readonly ISettingService m_SettingService;
        private string m_GridName = string.Empty;

        public DataGridPersistColumnOrderBehavior(ISettingService settingService)
        {
            ArgumentNullException.ThrowIfNull(settingService);
            m_SettingService = settingService;
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            string gridName = AssociatedObject?.Name ?? string.Empty;

            if (AssociatedObject is null
                || string.IsNullOrEmpty(gridName))
            {
                return;
            }

            m_GridName = gridName;

            // Restore order when the DataGrid is loaded
            AssociatedObject.Loaded += OnDataGridLoaded;

            // Save order whenever columns are reordered
            AssociatedObject.ColumnReordered += OnColumnReordered;
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject is not null)
            {
                AssociatedObject.Loaded -= OnDataGridLoaded;
                AssociatedObject.ColumnReordered -= OnColumnReordered;
            }
            m_GridName = string.Empty;
            base.OnDetaching();
        }


        private void OnDataGridLoaded(
            object? sender,
            RoutedEventArgs e)
        {
            DataGridModel gridModel = m_SettingService.GetDataGridLayout(m_GridName);
            Dictionary<int, DataGridColumnModel> modelMap = gridModel.Columns.ToDictionary(x => x.PositionIndex, x => x);

            if (AssociatedObject is not null
                && AssociatedObject.Columns.Count != 0)
            {
                for (int i = 0; i < AssociatedObject.Columns.Count; i++)
                {
                    DataGridColumn column = AssociatedObject.Columns[i];
                    if (modelMap.TryGetValue(i, out DataGridColumnModel? dataGridColumnModel))
                    {
                        column.DisplayIndex = dataGridColumnModel.DisplayIndex;
                    }
                }
            }
        }

        private void OnColumnReordered(
            object? sender,
            DataGridColumnEventArgs e)
        {
            if (AssociatedObject is not null
                && AssociatedObject.Columns.Count != 0)
            {
                Dictionary<int, DataGridColumnModel> modelMap = [];

                for (int i = 0; i < AssociatedObject.Columns.Count; i++)
                {
                    DataGridColumn dataGridColumnModel = AssociatedObject.Columns[i];
                    modelMap[i] = new DataGridColumnModel
                    {
                        PositionIndex = i,
                        DisplayIndex = dataGridColumnModel.DisplayIndex,
                    };
                }

                DataGridModel dataGridModel = new()
                {
                    Name = m_GridName,
                    Columns = [.. modelMap.Values],
                };

                m_SettingService.SetDataGridLayout(m_GridName, dataGridModel);
            }
        }
    }
}
