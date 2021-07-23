namespace LoggerLibrary.Interfaces
{
    public interface ISimpleLoggerFactory
    {
        ISimpleLogger CreateLogger(ISimpleLogger parentLogger, string componentName);
        ISimpleLogger CreateLogger(string logName);
        ISimpleLogger CreateLogger(string logName, string logFolder);
        ISimpleLogger CreateLogger(string logName, string logFolder, long logMaxBytes, uint logMaxCount);
    }
}