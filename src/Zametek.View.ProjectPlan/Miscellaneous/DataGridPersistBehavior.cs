using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class DataGridPersistBehavior
        : Behavior<DataGrid>
    {
        private readonly Lock m_Lock;
        private string m_GridName;
        private DataGridModel m_OriginalGridModelModel;
        private readonly IDataGridManager m_DataGridManager;
        private DataGrid? m_DataGrid;
        private bool m_IsInitialized;

        public DataGridPersistBehavior(IDataGridManager dataGridManager)
        {
            m_DataGridManager = dataGridManager ?? throw new ArgumentNullException(nameof(dataGridManager));
            m_Lock = new Lock();
            m_GridName = string.Empty;
            m_DataGrid = null;
            m_OriginalGridModelModel = new();
            m_IsInitialized = false;
            m_DataGridManager.ResetActions.Add(ResetDataGridModel);
        }

        private DataGridModel SaveDataGridModel()
        {
            lock (m_Lock)
            {
                List<DataGridColumnModel> columnModels = [];

                if (m_DataGrid is not null
                    && m_DataGrid.Columns.Count != 0)
                {
                    for (int i = 0; i < m_DataGrid.Columns.Count; i++)
                    {
                        DataGridColumn column = m_DataGrid.Columns[i];

                        var columnModel = new DataGridColumnModel
                        {
                            Name = column.Header?.ToString() ?? string.Empty,
                            PositionIndex = i,
                            DisplayIndex = column.DisplayIndex,
                            PixelWidth = column.ActualWidth,
                        };
                        columnModels.Add(columnModel);
                    }
                }

                return new DataGridModel
                {
                    Name = m_GridName,
                    Columns = columnModels,
                };
            }
        }

        private void LoadDataGridModel(DataGridModel dataGridModel)
        {
            lock (m_Lock)
            {
                if (m_DataGrid is not null
                    && m_DataGrid.Columns.Count != 0)
                {
                    Dictionary<int, DataGridColumnModel> modelMap = dataGridModel
                        .Columns
                        .ToDictionary(x => x.PositionIndex, x => x);

                    for (int i = 0; i < m_DataGrid.Columns.Count; i++)
                    {
                        DataGridColumn column = m_DataGrid.Columns[i];
                        if (modelMap.TryGetValue(i, out DataGridColumnModel? dataGridColumnModel))
                        {
                            column.DisplayIndex = dataGridColumnModel.DisplayIndex;
                            column.Width = new DataGridLength(dataGridColumnModel.PixelWidth, DataGridLengthUnitType.Pixel);
                        }
                    }
                }
            }
        }

        private void ResetDataGridModel()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                lock (m_Lock)
                {
                    LoadDataGridModel(m_OriginalGridModelModel);
                }
            });
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            m_DataGrid = AssociatedObject;
            m_GridName = m_DataGrid?.Name ?? string.Empty;

            if (m_DataGrid is null
                || string.IsNullOrEmpty(m_GridName))
            {
                return;
            }

            if (!m_IsInitialized)
            {
                m_OriginalGridModelModel = SaveDataGridModel();
                m_IsInitialized = true;
            }

            // Load settings once the control is ready
            m_DataGrid.Initialized += OnInitialized;

            // Restore order when the DataGrid is loaded
            m_DataGrid.Loaded += OnLoaded;

            // Save order whenever columns are reordered
            m_DataGrid.ColumnReordered += OnColumnReordered;

            // Listen for layout changes to capture user resizing
            m_DataGrid.LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnDetaching()
        {
            if (m_DataGrid is not null)
            {
                m_DataGrid.Initialized -= OnInitialized;
                m_DataGrid.Loaded -= OnLoaded;
                m_DataGrid.ColumnReordered -= OnColumnReordered;
                m_DataGrid.LayoutUpdated -= OnLayoutUpdated;
            }
            m_GridName = string.Empty;
            base.OnDetaching();
        }

        private void LoadPersistedDataGridModel()
        {
            lock (m_Lock)
            {
                DataGridModel dataGridModel = m_DataGridManager.GetDataGridModel(m_GridName);

                if (dataGridModel is not null)
                {
                    LoadDataGridModel(dataGridModel);
                }
            }
        }

        private void SavePersistedDataGridModel()
        {
            lock (m_Lock)
            {
                DataGridModel dataGridModel = SaveDataGridModel();

                if (dataGridModel is not null)
                {
                    m_DataGridManager.SetDataGridModel(dataGridModel);
                }
            }
        }

        private void OnInitialized(
            object? sender,
            EventArgs e)
        {
            LoadPersistedDataGridModel();
        }

        private void OnLoaded(
            object? sender,
            RoutedEventArgs e)
        {
            LoadPersistedDataGridModel();
        }

        private void OnColumnReordered(
            object? sender,
            DataGridColumnEventArgs e)
        {
            SavePersistedDataGridModel();
        }

        private void OnLayoutUpdated(
            object? sender,
            EventArgs e)
        {
            SavePersistedDataGridModel();
        }
    }
}
