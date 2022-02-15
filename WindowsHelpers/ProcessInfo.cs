using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.IO;
using System.Collections.Generic;

namespace WindowsLibrary;

public class ProcessInfo
{
    public string ProcessName { get; private set; }
    public string ProcessShortName { get; private set; }
    public string ProcessFriendlyName { get; private set; }
    public string ProcessFilePath { get; private set; }
    public int PID { get; private set; }
    public string UserName { get; private set; }
    public string CPUTime { get; private set; }
    public long NumBytes { get; private set; }
    public int HandleCount { get; private set; }
    public int ThreadCount { get; private set; }
    public string CommandLineArgs { get; private set; }

    public ProcessInfo(Process p)
    {
        ProcessName = p.MainModule.FileName;
        ProcessShortName = Path.GetFileName(ProcessName);
        ProcessFriendlyName = p.ProcessName;
        ProcessFilePath = Path.GetDirectoryName(ProcessName);
        PID = p.Id;
        UserName = GetProcessOwner(p.Handle);
        CPUTime = p.TotalProcessorTime.ToString().Substring(0, 11);
        NumBytes = p.WorkingSet64;
        HandleCount = p.HandleCount;
        ThreadCount = p.Threads.Count;
        CommandLineArgs = GetProcessCLIArgsWMI(PID);
    }

    public override string ToString()
    {
        return $"{ProcessName}|{PID}|{UserName}|{CPUTime}|{NumBytes}|{HandleCount}|{ThreadCount}|{CommandLineArgs}";
    }

    public string[] ToStringArray()
    {
        return new string[] {
                ProcessShortName,
                PID.ToString(),
                UserName,
                CPUTime,
                BytesToReadableValue(NumBytes),
                HandleCount.ToString(),
                ThreadCount.ToString(),
                $"{ProcessName} {CommandLineArgs}"};
    }

    public static string GetProcessCLIArgsWMI(int processId)
    {
        using (ManagementObjectSearcher searcher = new($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}"))
        {
            using (ManagementObjectCollection objects = searcher.Get())
            {
                return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
            }
        }
    }

    public static string GetProcessOwnerWMI(int processId)
    {
        // NOTE: This was replaced by GetProcessOwner(IntPtr hProcess), since native
        //       P/Invoke is significantly faster than WMI.

        string wmiQuery = $"Select * From Win32_Process Where ProcessID = {processId}";
        ManagementObjectSearcher wmiSearcher = new(wmiQuery);
        ManagementObjectCollection processList = wmiSearcher.Get();

        foreach (ManagementObject obj in processList)
        {
            string[] argList = new string[] { string.Empty, string.Empty };
            int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
            if (returnVal == 0)
            {
                return argList[1] + "\\" + argList[0];
            }
        }

        wmiSearcher.Dispose();
        processList.Dispose();

        return "<Unavailable>";
    }

    public static string GetProcessOwner(IntPtr hProcess)
    {
        IntPtr hToken = IntPtr.Zero;
        try
        {
            NativeMethods.OpenProcessToken(hProcess, 8, out hToken);
            var wi = new WindowsIdentity(hToken).Name;
            return wi;
        }
        catch
        {
            return "<Not Available>";
        }
        finally
        {
            if (hToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(hToken);
            }
        }
    }

    private static string BytesToReadableValue(long numBytes)
    {
        var suffixes = new List<string> { " B ", " KB", " MB", " GB", " TB", " PB" };

        for (int i = 0; i < suffixes.Count; i++)
        {
            // Divide by powers of 1024, as we move through the scales
            long temp = Math.Abs(numBytes / (long)Math.Pow(1024, i + 1));

            // Have we gone off scale?
            if (temp <= 0)
            {
                // Return prior suffix value
                return String.Format("{0,9}", String.Format("{0:0.00}", Math.Round((double)numBytes / Math.Pow(1024, i), 2)) + suffixes[i]);
            }
        }

        return numBytes.ToString();
    }
}
