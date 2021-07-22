using LoggerLibrary.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoggerLibrary
{
    public class ComponentLogger : IComponentLogger, ILogger
    {
        private readonly BaseLogger _parentLogger;

        public string ComponentName { get; init; }

        public ComponentLogger(BaseLogger parentLogger, string componentName)
        {
            _parentLogger = parentLogger;
            ComponentName = componentName;
        }

        /// <summary>
        /// Logs a message for the specific component.
        /// </summary>
        /// <param name="message">Message to be written.</param>
        /// <param name="logLevel">Log level specification. If unspecified, the default is 'INFO'.</param>
        public void Log(string message, BaseLogger.MsgType logLevel = BaseLogger.MsgType.INFO)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string prefix = _parentLogger.LogName + (string.IsNullOrWhiteSpace(ComponentName) ? "" : $"|{ComponentName}");

            if (_parentLogger.LogFilename == null)
            {
                lock (_parentLogger._lockObj)
                {
                    _parentLogger._logBuffer.Add(_parentLogger.MsgHeader(prefix, logLevel) + message);
                }
            }
            else
            {
                long logSizeBytes = new FileInfo(_parentLogger.LogFilename).Length;

                if (logSizeBytes >= _parentLogger.LogMaxBytes)
                {
                    _parentLogger.Open();
                }

                lock (_parentLogger._lockObj)
                {
                    foreach (var msg in _parentLogger._logBuffer)
                    {
                        Console.WriteLine(msg);
                        _parentLogger._logWriter.WriteLine(msg);
                    }

                    _parentLogger._logBuffer.Clear();

                    Console.WriteLine(_parentLogger.MsgHeader(prefix, logLevel) + message);
                    _parentLogger._logWriter.WriteLine(_parentLogger.MsgHeader(prefix, logLevel) + message);
                }
            }
        }

        /// <summary>
        /// Logs an exception message for a specific component.
        /// </summary>
        /// <param name="e">Exception to be logged.</param>
        /// <param name="message">Additional message for debugging purposes.</param>
        public void Log(Exception e, string message)
        {
            string prefix = _parentLogger.LogName + (string.IsNullOrWhiteSpace(ComponentName) ? "|" : $"|{ComponentName}|");

            if (_parentLogger.LogFilename == null)
            {
                lock (_parentLogger._lockObj)
                {
                    _parentLogger._logBuffer.Add(_parentLogger.MsgHeader(prefix, BaseLogger.MsgType.ERROR) + e.Message);

                    if (string.IsNullOrWhiteSpace(message) == false)
                    {
                        _parentLogger._logBuffer.Add(_parentLogger.MsgHeader(prefix, BaseLogger.MsgType.ERROR) + message);
                    }
                }
            }
            else
            {
                long logSizeBytes = new FileInfo(_parentLogger.LogFilename).Length;

                if (logSizeBytes >= _parentLogger.LogMaxBytes)
                {
                    _parentLogger.Open();
                }

                lock (_parentLogger._lockObj)
                {
                    foreach (var msg in _parentLogger._logBuffer)
                    {
                        Console.WriteLine(msg);
                        _parentLogger._logWriter.WriteLine(msg);
                    }

                    _parentLogger._logBuffer.Clear();

                    Console.WriteLine(_parentLogger.MsgHeader(prefix, BaseLogger.MsgType.ERROR) + e.Message);
                    _parentLogger._logWriter.WriteLine(_parentLogger.MsgHeader(prefix, BaseLogger.MsgType.ERROR) + e.Message);

                    if (string.IsNullOrWhiteSpace(message) == false)
                    {
                        Console.WriteLine(_parentLogger.MsgHeader(prefix, BaseLogger.MsgType.ERROR) + message);
                        _parentLogger._logWriter.WriteLine(_parentLogger.MsgHeader(prefix, BaseLogger.MsgType.ERROR) + message);
                    }
                }
            }
        }
    }
}
