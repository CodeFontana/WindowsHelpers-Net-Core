using System;

namespace LoggerLibrary.Interfaces
{
    public interface IComponentLogger : ILogger
    {
        string ComponentName { get; init; }
    }
}