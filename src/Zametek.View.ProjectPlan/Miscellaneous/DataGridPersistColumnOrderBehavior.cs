using Avalonia.Controls;
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

            // Load settings once the control is ready
            AssociatedObject.Initialized += OnInitialized;

            // Restore order when the DataGrid is loaded
            //AssociatedObject.Loaded += OnLoaded;

            // Save order whenever columns are reordered
            AssociatedObject.ColumnReordered += OnColumnReordered;

            // Listen for layout changes to capture user resizing
            AssociatedObject.LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject is not null)
            {
                AssociatedObject.Initialized -= OnInitialized;
                //AssociatedObject.Loaded -= OnLoaded;
                AssociatedObject.ColumnReordered -= OnColumnReordered;
                AssociatedObject.LayoutUpdated -= OnLayoutUpdated;
            }
            m_GridName = string.Empty;
            base.OnDetaching();
        }

        private void OnInitialized(
            object? sender,
            EventArgs e)
        {
            LoadSettings();
        }

        //private void OnLoaded(
        //    object? sender,
        //    RoutedEventArgs e)
        //{
        //    LoadSettings();
        //}

        private void LoadSettings()
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
                        column.Width = new DataGridLength(dataGridColumnModel.PixelWidth, DataGridLengthUnitType.Pixel);
                    }
                }
            }
        }

        private void OnColumnReordered(
            object? sender,
            DataGridColumnEventArgs e)
        {
            SaveSettings();
        }

        private void OnLayoutUpdated(
            object? sender,
            EventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
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
                        PixelWidth = dataGridColumnModel.ActualWidth,
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
