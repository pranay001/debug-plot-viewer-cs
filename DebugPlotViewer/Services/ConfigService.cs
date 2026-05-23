using System.Configuration;

namespace DebugPlotViewer.Services
{
    public static class ConfigService
    {
        private const string DefaultAddress = "localhost:50056";

        public static string GetGrpcServerAddress()
        {
            var address = ConfigurationManager.AppSettings["GrpcServerAddress"];
            return string.IsNullOrWhiteSpace(address) ? DefaultAddress : address;
        }
    }
}
