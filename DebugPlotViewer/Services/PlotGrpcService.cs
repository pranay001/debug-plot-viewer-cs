using DebugPlotViewer.Models;
using DebugPlotViewer.Protos;
using Grpc.Core;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace DebugPlotViewer.Services
{
    /// <summary>
    /// gRPC server-side implementation of the TSDebugPlot.Plot service.
    /// Mirrors the LabVIEW Plot gRPC_server Run Service + Plot.lvclass handler.
    /// </summary>
    public class PlotGrpcService : Plot.PlotBase
    {
        private readonly Action<PlotDataPayload> _onPlotDataReceived;

        public PlotGrpcService(Action<PlotDataPayload> onPlotDataReceived)
        {
            _onPlotDataReceived = onPlotDataReceived ?? throw new ArgumentNullException(nameof(onPlotDataReceived));
        }

        public override Task<PlotDataReply> PlotData(PlotDataRequest request, ServerCallContext context)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<PlotDataPayload>(request.Data);
                if (payload != null)
                    _onPlotDataReceived(payload);

                return Task.FromResult(new PlotDataReply { Message = string.Empty });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PlotDataReply { Message = $"Error: {ex.Message}" });
            }
        }
    }
}
