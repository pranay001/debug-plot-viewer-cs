using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DebugPlotViewer.ViewModels
{
    /// <summary>
    /// Represents a single named plot (mirrors LabVIEW GraphPlot actor).
    /// Owns the OxyPlot model and knows how to update it from incoming CSV data.
    /// </summary>
    public class PlotViewModel : ViewModelBase
    {
        private PlotModel _plotModel;

        public string PlotName { get; }

        public PlotModel PlotModel
        {
            get => _plotModel;
            private set => SetProperty(ref _plotModel, value);
        }

        public PlotViewModel(string plotName)
        {
            PlotName = plotName;
            PlotModel = BuildEmptyModel();
        }

        /// <summary>
        /// Update the chart from a CSV payload.
        /// Mirrors LabVIEW's GraphPlot:UpdatePlotData + GraphPlot:ActorCore Data event.
        /// </summary>
        public void UpdateData(string csvData, List<string> labels)
        {
            double[][] columns = ParseCsvToColumns(csvData);

            var model = new PlotModel { Title = PlotName };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Samples" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Data" });

            for (int i = 0; i < columns.Length; i++)
            {
                string seriesTitle = i < labels.Count ? labels[i] : $"Series {i + 1}";
                var series = new LineSeries { Title = seriesTitle };

                double[] col = columns[i];
                for (int j = 0; j < col.Length; j++)
                    series.Points.Add(new DataPoint(j, col[j]));

                model.Series.Add(series);
            }

            PlotModel = model;
        }

        /// <summary>
        /// Parse LabVIEW-style CSV into columns.
        /// LabVIEW Spreadsheet String To Array uses comma delimiter; rows are newline-separated.
        /// Each column corresponds to one PlotLabel entry (one waveform series).
        /// </summary>
        public static double[][] ParseCsvToColumns(string csvData)
        {
            if (string.IsNullOrWhiteSpace(csvData))
                return Array.Empty<double[]>();

            var rows = csvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (rows.Length == 0)
                return Array.Empty<double[]>();

            var firstCells = rows[0].Split(',');
            int numCols = firstCells.Length;

            var columns = new List<double>[numCols];
            for (int c = 0; c < numCols; c++)
                columns[c] = new List<double>();

            foreach (var row in rows)
            {
                var cells = row.Split(',');
                for (int c = 0; c < numCols && c < cells.Length; c++)
                {
                    if (double.TryParse(cells[c].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double val))
                        columns[c].Add(val);
                }
            }

            return columns.Select(c => c.ToArray()).ToArray();
        }

        private PlotModel BuildEmptyModel()
        {
            var model = new PlotModel { Title = PlotName };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Samples" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Data" });
            return model;
        }
    }
}
