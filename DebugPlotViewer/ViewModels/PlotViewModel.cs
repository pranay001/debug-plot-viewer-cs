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
            // In LabVIEW, UpdatePlotData.vi indexes the 2D array by row in a ForLoop alongside
            // PlotLabel — so row i is the complete time-series for PlotLabel[i].
            // CSV layout: each ROW = one series; columns within that row = samples.
            double[][] seriesRows = ParseCsvToRows(csvData);

            var model = new PlotModel { Title = PlotName };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Samples" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Data" });

            for (int i = 0; i < seriesRows.Length; i++)
            {
                string seriesTitle = i < labels.Count ? labels[i] : $"Series {i + 1}";
                var series = new LineSeries { Title = seriesTitle };

                double[] samples = seriesRows[i];
                for (int j = 0; j < samples.Length; j++)
                    series.Points.Add(new DataPoint(j, samples[j]));

                model.Series.Add(series);
            }

            PlotModel = model;
        }

        /// <summary>
        /// Parse LabVIEW-style CSV into rows where each row is one waveform series.
        /// LabVIEW's Spreadsheet String To Array (comma delimiter) produces a 2D array
        /// where row i holds all samples for PlotLabel[i].
        /// </summary>
        public static double[][] ParseCsvToRows(string csvData)
        {
            if (string.IsNullOrWhiteSpace(csvData))
                return Array.Empty<double[]>();

            var lines = csvData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<double[]>(lines.Length);

            foreach (var line in lines)
            {
                var cells = line.Split(',');
                var values = new double[cells.Length];
                for (int c = 0; c < cells.Length; c++)
                    double.TryParse(cells[c].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out values[c]);
                result.Add(values);
            }

            return result.ToArray();
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
