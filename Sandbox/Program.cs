using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.Logging;
using LoggerLibrary.Interfaces;
using LoggerLibrary;
using WindowsLibrary;

namespace Sandbox;

class Program
{
    public static IConfigurationRoot Configuration { get; set; }

    static async Task Main(string[] args)
    {
        string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        bool isDevelopment = string.IsNullOrEmpty(env) || env.ToLower() == "development";

        await Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile($"appsettings.json", true, true);
                config.AddJsonFile($"appsettings.{env}.json", true, true);
                config.AddEnvironmentVariables();

                if (isDevelopment)
                {
                    //config.AddUserSecrets<Program>();
                }
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddTransient<ISimpleLoggerFactory, SimpleLoggerFactory>();
                services.AddSingleton(sp => sp.GetRequiredService<ISimpleLoggerFactory>().CreateLogger("Sandbox"));
                services.AddTransient<IWindowsLibraryFactory, WindowsLibraryFactory>();
                services.AddHostedService<Sandbox>();
            })
            .ConfigureLogging((hostContext, config) =>
            {
                config.ClearProviders();
            })
            .RunConsoleAsync();
    }
}
