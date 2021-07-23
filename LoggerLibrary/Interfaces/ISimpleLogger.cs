using System;

namespace LoggerLibrary.Interfaces
{
    public interface ISimpleLogger
    {
        void Log(Exception e, string message);
        void Log(string message, SimpleLogger.MsgType logLevel = SimpleLogger.MsgType.INFO);
    }
}