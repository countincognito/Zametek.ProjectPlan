using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Zametek.Common.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    // Shared pieces for building clipboard/text output from DataGrid content.
    public static class DataGridHelper
    {
        // The BCL offers no named constants for these characters (Environment.NewLine
        // is the platform newline *string*), so name them here.
        public const char Tab = '\t';
        public const char CarriageReturn = '\r';
        public const char LineFeed = '\n';
        public const char Space = ' ';

        // Dates are copied in ISO format: unambiguous, invariant, and parsed by
        // spreadsheets regardless of locale.
        private const string c_DateFormat = "yyyy-MM-dd";

        // Copy a table to the clipboard as tab-separated text: a header row followed
        // by one row per item, columns in their current display (drag) order with
        // hidden columns (e.g. cost or billing groups) omitted - so the copy matches
        // the visible grid shape while carrying raw values. Column definitions are
        // keyed by markup declaration order, so each grid's definition list must stay
        // in sync with its column markup. Best-effort: reports rather than throws if
        // a clipboard backend cannot accept the text.
        public static async Task CopyTableToClipboardAsync<T>(
            DataGrid dataGrid,
            IReadOnlyList<(string Header, Func<T, object?> Value)> columnDefinitions,
            IEnumerable<T> rows,
            Func<string, Task> reportErrorAsync)
        {
            ArgumentNullException.ThrowIfNull(dataGrid);
            ArgumentNullException.ThrowIfNull(columnDefinitions);
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(reportErrorAsync);

            try
            {
                List<(int DisplayIndex, string Header, Func<T, object?> Value)> visibleColumns = [];

                int columnCount = Math.Min(dataGrid.Columns.Count, columnDefinitions.Count);

                for (int i = 0; i < columnCount; i++)
                {
                    DataGridColumn column = dataGrid.Columns[i];

                    if (!column.IsVisible)
                    {
                        continue;
                    }

                    (string header, Func<T, object?> value) = columnDefinitions[i];
                    visibleColumns.Add((column.DisplayIndex, header, value));
                }

                visibleColumns.Sort(static (a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

                List<string> lines =
                [
                    string.Join(Tab, visibleColumns.Select(static x => EscapeCellText(x.Header))),
                ];

                foreach (T row in rows)
                {
                    lines.Add(string.Join(Tab, visibleColumns.Select(x => FormatCellValue(x.Value(row)))));
                }

                await WriteTextToClipboardAsync(dataGrid, string.Join(Environment.NewLine, lines));
            }
            catch
            {
                // Best-effort: never crash if a clipboard backend cannot accept the text.
                await reportErrorAsync(Resource.ProjectPlan.Messages.Message_ClipboardCopyFailed);
            }
        }

        // Copy a prebuilt table to the clipboard as tab-separated text: a header row
        // followed by the given rows. Used by panels that are not DataGrids (e.g. the
        // Metrics panel), so there is no column visibility or display order to apply -
        // callers supply exactly the rows and cells to copy. Best-effort like the
        // DataGrid overload.
        public static async Task CopyTableToClipboardAsync(
            Visual anchor,
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<object?>> rows,
            Func<string, Task> reportErrorAsync)
        {
            ArgumentNullException.ThrowIfNull(anchor);
            ArgumentNullException.ThrowIfNull(headers);
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(reportErrorAsync);

            try
            {
                List<string> lines =
                [
                    string.Join(Tab, headers.Select(static x => EscapeCellText(x))),
                ];

                foreach (IReadOnlyList<object?> row in rows)
                {
                    lines.Add(string.Join(Tab, row.Select(static x => FormatCellValue(x))));
                }

                await WriteTextToClipboardAsync(anchor, string.Join(Environment.NewLine, lines));
            }
            catch
            {
                // Best-effort: never crash if a clipboard backend cannot accept the text.
                await reportErrorAsync(Resource.ProjectPlan.Messages.Message_ClipboardCopyFailed);
            }
        }

        private static async Task WriteTextToClipboardAsync(Visual anchor, string text)
        {
            IClipboard? clipboard = TopLevel.GetTopLevel(anchor)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            var item = new DataTransferItem();
            item.SetText(text);
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(item);
            await clipboard.SetDataAsync(dataTransfer);
        }

        // Raw, invariant-culture cell values (not display-formatted), so copied
        // tables are calculation-friendly when pasted into a spreadsheet. The
        // per-type conversions mirror the xlsx exporter's AddToCell rules (e.g.
        // colors copy as HTML hex codes, enums by name).
        public static string FormatCellValue(object? value) =>
            value switch
            {
                null => string.Empty,
                double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
                int intValue => intValue.ToString(CultureInfo.InvariantCulture),
                bool boolValue => boolValue.ToString(),
                DateTime dateTimeValue => dateTimeValue.ToString(c_DateFormat, CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffsetValue => dateTimeOffsetValue.ToString(c_DateFormat, CultureInfo.InvariantCulture),
                ColorFormatModel colorFormatValue => ColorHelper.ColorFormatToHtmlHexCode(colorFormatValue),
                Enum enumValue => enumValue.ToString(),
                string stringValue => EscapeCellText(stringValue),
                _ => EscapeCellText(value.ToString() ?? string.Empty),
            };

        // Cell content can be free text: strip the characters that would corrupt
        // a tab-separated layout.
        public static string EscapeCellText(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.Replace(Tab, Space).Replace(CarriageReturn, Space).Replace(LineFeed, Space);
        }
    }
}
