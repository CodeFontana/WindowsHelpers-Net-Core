using LoggerLibrary.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoggerLibrary
{
    public class ComponentLogger : ISimpleLogger
    {
        private readonly ISimpleLogger _parentLogger;

        public string ComponentName { get; init; }

        public ComponentLogger(ISimpleLogger parentLogger, string componentName)
        {
            _parentLogger = parentLogger ?? throw new ArgumentException("Parent logger must not be NULL.");

            if (string.IsNullOrWhiteSpace(componentName))
            {
                throw new ArgumentException("Component name must not be NULL or empty.");
            }
            
            ComponentName = componentName;
        }

        /// <summary>
        /// Logs a message for a specific component.
        /// </summary>
        /// <param name="message">Message to be written.</param>
        /// <param name="logLevel">Log level specification. If unspecified, the default is 'INFO'.</param>
        public void Log(string message, SimpleLogger.MsgType logLevel = SimpleLogger.MsgType.INFO)
        {
            _parentLogger.Log($"{ComponentName}|{message}", logLevel);
        }

        /// <summary>
        /// Logs an exception message for a specific component.
        /// </summary>
        /// <param name="e">Exception to be logged.</param>
        /// <param name="message">Additional message for debugging purposes.</param>
        public void Log(Exception e, string message)
        {
            _parentLogger.Log(e, $"{ComponentName}|{message}");
        }
    }
}
