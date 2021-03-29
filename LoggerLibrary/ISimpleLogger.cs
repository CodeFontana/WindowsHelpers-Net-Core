using System;

namespace LoggerLibrary
{
    public interface ISimpleLogger
    {
        string LogFilename { get; }
        string LogFolder { get; }
        int LogIncrement { get; }
        long LogMaxBytes { get; }
        uint LogMaxCount { get; }
        string LogName { get; }

        void Open(string logName, string logPath = null, long maxBytes = 52428800, uint maxCount = 10);
        bool Close();
        void Log(Exception e, string message);
        void Log(string message, SimpleLogger.MsgType logLevel = SimpleLogger.MsgType.INFO);
        
    }
}