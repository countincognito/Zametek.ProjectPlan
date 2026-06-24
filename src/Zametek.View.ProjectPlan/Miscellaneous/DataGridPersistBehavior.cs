using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
        private readonly Dictionary<DataGridColumn, string> m_HeaderTextCache;
        private DataGrid? m_DataGrid;
        private bool m_IsInitialized;

        public DataGridPersistBehavior(IDataGridManager dataGridManager)
        {
            m_DataGridManager = dataGridManager ?? throw new ArgumentNullException(nameof(dataGridManager));
            m_Lock = new Lock();
            m_GridName = string.Empty;
            m_DataGrid = null;
            m_OriginalGridModelModel = new();
            m_HeaderTextCache = [];
            m_IsInitialized = false;
            m_DataGridManager.ResetActions.Add(ResetDataGridModel);
        }

        // DataGridTemplateColumns set only a HeaderTemplate (a DataTemplate containing a
        // TextBlock bound to a static label), never the Header property itself, so
        // column.Header?.ToString() yields nothing usable. Build the header template and
        // read the realised text so the display header can be used as a persistence key.
        // Results are cached per column because the header text is fixed for the lifetime
        // of the grid and SaveDataGridModel runs on every LayoutUpdated.
        private string GetColumnHeaderText(DataGridColumn column)
        {
            if (m_HeaderTextCache.TryGetValue(column, out string? cached))
            {
                return cached;
            }

            string headerText = ExtractColumnHeaderText(column);
            m_HeaderTextCache[column] = headerText;
            return headerText;
        }

        private static string ExtractColumnHeaderText(DataGridColumn column)
        {
            // A plainly-set string header takes precedence.
            if (column.Header is string headerString
                && !string.IsNullOrWhiteSpace(headerString))
            {
                return headerString;
            }

            // Otherwise realise the header template and find the first non-empty TextBlock.
            if (column.HeaderTemplate is IDataTemplate headerTemplate)
            {
                Control? built = headerTemplate.Build(null);

                if (built is TextBlock rootTextBlock
                    && !string.IsNullOrWhiteSpace(rootTextBlock.Text))
                {
                    return rootTextBlock.Text;
                }

                string? nestedText = built?
                    .GetLogicalDescendants()
                    .OfType<TextBlock>()
                    .Select(x => x.Text)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (!string.IsNullOrWhiteSpace(nestedText))
                {
                    return nestedText;
                }
            }

            return column.Header?.ToString() ?? string.Empty;
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
                            Name = GetColumnHeaderText(column),
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
                    // Match persisted columns by their display header name first. Only names
                    // that are non-empty and unambiguous (appear once) are usable as keys.
                    Dictionary<string, DataGridColumnModel> nameMap = dataGridModel
                        .Columns
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                        .GroupBy(x => x.Name)
                        .Where(x => x.Count() == 1)
                        .ToDictionary(x => x.Key, x => x.First());

                    // Fall back to the position index for columns without a usable name
                    // (e.g. spacer columns) or whose name no longer matches (e.g. older
                    // layout files, renamed labels, or a language change).
                    Dictionary<int, DataGridColumnModel> positionMap = dataGridModel
                        .Columns
                        .GroupBy(x => x.PositionIndex)
                        .ToDictionary(x => x.Key, x => x.First());

                    // Ensure each persisted column (identified by its unique PositionIndex)
                    // is applied to at most one live column, so a name match never collides
                    // with a later position fallback.
                    HashSet<int> consumed = [];

                    for (int i = 0; i < m_DataGrid.Columns.Count; i++)
                    {
                        DataGridColumn column = m_DataGrid.Columns[i];
                        string headerText = GetColumnHeaderText(column);

                        DataGridColumnModel? dataGridColumnModel = null;

                        // Prefer a unique name match; otherwise fall back to the position
                        // index. consumed.Add guards against applying a persisted column twice.
                        if (!string.IsNullOrWhiteSpace(headerText)
                            && nameMap.TryGetValue(headerText, out DataGridColumnModel? byName)
                            && consumed.Add(byName.PositionIndex))
                        {
                            dataGridColumnModel = byName;
                        }
                        else if (positionMap.TryGetValue(i, out DataGridColumnModel? byPosition)
                            && consumed.Add(byPosition.PositionIndex))
                        {
                            dataGridColumnModel = byPosition;
                        }

                        if (dataGridColumnModel is not null)
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
            m_HeaderTextCache.Clear();
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
