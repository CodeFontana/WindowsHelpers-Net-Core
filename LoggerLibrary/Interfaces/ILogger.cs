using System;

namespace LoggerLibrary.Interfaces
{
    public interface ILogger
    {
        void Log(Exception e, string message);
        void Log(string message, BaseLogger.MsgType logLevel = BaseLogger.MsgType.INFO);
    }
}