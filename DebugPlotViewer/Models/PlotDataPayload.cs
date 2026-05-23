using Newtonsoft.Json;
using System.Collections.Generic;

namespace DebugPlotViewer.Models
{
    /// <summary>
    /// JSON payload carried inside a PlotDataRequest.data string.
    /// Matches the LabVIEW cluster: {Plot Name, Plot Label, Data, Plot Type}.
    /// </summary>
    public class PlotDataPayload
    {
        [JsonProperty("Plot Name")]
        public string PlotName { get; set; } = string.Empty;

        /// <summary>One label per data column (channel/series name).</summary>
        [JsonProperty("Plot Label")]
        public List<string> PlotLabel { get; set; } = new List<string>();

        /// <summary>
        /// CSV-formatted numeric data.
        /// Rows are separated by newlines; columns (channels) by commas.
        /// Each column maps to the corresponding entry in PlotLabel.
        /// </summary>
        [JsonProperty("Data")]
        public string Data { get; set; } = string.Empty;

        /// <summary>"Graph" → waveform graph; other values fall back to base plot.</summary>
        [JsonProperty("Plot Type")]
        public string PlotType { get; set; } = "Graph";
    }
}
