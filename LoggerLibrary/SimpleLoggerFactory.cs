using LoggerLibrary.Interfaces;

namespace LoggerLibrary;

public class SimpleLoggerFactory : ISimpleLoggerFactory
{
    public ISimpleLogger CreateLogger(string logName)
    {
        return new SimpleLogger(logName);
    }

    public ISimpleLogger CreateLogger(string logName, string logFolder)
    {
        return new SimpleLogger(logName, logFolder);
    }

    public ISimpleLogger CreateLogger(string logName, string logFolder, long logMaxBytes, uint logMaxCount)
    {
        return new SimpleLogger(logName, logFolder, logMaxBytes, logMaxCount);
    }

    public ISimpleLogger CreateLogger(ISimpleLogger parentLogger, string componentName)
    {
        return new ComponentLogger(parentLogger, componentName);
    }
}
