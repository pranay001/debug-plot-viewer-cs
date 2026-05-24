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
    ///   - Maintains a registry of view-models keyed by plot name
    ///   - Routes data to PlotViewModel ("Graph") or TableViewModel ("Table")
    /// Also owns the PlotUIDisplay responsibilities (server IP, plot selection, help).
    /// </summary>
    public class MainViewModel : ViewModelBase, IDisposable
    {
        // Registry holds either PlotViewModel or TableViewModel, keyed by plot name.
        // Type is fixed at first registration (mirrors LabVIEW actor-per-plot design).
        private readonly ConcurrentDictionary<string, ViewModelBase> _plotRegistry
            = new ConcurrentDictionary<string, ViewModelBase>(StringComparer.OrdinalIgnoreCase);

        private Server _grpcServer;
        private string _selectedPlotName;
        private ViewModelBase _currentPlotViewModel;
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

        /// <summary>
        /// Currently displayed view-model. The DataTemplates in App.xaml map:
        ///   PlotViewModel  → PlotView  (line chart)
        ///   TableViewModel → TableView (data grid)
        /// </summary>
        public ViewModelBase CurrentPlotViewModel
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

        // ── gRPC server ──────────────────────────────────────────────────────────

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

        // ── Incoming data handler ─────────────────────────────────────────────────

        private void OnPlotDataReceived(PlotDataPayload payload)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bool isNew = false;

                if (!_plotRegistry.TryGetValue(payload.PlotName, out ViewModelBase vm))
                {
                    // Create the correct ViewModel based on PlotType (fixed for the lifetime of this plot)
                    vm = CreateViewModel(payload.PlotName, payload.PlotType);

                    if (_plotRegistry.TryAdd(payload.PlotName, vm))
                        isNew = true;
                    else
                        // Another thread beat us – use whatever is already registered
                        vm = _plotRegistry[payload.PlotName];
                }

                if (isNew)
                    PlotNames.Add(payload.PlotName);

                // Dispatch data to the correct concrete type
                if (vm is PlotViewModel graphVm)
                    graphVm.UpdateData(payload.Data, payload.PlotLabel);
                else if (vm is TableViewModel tableVm)
                    tableVm.UpdateData(payload.Data, payload.PlotLabel);

                StatusMessage = $"Updated: {payload.PlotName}";

                // Keep the displayed view in sync when the selected plot is updated
                if (SelectedPlotName == payload.PlotName)
                    CurrentPlotViewModel = vm;
            });
        }

        private static ViewModelBase CreateViewModel(string plotName, string plotType)
        {
            if (string.Equals(plotType, "Table", StringComparison.OrdinalIgnoreCase))
                return new TableViewModel(plotName);

            // "Graph" or any unrecognised type → waveform chart (matches LabVIEW default)
            return new PlotViewModel(plotName);
        }

        // ── Help ─────────────────────────────────────────────────────────────────

        private void OpenHelp()
        {
            MessageBox.Show(
                "Debug Plot Viewer\n\n" +
                "Receives plot data via gRPC and displays each named plot as a waveform graph or data table.\n\n" +
                $"gRPC Server Address: {ServerAddress}\n" +
                "Service:  TSDebugPlot.Plot\n" +
                "Method:   /TSDebugPlot.Plot/PlotData\n\n" +
                "Request JSON fields:\n" +
                "  \"Plot Name\"   – unique identifier for the plot\n" +
                "  \"Plot Label\"  – array of series/column names (one per CSV row)\n" +
                "  \"Data\"        – CSV string: each ROW = one series; columns = samples\n" +
                "  \"Plot Type\"   – \"Graph\" (waveform chart) | \"Table\" (data grid)\n\n" +
                "Graph example:\n" +
                "  {\"Plot Name\":\"Voltage\",\"Plot Label\":[\"CH1\",\"CH2\"],\n" +
                "   \"Data\":\"1.0,2.0,3.0\\n4.0,5.0,6.0\",\"Plot Type\":\"Graph\"}\n\n" +
                "Table example:\n" +
                "  {\"Plot Name\":\"Results\",\"Plot Label\":[\"Temp\",\"Pressure\"],\n" +
                "   \"Data\":\"25.1,26.3,27.0\\n101.1,102.4,103.2\",\"Plot Type\":\"Table\"}",
                "Debug Plot Viewer – Help",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ── Utilities ─────────────────────────────────────────────────────────────

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
