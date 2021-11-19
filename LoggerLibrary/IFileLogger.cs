using Microsoft.Extensions.Logging;
using System;

namespace LoggerLibrary
{
    public interface IFileLogger
    {
        IDisposable BeginScope<TState>(TState state);
        IFileLogger CreateFileLogger(string categoryName);
        ILogger CreateLogger(string categoryName);
        void Dispose();
        bool IsEnabled(LogLevel logLevel);
        void Log(Exception e, string message);
        void Log(string message, FileLoggerProvider.MsgType logLevel = FileLoggerProvider.MsgType.INFO);
        void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter);
        void LogCritical(string message);
        void LogDebug(string message);
        void LogError(string message);
        void LogInformation(string message);
        void LogWarning(string message);
    }
}