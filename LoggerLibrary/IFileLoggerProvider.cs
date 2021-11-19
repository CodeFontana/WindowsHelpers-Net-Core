using Microsoft.Extensions.Logging;
using System;

namespace LoggerLibrary
{
    public interface IFileLoggerProvider
    {
        string LogFilename { get; }
        string LogFolder { get; }
        int LogIncrement { get; }
        long LogMaxBytes { get; }
        uint LogMaxCount { get; }
        string LogName { get; }

        bool Close();
        IFileLogger CreateFileLogger(string categoryName);
        ILogger CreateLogger(string categoryName);
        void Dispose();
        void Log(Exception e, string message);
        void Log(string message, FileLoggerProvider.MsgType logLevel = FileLoggerProvider.MsgType.INFO);
        void LogCritical(string message);
        void LogDebug(string message);
        void LogError(string message);
        void LogInformation(string message);
        void LogWarning(string message);
        void Open();
    }
}