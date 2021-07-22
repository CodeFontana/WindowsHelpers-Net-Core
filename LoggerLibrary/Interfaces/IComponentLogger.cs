using System;

namespace LoggerLibrary.Interfaces
{
    public interface IComponentLogger
    {
        string ComponentName { get; init; }

        void Log(Exception e, string message);
        void Log(string message, BaseLogger.MsgType logLevel = BaseLogger.MsgType.INFO);
    }
}