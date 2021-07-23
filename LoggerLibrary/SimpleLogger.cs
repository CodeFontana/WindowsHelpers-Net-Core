using LoggerLibrary.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LoggerLibrary
{
    public class SimpleLogger : ISimpleLogger, IDisposable
    {
        public FileStream _logStream = null;
        public StreamWriter _logWriter = null;
        public List<string> _logBuffer = new();
        public readonly object _lockObj = new();
        public bool _rollMode = false;

        public string LogName { get; private set; }
        public string LogFilename { get; private set; }
        public string LogFolder { get; private set; } = "";
        public int LogIncrement { get; private set; } = 0;
        public long LogMaxBytes { get; private set; } = 50 * 1048576;
        public uint LogMaxCount { get; private set; } = 10;

        public enum MsgType { NONE, INFO, DEBUG, WARN, ERROR, CRITICAL };

        /// <summary>
        /// Default constructor, instantiates a new log file instance.
        /// 
        /// For reference:
        ///   1 MB = 1000000 Bytes (in decimal)
        ///   1 MB = 1048576 Bytes (in binary)
        /// </summary>
        /// <param name="logName">Name for log file.</param>
        /// <param name="logFolder">Path where logs file(s) will be saved.</param>
        /// <param name="logMaxBytes">Maximum size (in bytes) for the log file. If unspecified, the default is 50MB per log.</param>
        /// <param name="logMaxCount">Maximum count of log files for rotation. If unspecified, the default is 10 logs.</param>
        /// <returns></returns>
        public SimpleLogger(string logName,
                            string logFolder = null,
                            long logMaxBytes = 50 * 1048576,
                            uint logMaxCount = 10)
        {
            if (string.IsNullOrWhiteSpace(logFolder))
            {
                string processName = Process.GetCurrentProcess().MainModule.FileName;
                string processPath = processName.Substring(0, processName.LastIndexOf("\\"));
                LogFolder = processPath + @"\log";
            }
            else if (Directory.Exists(logFolder) == false)
            {
                LogFolder = logFolder;
            }
            else
            {
                LogFolder = logFolder;
            }

            Directory.CreateDirectory(LogFolder);
            LogName = logName;
            LogMaxBytes = logMaxBytes;
            LogMaxCount = logMaxCount;
            Open();
        }

        /// <summary>
        /// Checks if the specified file is in-use.
        /// </summary>
        /// <param name="fileName">The filename to check.</param>
        /// <returns></returns>
        public static bool IsFileInUse(string fileName)
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
            _logWriter = new StreamWriter(_logStream)
            {
                AutoFlush = true
            };

            // Write breakpoint.
            _logWriter.WriteLine("########################################");

            // Push any buffered messages for this log.
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
        /// the current timestamp, a prefix and a formatted string with the specified
        /// log level. This method ensures log messages are consistently formatted.
        /// </summary>
        /// <param name="prefix">Message prefix.</param>
        /// <param name="entryType">The log level being annotated in the message preamble.</param>
        /// <returns>A consistently formatted preamble for human consumption.</returns>
        public static string MsgHeader(string prefix, MsgType entryType)
        {
            string header = DateTime.Now.ToString("yyyy-MM-dd--HH.mm.ss|");
            header += prefix + "|";

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
        /// Logs a message.
        /// </summary>
        /// <param name="message">Message to be written.</param>
        /// <param name="logLevel">Log level specification. If unspecified, the default is 'INFO'.</param>
        public void Log(string message, MsgType logLevel = MsgType.INFO)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

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
                    foreach (var msg in _logBuffer)
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

        /// <summary>
        /// Logs an exception message.
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

        public void Dispose()
        {
            Close();
        }
    }
}
