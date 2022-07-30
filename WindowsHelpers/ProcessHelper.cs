using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using static System.Management.ManagementObjectCollection;

namespace WindowsLibrary;

public class ProcessHelper
{
    private readonly ILogger<ProcessHelper> _logger;
    private readonly WindowsHelper _winHelper;

    public ProcessHelper(ILogger<ProcessHelper> logger,
                         WindowsHelper winHelper)
    {
        _logger = logger;
        _winHelper = winHelper;
    }

    public Tuple<bool, int> CreateProcessAsUser(IntPtr hUserToken, string appFileName, string appArgs)
    {
        try
        {
            // Identify user from access token.
            WindowsIdentity userId = new(hUserToken);
            _logger.LogInformation($"Create process for: {userId.Name} [{appFileName} {appArgs}]");
            userId.Dispose();

            // Obtain duplicated user token (elevated if UAC is turned on/enabled).
            IntPtr hDuplicateToken = _winHelper.DuplicateToken(hUserToken);

            // Initialize process info and startup info.
            NativeMethods.PROCESS_INFORMATION pi = new();
            NativeMethods.STARTUPINFO si = new();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "winsta0\\default";
            NativeMethods.SECURITY_ATTRIBUTES lpProcessAttributes = new();
            NativeMethods.SECURITY_ATTRIBUTES lpThreadAttributes = new();
            IntPtr hEnvironment = IntPtr.Zero;

            if (NativeMethods.CreateEnvironmentBlock(out hEnvironment, hDuplicateToken, true) == false)
            {
                _logger.LogWarning($"Unable to create environment block [CreateEnvironmentBlock={Marshal.GetLastWin32Error()}]");
            }

            if (NativeMethods.CreateProcessAsUser(
                hDuplicateToken,
                null,
                appFileName + " " + appArgs,
                ref lpProcessAttributes,
                ref lpThreadAttributes,
                false,
                (uint)NativeMethods.CreateProcessFlags.NORMAL_PRIORITY_CLASS |
                (uint)NativeMethods.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT |
                (uint)NativeMethods.CreateProcessFlags.CREATE_NEW_CONSOLE,
                hEnvironment,
                Path.GetDirectoryName(appFileName),
                ref si,
                out pi) == false)
            {
                _logger.LogError($"Unable to create user process [CreateProcessAsUser={Marshal.GetLastWin32Error()}]");

                Marshal.FreeHGlobal(hDuplicateToken);
                Marshal.FreeHGlobal(hEnvironment);
                Marshal.FreeHGlobal(hUserToken);
                return new Tuple<bool, int>(false, -1);
            }
            else
            {
                _logger.LogInformation($"Created new process: {pi.dwProcessId}/{appFileName} {appArgs}");
                var newProcess = Process.GetProcessById(pi.dwProcessId);

                try
                {
                    // For UI apps, wait for idle state, before continuing.
                    newProcess.WaitForInputIdle(2000);
                }
                catch (InvalidOperationException)
                {
                    // Must be a non-UI app, just give it a sec to start.
                    Thread.Sleep(1000);
                }

                newProcess.Dispose();
                Marshal.FreeHGlobal(hDuplicateToken);
                Marshal.FreeHGlobal(hEnvironment);
                Marshal.FreeHGlobal(hUserToken);
                return new Tuple<bool, int>(true, pi.dwProcessId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create process as user");
            return new Tuple<bool, int>(false, -1);
        }
    }

    public bool CreateProcessAsUser(WindowsIdentity userId, string appFileName, string appArgs)
    {
        try
        {
            _logger.LogInformation($"Create process for: {userId.Name}");
            List<Tuple<uint, string>> userSessions = _winHelper.GetUserSessions();
            int sessionId = -1;

            foreach (Tuple<uint, string> logonSession in userSessions)
            {
                if (logonSession.Item2.ToLower().Equals(userId.Name.ToLower()))
                {
                    sessionId = (int)logonSession.Item1;
                    break;
                }
            }

            if (sessionId == -1)
            {
                _logger.LogError($"Failed to match any/existing logon session with user [{userId.Name}]");
                return false;
            }

            if (NativeMethods.WTSQueryUserToken((uint)sessionId, out IntPtr hUserToken) == false)
            {
                _logger.LogError($"Failed to query user token [WTSQueryUserToken={Marshal.GetLastWin32Error()}]");
                return false;
            }

            // Obtain duplicated user token (elevated if UAC is turned on/enabled).
            IntPtr hDuplicateToken = _winHelper.DuplicateToken(hUserToken, (uint)sessionId);
            Marshal.FreeHGlobal(hUserToken);

            // Initialize process info and startup info.
            NativeMethods.PROCESS_INFORMATION pi = new();
            NativeMethods.STARTUPINFO si = new();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "winsta0\\default";
            NativeMethods.SECURITY_ATTRIBUTES lpProcessAttributes = new();
            NativeMethods.SECURITY_ATTRIBUTES lpThreadAttributes = new();
            IntPtr hEnvironment = IntPtr.Zero;

            if (NativeMethods.CreateEnvironmentBlock(out hEnvironment, hDuplicateToken, true) == false)
            {
                _logger.LogWarning($"Unable to create environment block [CreateEnvironmentBlock={Marshal.GetLastWin32Error()}]");
            }

            if (NativeMethods.CreateProcessAsUser(
                hDuplicateToken,
                null,
                appFileName + " " + appArgs,
                ref lpProcessAttributes,
                ref lpThreadAttributes,
                false,
                (uint)NativeMethods.CreateProcessFlags.NORMAL_PRIORITY_CLASS |
                (uint)NativeMethods.CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT |
                (uint)NativeMethods.CreateProcessFlags.CREATE_NEW_CONSOLE,
                hEnvironment,
                Path.GetDirectoryName(appFileName),
                ref si,
                out pi) == false)
            {
                _logger.LogError($"Unable to create user process [CreateProcessAsUser={Marshal.GetLastWin32Error()}]");
                return false;
            }
            else
            {
                _logger.LogInformation($"Created new process: {pi.dwProcessId}/{appFileName} {appArgs}");
                var newProcess = Process.GetProcessById(pi.dwProcessId);

                try
                {
                    // For UI apps, wait for idle state, before continuing.
                    newProcess.WaitForInputIdle(2000);
                }
                catch (InvalidOperationException)
                {
                    // Must be a non-UI app, just give it a sec to start.
                    Thread.Sleep(1000);
                }

                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create process as user");
            return false;
        }
    }

    public bool IsProcessRunning(string processFriendlyName, bool moreInfo = false)
    {
        processFriendlyName = Path.GetFileNameWithoutExtension(processFriendlyName);

        foreach (Process runningProcess in Process.GetProcesses())
        {
            if (runningProcess.ProcessName.ToLower().Equals(processFriendlyName.ToLower()))
            {
                if (moreInfo)
                {
                    string commandLine = null;

                    try
                    {
                        ManagementObjectSearcher wmiQuery = new($"SELECT CommandLine FROM Win32_Process WHERE ProcessId='{runningProcess.Id}'");

                        foreach (ManagementObject wmiProcess in wmiQuery.Get())
                        {
                            commandLine = wmiProcess["CommandLine"].ToString();
                        }

                        wmiQuery.Dispose();
                    }
                    catch (Exception)
                    {
                        commandLine = "unavailable";
                    }

                    _logger.LogInformation($"IsProcessRunning() found: {runningProcess.Id}/{runningProcess.ProcessName} [{commandLine}]");

                    try
                    {
                        int currentID = runningProcess.Id;

                        List<uint> loopSafety = new List<uint>
                            {
                                (uint)currentID
                            };

                        // Iterate no more than the process count, divided by 2.
                        for (int i = 0; i <= Process.GetProcesses().Count() / 2; i++)
                        {
                            ManagementObjectSearcher wmiQuery = new($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId={currentID}");
                            ManagementObjectEnumerator wmiResult = wmiQuery.Get().GetEnumerator();
                            wmiResult.MoveNext();
                            var queryObj = wmiResult.Current;
                            var parentId = (uint)queryObj["ParentProcessId"]; // Query PPID

                            wmiQuery.Dispose();
                            wmiResult.Dispose();
                            queryObj.Dispose();

                            if (int.TryParse(parentId.ToString(), out int result))
                            {
                                break; // Invalid PPID
                            }

                            if (loopSafety.Contains(parentId))
                            {
                                break; // Loop safety
                            }
                            else
                            {
                                loopSafety.Add(parentId);
                            }

                            try
                            {
                                string parentName = Process.GetProcessById((int)parentId).ProcessName;
                                _logger.LogInformation($"IsProcessRunning() parent: {parentId}/{parentName}");
                                currentID = (int)parentId;
                            }
                            catch (ArgumentException)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception) { }
                }

                runningProcess.Dispose();
                return true;
            }

            runningProcess.Dispose();
        }

        return false;
    }

    public int IsProcessRunningCount(string processFriendlyName)
    {
        int processCount = 0;
        processFriendlyName = Path.GetFileNameWithoutExtension(processFriendlyName);

        foreach (Process runningProcess in Process.GetProcesses())
        {
            if (runningProcess.ProcessName.ToLower().Equals(processFriendlyName.ToLower()))
            {
                processCount += 1;
            }

            runningProcess.Dispose();
        }

        return processCount;
    }

    public bool KillProcess(string friendlyOrShortName, bool moreInfo = false)
    {
        bool matchFound = false;

        // ******************************
        // Match Process by Friendly Name [myApp].
        // ******************************

        try
        {
            foreach (Process runningProcess in Process.GetProcesses())
            {
                if (runningProcess.ProcessName.ToLower().Equals(friendlyOrShortName.ToLower()))
                {
                    matchFound = true;
                    string commandLine = null;

                    if (moreInfo)
                    {
                        try
                        {
                            ManagementObjectSearcher wmiQuery = new(
                                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId='{runningProcess.Id}'");

                            foreach (ManagementObject wmiProcess in wmiQuery.Get())
                            {
                                commandLine = wmiProcess["CommandLine"].ToString();
                            }

                            wmiQuery.Dispose();
                        }
                        catch (Exception)
                        {
                            commandLine = "unavailable";
                        }
                    }

                    runningProcess.Kill();

                    if (moreInfo)
                    {
                        _logger.LogInformation($"Killed: {runningProcess.Id}/{runningProcess.MainModule.FileName} [{commandLine}]");
                    }
                    else
                    {
                        _logger.LogInformation($"Killed: {runningProcess.Id}/{runningProcess.MainModule.FileName}");
                    }
                }

                runningProcess.Dispose();
            }

            if (matchFound)
            {
                return matchFound;
            }
        }
        catch (Exception) { }

        // ******************************
        // Match Process by Shortname [myApp.exe].
        // ******************************

        try
        {
            ManagementObjectSearcher wmiQuery = new($"SELECT ProcessID FROM Win32_Process WHERE Name='{friendlyOrShortName}'");

            foreach (ManagementObject wmiProcess in wmiQuery.Get())
            {
                string processId = null;

                if (wmiProcess["ProcessID"] != null)
                {
                    processId = wmiProcess["ProcessID"].ToString();
                    wmiProcess.Dispose();
                }
                else
                {
                    wmiProcess.Dispose();
                    continue; // Skip -- Missing required attribute[ProcessID]
                }

                matchFound = true;
                KillProcess(int.Parse(processId), moreInfo);
            }

            wmiQuery.Dispose();
        }
        catch (Exception) { }

        return matchFound;
    }

    public bool KillProcess(int processID, bool moreInfo = false)
    {
        try
        {
            foreach (Process runningProcess in Process.GetProcesses())
            {
                if (runningProcess.Id == processID)
                {
                    string commandLine = null;

                    if (moreInfo)
                    {
                        try
                        {
                            ManagementObjectSearcher wmiQuery = new($"SELECT CommandLine FROM Win32_Process WHERE ProcessId='{runningProcess.Id}'");

                            foreach (ManagementObject wmiProcess in wmiQuery.Get())
                            {
                                commandLine = wmiProcess["CommandLine"].ToString();
                            }

                            wmiQuery.Dispose();
                        }
                        catch (Exception)
                        {
                            commandLine = "unavailable";
                        }
                    }

                    runningProcess.Kill();

                    if (moreInfo)
                    {
                        _logger.LogInformation($"Killed: {runningProcess.Id}/{runningProcess.MainModule.FileName} [{commandLine}]");
                    }
                    else
                    {
                        _logger.LogInformation($"Killed: {runningProcess.Id}/{runningProcess.MainModule.FileName}");
                    }

                    runningProcess.Dispose();
                    return true;
                }

                runningProcess.Dispose();
            }
        }
        catch (Exception) { }

        return false;
    }

    public bool KillProcessByCommandLine(string processShortName, string containsCommandLine, bool moreInfo = false)
    {
        bool matchFound = false;

        try
        {
            ManagementObjectSearcher wmiQuery = new($"SELECT ProcessID,CommandLine FROM Win32_Process WHERE Name='{processShortName}'");

            foreach (ManagementObject wmiProcess in wmiQuery.Get())
            {
                string processId = null;
                string commandLine = null;

                if (wmiProcess["ProcessID"] != null && wmiProcess["CommandLine"] != null)
                {
                    processId = wmiProcess["ProcessID"].ToString();
                    commandLine = wmiProcess["CommandLine"].ToString();
                    wmiProcess.Dispose();
                }
                else
                {
                    wmiProcess.Dispose();
                    continue; // Skip -- Missing required attribute[CommandLine]
                }

                if (commandLine != null && commandLine.ToLower().Contains(containsCommandLine.ToLower()))
                {
                    matchFound = true;
                    KillProcess(int.Parse(processId), moreInfo);
                }
            }

            wmiQuery.Dispose();
        }
        catch (Exception) { }

        return matchFound;
    }

    public bool KillProcessByPath(string processShortName, string processPathContains)
    {
        bool processFound = false;

        try
        {
            ManagementObjectSearcher wmiQuery = new($"SELECT ProcessID,ExecutablePath FROM Win32_Process WHERE Name='{processShortName}");

            foreach (ManagementObject wmiProcess in wmiQuery.Get())
            {
                string processId = null;
                string executablePath = null;

                if (wmiProcess["ProcessID"] != null && wmiProcess["ExecutablePath"] != null)
                {
                    processId = wmiProcess["ProcessID"].ToString();
                    executablePath = wmiProcess["ExecutablePath"].ToString();
                    wmiProcess.Dispose();
                }
                else
                {
                    wmiProcess.Dispose();
                    continue; // Skip -- Missing required attribute [ExecutablePath]
                }

                if (executablePath != null && executablePath.ToLower().Contains(processPathContains.ToLower()))
                {
                    processFound = true;
                    KillProcess(int.Parse(processId));
                }
            }

            wmiQuery.Dispose();
        }
        catch (Exception) { }

        return processFound;
    }

    public string ReadProcessList()
    {
        List<string[]> runningProcesses = new();
        string[] outputHeader = { "Process", "PID", "User", "CPU Time", "Memory", "Handles", "Threads", "Command Line" };
        runningProcesses.Add(outputHeader);

        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                ProcessInfo pi = new(p);
                runningProcesses.Add(pi.ToStringArray());
                p.Dispose();
            }
            catch (Exception) { }
        }

        return DotNetHelper.PadListElements(runningProcesses, 1);
    }

    public Tuple<long, string> RunProcess(string appFileName,
                                          string arguments = "",
                                          string workingDirectory = "",
                                          int execTimeoutSeconds = Timeout.Infinite,
                                          bool hideWindow = false,
                                          bool hideStreamOutput = false,
                                          bool hideExecution = false)
    {
        // ******************************
        // Resolve Explicit Path of App to Run.
        // ******************************

        string processName = Process.GetCurrentProcess().MainModule.FileName;
        string processPath = processName.Substring(0, processName.LastIndexOf("\\"));

        try
        {
            // Prepend relative path with current process path.
            if (appFileName.Contains("\\") && appFileName.Contains(":\\") == false)
            {
                if (appFileName.StartsWith("\\"))
                {
                    appFileName = processPath + appFileName;
                }
                else
                {
                    appFileName = processPath + "\\" + appFileName;
                }
            }
            else if (appFileName.Contains("\\") == false 
                && appFileName.Contains(":\\") == false)
            {
                appFileName = processPath + "\\" + appFileName;
            }

            // Application executable doesn't exists?
            // Note: File.Exists() accepts relative paths via current working directory.
            if (File.Exists(appFileName) == false 
                && File.Exists(appFileName.TrimStart('\\')) == false)
            {
                // Take a copy of the original string.
                string origAppToExecute = appFileName;

                // As file doesn't exist, strip away the path.
                if (appFileName.Contains("\\"))
                {
                    appFileName = appFileName.Substring(appFileName.LastIndexOf("\\") + 1);
                }

                var pathValues = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);

                // Is this application available on the system PATH?
                foreach (var path in pathValues.Split(';'))
                {
                    var pathFilename = Path.Combine(path, appFileName);

                    if (File.Exists(pathFilename))
                    {
                        appFileName = pathFilename;
                        break;
                    }
                }

                // Last chance.
                if (File.Exists(appFileName) == false 
                    && File.Exists(appFileName.TrimStart('\\')) == false)
                {
                    _logger.LogError("Application not found [" + origAppToExecute + "]");
                    return Tuple.Create((long)-1, "");
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to resolve explicit path for app [{appFileName}]");
            return Tuple.Create((long)-1, "");
        }

        // ******************************
        // Resolve Working Directory.
        // ******************************

        try
        {
            if (workingDirectory == null || workingDirectory.Equals(""))
            {
                if (appFileName.Contains("\\"))
                {
                    workingDirectory = Path.GetDirectoryName(appFileName);

                    if (Directory.Exists(workingDirectory) == false)
                    {
                        workingDirectory = processPath;
                    }
                }
                else
                {
                    workingDirectory = processPath;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to resolve working directory for app [{appFileName}]");
            return Tuple.Create((long)-1, "");
        }

        // ******************************
        // Prepare New Process.
        // ******************************

        Process p = new Process();
        List<string> combinedOutput = new();
        Task consumeStdOut = null;
        Task consumeStdErr = null;
        CancellationTokenSource cts = new(); // Needed for batch files, see usage below.

        try
        {
            p.StartInfo.FileName = appFileName.Replace("\\\\", "\\");
            p.StartInfo.Arguments = arguments;
            p.StartInfo.WorkingDirectory = workingDirectory;
            p.StartInfo.UseShellExecute = false; // Use CreateProcess(), *NOT* ShellExecute()
            p.StartInfo.RedirectStandardOutput = true; // Redirect STDOUT
            p.StartInfo.RedirectStandardError = true; // Redirect STDERR
            p.StartInfo.CreateNoWindow = hideWindow; // Passed into function
            p.StartInfo.Verb = "runas"; // Elevate (note sure if this works with UseShellExecute=false)

            if (hideExecution == false)
            {
                _logger.LogInformation($"Create process: {appFileName} {arguments} [Timeout={execTimeoutSeconds}s]");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to prepare new process for execution [{appFileName}]");
            return Tuple.Create((long)-1, "");
        }

        // ******************************
        // Start New Process.
        // ******************************

        try
        {
            p.Start();

            // Create async task for consuming the STDOUT/STDERR streams.
            async Task ConsumeOutputAsync(StreamReader outputStream)
            {
                string textLine;

                while (cts.Token.IsCancellationRequested == false &&
                    (textLine = await outputStream.ReadLineAsync()) != null)
                {
                    lock (combinedOutput)
                    {
                        combinedOutput.Add(textLine);
                    }

                    if (hideStreamOutput == false && hideExecution == false)
                    {
                        _logger.LogInformation(textLine);
                    }
                }
            }

            consumeStdOut = Task.Run(() => ConsumeOutputAsync(p.StandardOutput), cts.Token);
            consumeStdErr = Task.Run(() => ConsumeOutputAsync(p.StandardError), cts.Token);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to start new process");
            return Tuple.Create((long)-1, "");
        }

        // ******************************
        // Monitor Process -- Wait for Process Exit or Timeout.
        // ******************************

        try
        {
            if (execTimeoutSeconds >= 0)
            {
                execTimeoutSeconds *= 1000;
            }

            p.WaitForExit(execTimeoutSeconds);

            if (p.HasExited == false)
            {
                p.Kill();
                _logger.LogError($"Killed: {Path.GetFileName(appFileName)} [Timeout breached]");
            }
            else
            {
                // Signal task cancellation for reading STDOUT/STDERR streams and wait for Tasks to stop
                cts.Cancel();
                try { if (consumeStdOut != null) { consumeStdOut.Wait(cts.Token); } } catch (OperationCanceledException) { }
                try { if (consumeStdErr != null) { consumeStdErr.Wait(cts.Token); } } catch (OperationCanceledException) { }
            }

            int ExitCode = p.ExitCode;

            if (hideExecution == false)
            {
                _logger.LogInformation($"{Path.GetFileName(appFileName)} return code: {ExitCode}");
            }

            return Tuple.Create((long)ExitCode, String.Join(Environment.NewLine, combinedOutput.ToList()));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "New process monitoring failure");
            return Tuple.Create((long)-1, "");
        }
        finally
        {
            try { if (consumeStdOut != null) { consumeStdOut.Dispose(); } }
            catch (Exception) { /* _logger.Log(e, "Resource disposal failure [consumeStdOut]"); */ }
            try { if (consumeStdErr != null) { consumeStdErr.Dispose(); } }
            catch (Exception) { /* _logger.Log(e, "Resource disposal failure [consumeStdErr]"); */ }
            try { cts.Dispose(); }
            catch (Exception) {/*  _logger.Log(e, "Resource disposal failure [cts]"); */ }
            try { p.Dispose(); }
            catch (Exception) { /* _logger.Log(e, "Resource disposal failure [p]"); */ }
        }
    }

    public bool RunProcessDetached(string appFileName,
                                   string arguments,
                                   string workingDirectory = "",
                                   bool hideWindow = false,
                                   bool hideExecution = false)
    {
        string processName = Process.GetCurrentProcess().MainModule.FileName;
        string processPath = processName.Substring(0, processName.LastIndexOf("\\"));

        // Prepend relative path with current process path.
        if (appFileName.Contains("\\") 
            && appFileName.Contains(":\\") == false)
        {
            if (appFileName.StartsWith("\\"))
            {
                appFileName = processPath + appFileName;
            }
            else
            {
                appFileName = processPath + "\\" + appFileName;
            }
        }
        else if (appFileName.Contains("\\") == false 
            && appFileName.Contains(":\\") == false)
        {
            appFileName = processPath + "\\" + appFileName;
        }

        // Application executable doesn't exists?
        // Note: File.Exists() accepts relative paths via current working directory.
        if (File.Exists(appFileName) == false 
            && File.Exists(appFileName.TrimStart('\\')) == false)
        {
            // Take a copy of the original string.
            string origAppToExecute = appFileName;

            // As file doesn't exist, strip away the path.
            if (appFileName.Contains("\\"))
            {
                appFileName = appFileName.Substring(appFileName.LastIndexOf("\\") + 1);
            }

            var pathValues = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);

            // Is this application available on the system PATH?
            foreach (var path in pathValues.Split(';'))
            {
                var pathFilename = Path.Combine(path, appFileName);

                if (File.Exists(pathFilename))
                {
                    appFileName = pathFilename;
                    break;
                }
            }

            // Last chance.
            if (File.Exists(appFileName) == false && File.Exists(appFileName.TrimStart('\\')) == false)
            {
                _logger.LogError("Application not found [" + origAppToExecute + "]");
                return false;
            }
        }

        if (workingDirectory == null || workingDirectory.Equals(""))
        {
            if (appFileName.Contains("\\"))
            {
                workingDirectory = Path.GetDirectoryName(appFileName);

                if (Directory.Exists(workingDirectory) == false)
                {
                    workingDirectory = processPath;
                }
            }
            else
            {
                workingDirectory = processPath;
            }
        }

        Process p = new();
        p.StartInfo.FileName = appFileName.Replace("\\\\", "\\");
        p.StartInfo.Arguments = arguments;
        p.StartInfo.WorkingDirectory = workingDirectory;
        p.StartInfo.UseShellExecute = false; // Use CreateProcess() API, *NOT* ShellExecute() API
        p.StartInfo.CreateNoWindow = hideWindow; // Passed into function
        p.StartInfo.Verb = "runas"; // Elevate (note sure if this works with UseShellExecute=false)

        if (hideExecution == false)
        {
            _logger.LogInformation("Execute [Detached]: " + appFileName + " " + arguments);
        }

        try
        {
            p.Start();
            _logger.LogInformation($"Created detached process: {p.Id}/{appFileName.Replace("\\\\", "\\")} {arguments}");

            // Brief delay for app startup, before continuing.
            // Note: This is for the scenario where the parent app (this app)
            //       is up against termination. We need to pause briefly to
            //       allow the child process to establish before continuing.
            Thread.Sleep(3000);

            // We used to do it this way, but WaitForInputIdle only works
            // for UI apps, and not batch or console apps. Adding a simple
            // delay accomplishes the same, but leaving this code for
            // reference, in case anyone gets any bright ideas.
            /*try
            {
                p.WaitForInputIdle(2000);
                p.WaitForExit(3000);
            }
            catch (InvalidOperationException)
            {
                Thread.Sleep(5000);
            }*/
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to start new detached process");
            return false;
        }

        return true;
    }
}
