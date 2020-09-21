using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.EventLog;

namespace AdminBolterService
{
    /// <summary>
    /// To publish the service, ensure that the service is deleted/not enabled first in services.msc
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>().Configure<EventLogSettings>(config =>
                    {
                        config.LogName = "Bolter Service";
                        config.SourceName = "Bolter Service Source";
                    }) ;
                }).UseWindowsService(); // Set the maximum possible lifetime
    }
}
