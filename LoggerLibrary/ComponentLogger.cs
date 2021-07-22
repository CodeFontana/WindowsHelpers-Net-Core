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
        private readonly IBaseLogger _parentLogger;

        public string ComponentName { get; init; }

        public ComponentLogger(IBaseLogger parentLogger, string componentName)
        {
            _parentLogger = parentLogger;
            ComponentName = componentName;
        }

        /// <summary>
        /// Logs a message for a specific component.
        /// </summary>
        /// <param name="message">Message to be written.</param>
        /// <param name="logLevel">Log level specification. If unspecified, the default is 'INFO'.</param>
        public void Log(string message, BaseLogger.MsgType logLevel = BaseLogger.MsgType.INFO)
        {
            string prefix = string.IsNullOrWhiteSpace(ComponentName) ? "" : $"{ComponentName}|";
            _parentLogger.Log(prefix + message, logLevel);
        }

        /// <summary>
        /// Logs an exception message for a specific component.
        /// </summary>
        /// <param name="e">Exception to be logged.</param>
        /// <param name="message">Additional message for debugging purposes.</param>
        public void Log(Exception e, string message)
        {
            string prefix = string.IsNullOrWhiteSpace(ComponentName) ? "" : $"{ComponentName}|";
            _parentLogger.Log(e, prefix + message);
        }
    }
}
