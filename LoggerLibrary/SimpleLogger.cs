﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LoggerLibrary
{
    /// <summary>
    /// An implementation of SimpleLogger, providing basic logging functionality
    /// to disk. Features include log size and maximum increment setting, along
    /// with automatic rollover from one file to the next.
    /// </summary>
    public class SimpleLogger
    {
        public static List<SimpleLogger> LogManager { get; } = new();

        private FileStream _logStream = null;
        private StreamWriter _logWriter = null;
        private List<string> _logBuffer = new();
        private readonly object _lockObj = new();
        private bool _rollMode = false;

        public string LogName { get; private set; }
        public string LogFilename { get; private set; }
        public string LogFolder { get; private set; } = "";
        public int LogIncrement { get; private set; } = 0;
        public long LogMaxBytes { get; private set; } = 50 * 1048576;
        public uint LogMaxCount { get; private set; } = 10;

        public enum MsgType { NONE, INFO, DEBUG, WARN, ERROR, CRITICAL };

        /// <summary>
        /// Default constructor, empty for facilitating dependency injection.
        /// The caller must call the static method, CreateLog() to obtain an
        /// instance, followed by Open() to start or resume logging to that
        /// instance.
        /// </summary>
        private SimpleLogger()
        {
            
        }

        // For reference:
        //   1 MB = 1000000 Bytes (in decimal)
        //   1 MB = 1048576 Bytes (in binary)

        /// <summary>
        /// Creates a new log file for the specified component, and adds to the LogManager.
        /// Note: This call does not Open() the log file.
        /// </summary>
        /// <param name="logName">Component name for log file.</param>
        /// <param name="logFolder">Path where logs file(s) will be saved.</param>
        /// <param name="logMaxBytes">Maximum size (in bytes) for the log file. If unspecified, the default is 50MB per log.</param>
        /// <param name="logMaxCount">Maximum count of log files for rotation. If unspecified, the default is 10 logs.</param>
        /// <returns></returns>
        public static SimpleLogger CreateLog(
            string logName,
            string logFolder = null,
            long logMaxBytes = 50 * 1048576,
            uint logMaxCount = 10)
        {
            // Check for exisitng logger before creating a new one.
            var existingLogger = LogManager
                .Where(l => l.LogName.ToLower().Equals(logName.ToLower()))
                .FirstOrDefault();

            if (existingLogger != null)
            {
                return existingLogger;
            }

            // Create a new logger.
            SimpleLogger newLogger = new();

            // Set log path.
            if (logFolder == null)
            {
                string processName = Process.GetCurrentProcess().MainModule.FileName;
                string processPath = processName.Substring(0, processName.LastIndexOf("\\"));
                newLogger.LogFolder = processPath;
            }
            else if (Directory.Exists(logFolder) == false)
            {
                Directory.CreateDirectory(logFolder);
                newLogger.LogFolder = logFolder;
            }
            else
            {
                newLogger.LogFolder = logFolder;
            }

            // Set logger properties.
            newLogger.LogName = logName;
            newLogger.LogMaxBytes = logMaxBytes;
            newLogger.LogMaxCount = logMaxCount;

            // Add to log manager for static reference by component name.
            //   --> This is for static calls to Log() functions.
            //   --> E.g. Calling Log(component) from a Class Library function.
            //   --> Rather than passing around instances of this class, the log
            //       message can route to the correct instance, by calling the
            //       static Log() function, and passing the component name of an
            //       existing instance.
            //   --> Think of static Log(component) as 'GetInstance(component)',
            //       but shortened to 'Log(component)'.
            LogManager.Add(newLogger);

            return newLogger;
        }

        /// <summary>
        /// Checks if the specified file is in-use.
        /// </summary>
        /// <param name="fileName">The filename to check.</param>
        /// <returns></returns>
        public bool IsFileInUse(string fileName)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    FileInfo fileInfo = new(fileName);
                    FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    fileStream.Dispose();
                    return false;
                }
                catch (Exception)
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Opens a new log file or resumes an existing one.
        /// </summary>
        public void Open()
        {
            // If open, close the log file.
            if (LogFilename != null &&
                _logWriter != null &&
                _logWriter.BaseStream != null)
            {
                Close();
            }

            // Select next available log increment (sets LogFilename).
            IncrementLog();

            // Append the log file.
            _logStream = new FileStream(LogFilename, FileMode.Append, FileAccess.Write, FileShare.Read);
            _logWriter = new StreamWriter(_logStream);
            _logWriter.AutoFlush = true;

            // Write breakpoint.
            _logWriter.WriteLine("########################################");

            // Push any buffered messages for this log/component.
            lock (_lockObj)
            {
                foreach (var msg in _logBuffer)
                {
                    Console.WriteLine(msg);
                    _logWriter.WriteLine(msg);
                }

                _logBuffer.Clear();
            }
        }

        /// <summary>
        /// Privately sets 'LogFilename' with next available increment in the
        /// log file rotation.
        /// </summary>
        private void IncrementLog()
        {
            if (_rollMode == false)
            {
                // After we find our starting point, we will permanetly be in 
                // rollMode, meaning we will always increment/wrap to the next
                // available log file increment.
                _rollMode = true;

                // Base case -- Find nearest unfilled log to continue
                //              appending, or nearest unused increment
                //              to start writing a new file.
                for (int i = 0; i < LogMaxCount; i++)
                {
                    string fileName = $"{LogFolder}\\{LogName}_{i}.log";

                    if (File.Exists(fileName))
                    {
                        long length = new FileInfo(fileName).Length;

                        if (length < LogMaxBytes && IsFileInUse(fileName) == false)
                        {
                            // Append unfilled log.
                            LogFilename = fileName;
                            LogIncrement = i;
                            return;
                        }
                    }
                    else
                    {
                        // Take this unused increment.
                        LogFilename = fileName;
                        LogIncrement = i;
                        return;
                    }
                }

                // Full house? -- Start over from the top.
                LogFilename = $"{LogFolder}\\{LogName}_0.log";
                LogIncrement = 0;
            }
            else
            {
                // Inductive case -- We are in roll mode, so we just
                //                   use the next increment file, or
                //                   wrap around to the starting point.
                if (LogIncrement + 1 < LogMaxCount)
                {
                    // Next log increment.
                    LogFilename = $"{LogFolder}\\{LogName}_{++LogIncrement}.log";
                }
                else
                {
                    // Start over from the top.
                    LogFilename = $"{LogFolder}\\{LogName}_0.log";
                    LogIncrement = 0;
                }
            }

            // Delete existing log, before using it.
            File.Delete(LogFilename);
        }

        /// <summary>
        /// Closes the log file.
        /// </summary>
        /// <returns>Returns true if the log file successfully closed, false otherwise.</returns>
        public bool Close()
        {
            try
            {
                lock (_lockObj)
                {
                    // Don't call Log() here, this will result in a -=#StackOverflow#=-.
                    _logWriter.Dispose();
                    _logStream.Dispose();
                    _logWriter = null;
                    _logStream = null;
                    LogFilename = null;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a standard preamble for each log message. The preamble includes
        /// the current timestamp, the log component name and a formatted string with
        /// the specified log level. This method ensures each log message is consistently
        /// formatted.
        /// </summary>
        /// <param name="component">The component name for the message preamble.</param>
        /// <param name="entryType">The log level being annotated in the message preamble.</param>
        /// <returns>A consistently formatted preamble for human consumption.</returns>
        private static string MsgHeader(string component, MsgType entryType)
        {
            string header = DateTime.Now.ToString("yyyy-MM-dd--HH.mm.ss|");
            header += component + "|";

            switch (entryType)
            {
                case MsgType.NONE:
                    header += "    |";
                    break;
                case MsgType.INFO:
                    header += "INFO|";
                    break;
                case MsgType.DEBUG:
                    header += "DBUG|";
                    break;
                case MsgType.WARN:
                    header += "WARN|";
                    break;
                case MsgType.ERROR:
                    header += "FAIL|";
                    break;
                case MsgType.CRITICAL:
                    header += "CRIT|";
                    break;
            }

            return header;
        }

        /* Member methods for writing messages or exceptions to a
         * log file. No surprises here, this annotation only serves
         * to call these member methods out seperately from the static
         * methods below.
         */

        /// <summary>
        /// Logs a message to the default component (e.g. LogName).
        /// </summary>
        /// <param name="message">Message to be written.</param>
        /// <param name="logLevel">Log level specification. If unspecified, the default is 'INFO'.</param>
        public void Log(string message, MsgType logLevel = MsgType.INFO)
        {
            if (string.IsNullOrWhiteSpace(message) == false)
            {
                if (LogFilename == null)
                {
                    lock (_lockObj)
                    {
                        _logBuffer.Add(MsgHeader(LogName, logLevel) + message);
                    }
                }
                else
                {
                    long logSizeBytes = new FileInfo(LogFilename).Length;

                    if (logSizeBytes >= LogMaxBytes)
                    {
                        Open();
                    }

                    lock (_lockObj)
                    {
                        foreach(var msg in _logBuffer)
                        {
                            Console.WriteLine(msg);
                            _logWriter.WriteLine(msg);
                        }

                        _logBuffer.Clear();

                        Console.WriteLine(MsgHeader(LogName, logLevel) + message);
                        _logWriter.WriteLine(MsgHeader(LogName, logLevel) + message);
                    }
                }
            }
        }

        /// <summary>
        /// Logs a C# exception message to the default component.
        /// </summary>
        /// <param name="e">Exception to be logged.</param>
        /// <param name="message">Additional message for debugging purposes.</param>
        public void Log(Exception e, string message)
        {
            if (LogFilename == null)
            {
                lock (_lockObj)
                {
                    _logBuffer.Add(MsgHeader(LogName, MsgType.ERROR) + e.Message);

                    if (string.IsNullOrWhiteSpace(message) == false)
                    {
                        _logBuffer.Add(MsgHeader(LogName, MsgType.ERROR) + message);
                    }
                }
            }
            else
            {
                long logSizeBytes = new FileInfo(LogFilename).Length;

                if (logSizeBytes >= LogMaxBytes)
                {
                    Open();
                }

                lock (_lockObj)
                {
                    foreach (var msg in _logBuffer)
                    {
                        Console.WriteLine(msg);
                        _logWriter.WriteLine(msg);
                    }

                    _logBuffer.Clear();

                    Console.WriteLine(MsgHeader(LogName, MsgType.ERROR) + e.Message);
                    _logWriter.WriteLine(MsgHeader(LogName, MsgType.ERROR) + e.Message);

                    if (string.IsNullOrWhiteSpace(message) == false)
                    {
                        Console.WriteLine(MsgHeader(LogName, MsgType.ERROR) + message);
                        _logWriter.WriteLine(MsgHeader(LogName, MsgType.ERROR) + message);
                    }
                }
            }
        }

        /* Static Log() methods. These exist in order to prevent you from ever
         * having to pass any instance of Logger as a parameter to any method.
         * It may be expensive to pass a Logger object as a method parameter,
         * thus instead, your code can take advantage of these static methods.
         * 
         * Each new instance of Logger is indexed by log/component name in
         * the static LogManager at the top of this class.
         * 
         * Thus you can call the static Log() methods, passing only the
         * component name you wish to log a message. If the component name
         * specified aligns with an instance contained in the LogManager,
         * the message will be forwarded to the Log() method of that instance
         * and get written to the appropriate file.
         * 
         * A properly designed app will likely use the Dependency Inversion
         * principle, and this never need to take advantage of these static
         * methods. However, no code is perfect, and better to have these
         * and not need them.
         */

        /// <summary>
        /// Logs a message to the specified Logger instance.
        /// </summary>
        /// <param name="instance">The Logger instance (or component) to forward the log message.</param>
        /// <param name="message">The log message.</param>
        /// <param name="logLevel">The log level specification.</param>
        public static void Log(string instance, string message, MsgType logLevel = MsgType.INFO)
        {
            var logger = LogManager
                .Where(l => l.LogName.ToLower().Equals(instance.ToLower()))
                .FirstOrDefault();

            if (logger != null)
            {
                logger.Log(message, logLevel);
            }
            else
            {
                Console.WriteLine(MsgHeader(instance, logLevel) + message);
            }
        }

        /// <summary>
        /// Logs an exception to the specified Logger instance.
        /// </summary>
        /// <param name="component">The Logger instance (or component) to forward the exception information.</param>
        /// <param name="e">The Exception object.</param>
        /// <param name="message">Any additional message for debugging purposes.</param>
        public static void Log(string component, Exception e, string message)
        {
            var logger = LogManager
                .Where(l => l.LogName.ToLower().Equals(component.ToLower()))
                .FirstOrDefault();

            if (logger != null)
            {
                logger.Log(e, message);
            }
            else
            {
                Console.WriteLine(MsgHeader(component, MsgType.ERROR) + message);
            }
        }
    }
}
