using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shadowsocks.Model;
using System.IO;
using System.Windows;

namespace BlsNeter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            Global.Controller?.Stop();
            Global.Controller = null;
        }

        protected async override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var hostbuilder = CreateHostBuilder(e.Args);
            var host = await hostbuilder.StartAsync();
            host.Services.GetRequiredService<MainWindow>()?.Show();
        }
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args);
            hostBuilder.ConfigureServices((ctx, services) =>
            {
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainWindowViewModel>();
            });

            return hostBuilder;
        }
    }
}
