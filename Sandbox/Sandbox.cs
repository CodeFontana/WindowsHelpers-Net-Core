using LoggerLibrary;
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
    private readonly IFileLogger _logger;

    public Sandbox(IHostApplicationLifetime hostApplicationLifetime,
                   IConfiguration configuration,
                   IFileLogger logger)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _configuration = configuration;
        _logger = logger;
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
                _logger.Log(e, "Unhandled exceptiopn!");
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
        _logger.Log("TODO: Add code here...");
    }
}
