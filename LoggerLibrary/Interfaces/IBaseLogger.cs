using System;

namespace LoggerLibrary.Interfaces
{
    public interface IBaseLogger
    {
        bool Close();
        void Log(Exception e, string message);
        void Log(string message, BaseLogger.MsgType logLevel = BaseLogger.MsgType.INFO);
        void Open();
    }
}