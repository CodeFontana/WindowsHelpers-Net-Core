using LoggerLibrary.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox;

public class Sandbox : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _configuration;
    private readonly ISimpleLogger _logFile;

    public Sandbox(IHostApplicationLifetime hostApplicationLifetime,
                   IConfiguration configuration,
                   ISimpleLogger logFile)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _configuration = configuration;
        _logFile = logFile;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                Run();
            }
            catch (Exception e)
            {
                _logFile.Log(e, "Unhandled exceptiopn!");
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void Run()
    {
        _logFile.Log("TODO: Add code here...");
    }
}
