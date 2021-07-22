using System;

namespace LoggerLibrary.Interfaces
{
    public interface IBaseLogger : ILogger
    {
        void Open();
        bool Close();
    }
}