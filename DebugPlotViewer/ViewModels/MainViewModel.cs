using DebugPlotViewer.Models;
using DebugPlotViewer.Protos;
using DebugPlotViewer.Services;
using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace DebugPlotViewer.ViewModels
{
    /// <summary>
    /// Root ViewModel. Mirrors the LabVIEW PlotManager actor:
    ///   - Starts gRPC server
    ///   - Maintains a registry of PlotViewModels keyed by plot name
    ///   - Updates the UI plot list and routes data to the correct PlotViewModel
    /// Also owns the PlotUIDisplay responsibilities (server IP, plot selection, help).
    /// </summary>
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly ConcurrentDictionary<string, PlotViewModel> _plotRegistry
            = new ConcurrentDictionary<string, PlotViewModel>(StringComparer.OrdinalIgnoreCase);

        private Server _grpcServer;
        private string _selectedPlotName;
        private PlotViewModel _currentPlotViewModel;
        private string _serverAddress;
        private string _statusMessage = "Waiting for data...";

        public ObservableCollection<string> PlotNames { get; } = new ObservableCollection<string>();

        public string SelectedPlotName
        {
            get => _selectedPlotName;
            set
            {
                if (SetProperty(ref _selectedPlotName, value))
                {
                    CurrentPlotViewModel = value != null
                        && _plotRegistry.TryGetValue(value, out var vm) ? vm : null;
                }
            }
        }

        public PlotViewModel CurrentPlotViewModel
        {
            get => _currentPlotViewModel;
            private set => SetProperty(ref _currentPlotViewModel, value);
        }

        public string ServerAddress
        {
            get => _serverAddress;
            private set => SetProperty(ref _serverAddress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public ICommand OpenHelpCommand { get; }

        public MainViewModel()
        {
            OpenHelpCommand = new RelayCommand(OpenHelp);
            ServerAddress = ConfigService.GetGrpcServerAddress();
            StartGrpcServer();
        }

        private void StartGrpcServer()
        {
            try
            {
                ParseAddress(ServerAddress, out string host, out int port);

                var service = new PlotGrpcService(OnPlotDataReceived);
                _grpcServer = new Server
                {
                    Services = { Plot.BindService(service) },
                    Ports = { new ServerPort(host, port, ServerCredentials.Insecure) }
                };
                _grpcServer.Start();
                StatusMessage = $"Listening on {ServerAddress}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"gRPC server error: {ex.Message}";
            }
        }

        private void OnPlotDataReceived(PlotDataPayload payload)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool isNew = false;
                var plotVm = _plotRegistry.GetOrAdd(payload.PlotName, name =>
                {
                    isNew = true;
                    return new PlotViewModel(name);
                });

                if (isNew)
                    PlotNames.Add(payload.PlotName);

                plotVm.UpdateData(payload.Data, payload.PlotLabel);

                StatusMessage = $"Updated: {payload.PlotName}";

                // Refresh displayed plot if it is the currently selected one
                if (SelectedPlotName == payload.PlotName)
                    CurrentPlotViewModel = plotVm;
            });
        }

        private void OpenHelp()
        {
            MessageBox.Show(
                "Debug Plot Viewer\n\n" +
                "Receives plot data via gRPC and displays each named plot as a waveform graph.\n\n" +
                $"gRPC Server Address: {ServerAddress}\n" +
                "Service:  TSDebugPlot.Plot\n" +
                "Method:   /TSDebugPlot.Plot/PlotData\n\n" +
                "Request JSON fields:\n" +
                "  \"Plot Name\"   – unique identifier for the plot\n" +
                "  \"Plot Label\"  – array of series names (one per column)\n" +
                "  \"Data\"        – CSV string (rows=newline, columns=comma)\n" +
                "  \"Plot Type\"   – \"Graph\" (waveform graph)\n\n" +
                "Example:\n" +
                "  {\"Plot Name\":\"Voltage\",\"Plot Label\":[\"CH1\",\"CH2\"],\n" +
                "   \"Data\":\"1.0,2.0\\n3.0,4.0\",\"Plot Type\":\"Graph\"}",
                "Debug Plot Viewer – Help",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static void ParseAddress(string address, out string host, out int port)
        {
            var parts = address?.Split(':') ?? new[] { "localhost", "50056" };
            host = parts.Length > 0 ? parts[0] : "localhost";
            port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 50056;
        }

        public void Dispose()
        {
            _grpcServer?.ShutdownAsync().GetAwaiter().GetResult();
        }
    }
}
