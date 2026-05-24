using System;
using System.Collections.Generic;
using System.Data;

namespace DebugPlotViewer.ViewModels
{
    /// <summary>
    /// ViewModel for the "Table" plot type.
    /// Receives the same CSV payload as GraphPlot but displays it as a DataGrid
    /// where columns = PlotLabel entries and rows = sample indices (transposed from
    /// the raw CSV which has rows=series, columns=samples).
    /// </summary>
    public class TableViewModel : ViewModelBase
    {
        private DataView _tableData;

        public string PlotName { get; }

        /// <summary>DataView bound to the DataGrid; rebuilt on every UpdateData call.</summary>
        public DataView TableData
        {
            get => _tableData;
            private set => SetProperty(ref _tableData, value);
        }

        public TableViewModel(string plotName)
        {
            PlotName = plotName;
            // Start with an empty table so the DataGrid renders immediately
            TableData = new DataTable(plotName).DefaultView;
        }

        /// <summary>
        /// Rebuild the DataGrid contents from a CSV payload.
        /// CSV layout (same as Graph): row i = all samples for PlotLabel[i].
        /// Table display layout: columns = PlotLabel names, rows = sample index.
        /// </summary>
        public void UpdateData(string csvData, List<string> labels)
        {
            // Reuse the same row-parsing logic as PlotViewModel
            double[][] seriesRows = PlotViewModel.ParseCsvToRows(csvData);

            var table = new DataTable(PlotName);

            // First column: sample index
            table.Columns.Add("Sample #", typeof(int));

            // One column per label (guard against duplicate column names)
            foreach (string label in labels)
            {
                string colName = label;
                int suffix = 1;
                while (table.Columns.Contains(colName))
                    colName = $"{label}_{suffix++}";
                table.Columns.Add(colName, typeof(double));
            }

            // Determine sample count from the longest series
            int numSamples = 0;
            foreach (var row in seriesRows)
                if (row.Length > numSamples) numSamples = row.Length;

            // Fill rows (each row = one sample index across all series)
            for (int sampleIdx = 0; sampleIdx < numSamples; sampleIdx++)
            {
                DataRow row = table.NewRow();
                row["Sample #"] = sampleIdx;

                for (int si = 0; si < seriesRows.Length && si < labels.Count; si++)
                {
                    string colName = table.Columns[si + 1].ColumnName; // +1 for "Sample #"
                    row[colName] = sampleIdx < seriesRows[si].Length
                        ? (object)seriesRows[si][sampleIdx]
                        : DBNull.Value;
                }

                table.Rows.Add(row);
            }

            TableData = table.DefaultView;
        }
    }
}
