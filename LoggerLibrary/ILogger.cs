using System;

namespace LoggerLibrary
{
    public interface ILogger
    {
        string LogFilename { get; }
        string LogFolder { get; }
        int LogIncrement { get; }
        long LogMaxBytes { get; }
        uint LogMaxCount { get; }
        string LogName { get; }

        bool Close();
        void Log(Exception e, string message);
        void Log(string message, Logger.MsgType logLevel = Logger.MsgType.INFO);
    }
}