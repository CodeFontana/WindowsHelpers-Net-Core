using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Management;
using static System.Management.ManagementObjectCollection;
using Microsoft.Extensions.Logging;

namespace WindowsLibrary;

public class WindowsHelper
{
    private readonly ILogger<WindowsHelper> _logger;
    private readonly RegistryHelper _registryHelper;

    public WindowsHelper(ILogger<WindowsHelper> logger, RegistryHelper registryHelper)
    {
        _logger = logger;
        _registryHelper = registryHelper;
    }

    public bool AddHostFileEntry(string entry)
    {
        try
        {
            StreamWriter hostsWriter = new(Environment.GetEnvironmentVariable("windir") + @"\system32\drivers\etc\hosts", true);
            hostsWriter.AutoFlush = true;
            hostsWriter.WriteLine(entry);
            hostsWriter.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to add hosts file entry");
            return false;
        }

        return true;
    }

    public bool AddToSystemPath(string folder)
    {
        try
        {
            if (Directory.Exists(folder) == false)
            {
                return false;
            }

            bool existsOnPath = false;
            string pathVariable = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);

            foreach (string path in pathVariable.Split(';'))
            {
                if (path.ToLower().Equals(folder.ToLower()))
                {
                    existsOnPath = true;
                    _logger.LogInformation($"Already exists on PATH: {folder}");
                    break;
                }
            }

            if (existsOnPath == false)
            {
                _logger.LogInformation($"Add to PATH: {folder}");
                string newPathVariable = pathVariable + ";" + folder;
                Environment.SetEnvironmentVariable("Path", newPathVariable, EnvironmentVariableTarget.Machine);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to add {folder} to system PATH variable");
            return false;
        }

        return true;
    }

    // Replace with: https://stackoverflow.com/questions/52443399/c-net-code-to-screen-capture-multiple-monitors-with-scaling

    /*public List<Tuple<string, Bitmap>> CaptureScreen()
    {
        try
        {
            List<Tuple<string, Bitmap>> bitmapList = new();

            foreach (Screen s in Screen.AllScreens)
            {
                string captureFileShortName = s.DeviceName.Substring(s.DeviceName.LastIndexOf("\\") + 1) + "--" + GetTimeStamp();
                _logger.LogInformation("Capture screen: " + s.DeviceName +
                    " [" + s.Bounds.Width + "x" + s.Bounds.Height + "] [" + captureFileShortName + "]");

                Bitmap bmpScreenshot = new(s.Bounds.Width, s.Bounds.Height, PixelFormat.Format32bppArgb);
                Graphics gfxScreenshot = Graphics.FromImage(bmpScreenshot);
                gfxScreenshot.CopyFromScreen(s.Bounds.X, s.Bounds.Y, 0, 0, s.Bounds.Size, CopyPixelOperation.SourceCopy);
                bitmapList.Add(new Tuple<string, Bitmap>(captureFileShortName, bmpScreenshot));
            }

            return bitmapList;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to capture screen");
        }

        return null;
    }*/

    /*public bool CaptureScreen(string outputFolder)
    {
        try
        {
            foreach (Screen s in Screen.AllScreens)
            {
                string captureFileShortName = s.DeviceName.Substring(s.DeviceName.LastIndexOf("\\") + 1) + "--" + GetTimeStamp();
                _logger.LogInformation("Capture screen: " + s.DeviceName +
                    " [" + s.Bounds.Width + "x" + s.Bounds.Height + "] [" + captureFileShortName + "]");

                Bitmap bmpScreenshot = new(s.Bounds.Width, s.Bounds.Height, PixelFormat.Format32bppArgb);
                Graphics gfxScreenshot = Graphics.FromImage(bmpScreenshot);
                gfxScreenshot.CopyFromScreen(s.Bounds.X, s.Bounds.Y, 0, 0, s.Bounds.Size, CopyPixelOperation.SourceCopy);
                _logger.LogInformation("Save: " + outputFolder + "\\" + captureFileShortName + ".png");
                bmpScreenshot.Save(outputFolder + "\\" + captureFileShortName + ".png", ImageFormat.Png);
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to capture screen");
        }

        return false;
    }*/

    public void CreateShortcut(string shortcutFileName,
                               string targetFileName,
                               string targetArguments = "",
                               string shortcutDescription = "",
                               int iconNumber = 2)
    {
        // Icon index numbers can be referenced at this link:
        //   https://help4windows.com/windows_7_shell32_dll.shtml

        // Define 'Windows Script Host Shell Object' as a type
        Type windowsScriptHostShell = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));

        // Create a shell instance
        dynamic wshShellInstance = Activator.CreateInstance(windowsScriptHostShell);

        try
        {
            if (shortcutFileName.EndsWith(".lnk") == false)
            {
                shortcutFileName += ".lnk";
            }

            var lnk = wshShellInstance.CreateShortcut(shortcutFileName);

            try
            {
                lnk.TargetPath = targetFileName;
                lnk.Arguments = targetArguments;
                lnk.WorkingDirectory = Path.GetDirectoryName(targetFileName);
                lnk.IconLocation = "shell32.dll, " + iconNumber.ToString();
                lnk.Description = shortcutDescription;
                lnk.Save();
            }
            finally
            {
                Marshal.FinalReleaseComObject(lnk);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(wshShellInstance);
        }
    }

    public bool ConfigureAutomaticLogon(string logonUser, string logonPwd)
    {
        try
        {
            _logger.LogInformation("Configure automatic logon user: " + logonUser);

            RegistryKey winLogonKey = _registryHelper.OpenKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true, RegistryHive.LocalMachine);
            winLogonKey.SetValue("AutoAdminLogon", "1", RegistryValueKind.String);
            winLogonKey.SetValue("DefaultUserName", logonUser, RegistryValueKind.String);
            winLogonKey.SetValue("DefaultPassword", logonPwd, RegistryValueKind.String);
            winLogonKey.SetValue("DisableCAD", "1", RegistryValueKind.DWord);
            winLogonKey.DeleteValue("AutoLogonCount", false);
            winLogonKey.DeleteValue("DefaultDomainName", false);
            winLogonKey.Dispose();

            RegistryKey policiesKey = _registryHelper.OpenKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true, RegistryHive.LocalMachine);
            policiesKey.SetValue("EnableFirstLogonAnimation", "0", RegistryValueKind.DWord);
            policiesKey.Dispose();

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to configure automatic logon");
            return false;
        }
    }

    public DateTime ConvertBinaryDateTime(Byte[] bytes)
    {
        long filedate = (((((((
        (long)bytes[7] * 256 +
        (long)bytes[6]) * 256 +
        (long)bytes[5]) * 256 +
        (long)bytes[4]) * 256 +
        (long)bytes[3]) * 256 +
        (long)bytes[2]) * 256 +
        (long)bytes[1]) * 256 +
        (long)bytes[0]);
        DateTime returnDate = DateTime.FromFileTime(filedate);
        return returnDate;
    }

    public bool DeleteEnvironmentVariable(string variableName)
    {
        if (Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine) != null)
        {
            Environment.SetEnvironmentVariable(variableName, null, EnvironmentVariableTarget.Machine);
            return true;
        }

        return false;
    }

    public void DetachConsole()
    {
        IntPtr cw = NativeMethods.GetConsoleWindow();
        NativeMethods.FreeConsole();
        NativeMethods.SendMessage(cw, 0x0102, 13, IntPtr.Zero);
    }

    public IntPtr DuplicateToken(IntPtr hUserToken, uint sessionId = 65536)
    {
        IntPtr hTokenToDup = hUserToken; // this may be replaced by a linked/elevated token if UAC is turned ON/enabled.
        IntPtr hDuplicateToken = IntPtr.Zero;
        int cbSize = 0;

        try
        {
            NativeMethods.SECURITY_ATTRIBUTES sa = new();
            sa.nLength = Marshal.SizeOf(sa);

            if (hUserToken == IntPtr.Zero)
            {
                _logger.LogError("No token was provided");
                return IntPtr.Zero;
            }

            if (Environment.OSVersion.Version.Major >= 6)
            {
                // Is the provided token NOT elevated?
                if (IsTokenElevated(hUserToken) == false)
                {
                    cbSize = IntPtr.Size;
                    IntPtr pLinkedToken = Marshal.AllocHGlobal(cbSize);

                    if (pLinkedToken == IntPtr.Zero)
                    {
                        _logger.LogError("Failed to allocate memory for linked token check");
                        return IntPtr.Zero;
                    }

                    // Are we NOT able to query the linked token? [Note: If the user is an admin, the linked token will be the elevation token!!!!!]
                    if (NativeMethods.GetTokenInformation(hUserToken,
                                                          NativeMethods.TOKEN_INFORMATION_CLASS.TokenLinkedToken,
                                                          pLinkedToken,
                                                          cbSize,
                                                          out cbSize) == false)
                    {
                        _logger.LogError($"Failed to query LINKED token [GetTokenInformation={Marshal.GetLastWin32Error()}]");
                        Marshal.FreeHGlobal(pLinkedToken);
                        return IntPtr.Zero;
                    }

                    if (pLinkedToken != IntPtr.Zero)
                    {
                        _logger.LogInformation("Token has a LINKED token");

                        // Is the linked token an elevated token?
                        if (IsTokenElevated(Marshal.ReadIntPtr(pLinkedToken)))
                        {
                            _logger.LogInformation("LINKED token is ELEVATED, assign for duplication...");
                            hTokenToDup = Marshal.ReadIntPtr(pLinkedToken);
                        }
                        else
                        {
                            _logger.LogInformation("LINKED token is not elevated");
                        }

                        Marshal.FreeHGlobal(pLinkedToken);
                    }
                    else
                    {
                        _logger.LogInformation("Token does NOT have a LINKED token");
                    }
                }
            }

            if (NativeMethods.DuplicateTokenEx(hTokenToDup,
                                               NativeMethods.TOKEN_MAXIMUM_ALLOWED,
                                               ref sa,
                                               NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                                               NativeMethods.TOKEN_TYPE.TokenPrimary,
                                               ref hDuplicateToken) == false)
            {
                _logger.LogError($"Failed to duplicate token [DuplicateTokenEx={Marshal.GetLastWin32Error()}]");
                Marshal.FreeHGlobal(hTokenToDup);
                return IntPtr.Zero;
            }

            Marshal.FreeHGlobal(hTokenToDup);

            cbSize = IntPtr.Size;
            IntPtr pSessionId = Marshal.AllocHGlobal(cbSize);

            if (NativeMethods.GetTokenInformation(hDuplicateToken,
                                                  NativeMethods.TOKEN_INFORMATION_CLASS.TokenSessionId,
                                                  pSessionId,
                                                  cbSize,
                                                  out cbSize) == false)
            {
                _logger.LogError($"Failed to token's session id [GetTokenInformation={Marshal.GetLastWin32Error()}]");
                Marshal.FreeHGlobal(pSessionId);
                return IntPtr.Zero;
            }
            else
            {
                _logger.LogInformation($"Duplicated token is configured for session id [{Marshal.ReadInt32(pSessionId)}]");
            }

            if (sessionId >= 0 && sessionId <= 65535 && sessionId != Marshal.ReadInt32(pSessionId))
            {
                _logger.LogInformation($"Adjust token session: {sessionId}");

                if (NativeMethods.SetTokenInformation(hDuplicateToken,
                                                      NativeMethods.TOKEN_INFORMATION_CLASS.TokenSessionId,
                                                      ref sessionId,
                                                      (uint)Marshal.SizeOf(sessionId)) == false)
                {
                    _logger.LogError($"Failed to assign token session [SetTokenInformation={Marshal.GetLastWin32Error()}]");
                    return hDuplicateToken;
                }
            }

            Marshal.FreeHGlobal(pSessionId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed duplicating or elevating user token");
        }

        return hDuplicateToken;
    }

    public List<Tuple<uint, string>> GetUserSessions()
    {
        List<Tuple<uint, string>> userSessions = new();

        try
        {
            IntPtr hServer = NativeMethods.WTSOpenServer(Environment.MachineName);
            IntPtr hSessionInfo = IntPtr.Zero;

            if (NativeMethods.WTSEnumerateSessions(hServer,
                                                   0,
                                                   1,
                                                   ref hSessionInfo,
                                                   out UInt32 sessionCount) == false)
            {
                _logger.LogError($"Failed to enumerate user sessions [WTSEnumerateSessions={Marshal.GetLastWin32Error()}]");
            }
            else
            {
                Int32 sessionSize = Marshal.SizeOf(typeof(NativeMethods.WTS_SESSION_INFO));
                IntPtr hCurSession = hSessionInfo;

                for (int i = 1; i < sessionCount; i++)
                {
                    NativeMethods.WTS_SESSION_INFO si = (NativeMethods.WTS_SESSION_INFO)Marshal.PtrToStructure(hCurSession,
                                                                                                               typeof(NativeMethods.WTS_SESSION_INFO));

                    if (NativeMethods.WTSQueryUserToken(si.SessionID, out IntPtr hUserToken) == false)
                    {
                        _logger.LogError($"Failed to query terminal user token [WTSQueryUserToken={Marshal.GetLastWin32Error()}] in session [{si.SessionID}]");
                    }
                    else
                    {
                        WindowsIdentity userId = new(hUserToken);
                        _logger.LogInformation($"Found session: {si.SessionID}/{userId.Name}");
                        userSessions.Add(new Tuple<uint, string>(si.SessionID, userId.Name));
                        userId.Dispose();
                    }

                    hCurSession += sessionSize;
                }

                NativeMethods.WTSFreeMemory(hSessionInfo);
            }

            NativeMethods.WTSCloseServer(hServer);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to query terminal user sessions");
        }

        return userSessions;
    }

    public bool EnablePrivilege(IntPtr hToken, string privilege)
    {
        _logger.LogInformation($"Enable: {privilege}");

        NativeMethods.LUID luid = new();
        NativeMethods.TOKEN_PRIVILEGES newState;
        newState.PrivilegeCount = 1;
        newState.Privileges = new NativeMethods.LUID_AND_ATTRIBUTES[1];

        if (NativeMethods.LookupPrivilegeValue(null, privilege, ref luid) == false)
        {
            _logger.LogError($"Unable to lookup privilege (LookupPrivilegeValue={Marshal.GetLastWin32Error()})");
            return false;
        }

        newState.Privileges[0].Luid = luid;
        newState.Privileges[0].Attributes = NativeMethods.SE_PRIVILEGE_ENABLED;

        if (NativeMethods.AdjustTokenPrivileges(hToken,
                                                false,
                                                ref newState,
                                                (UInt32)Marshal.SizeOf(newState),
                                                out NativeMethods.TOKEN_PRIVILEGES oldState,
                                                out UInt32 outBytes) == false)
        {
            _logger.LogError($"Unable to adjust token privileges (AdjustTokenPrivileges={Marshal.GetLastWin32Error()})");
            return false;
        }

        return true;
    }

    public IntPtr GetAdminUserToken()
    {
        try
        {
            uint consoleSessionId = NativeMethods.WTSGetActiveConsoleSessionId();

            if (consoleSessionId != 0xFFFFFFFF)
            {
                _logger.LogInformation($"Found console session: {consoleSessionId}");

                if (NativeMethods.WTSQueryUserToken(consoleSessionId, out IntPtr hUserToken) == false)
                {
                    _logger.LogError($"Failed to query console user token [WTSQueryUserToken={Marshal.GetLastWin32Error()}]");
                }
                else
                {
                    WindowsIdentity userId = new(hUserToken);
                    _logger.LogInformation($"Console user: {userId.Name}");
                    userId.Dispose();

                    if (IsUserInAdminGroup(hUserToken) == false)
                    {
                        _logger.LogWarning("Console user is not an administrator");
                    }
                    else
                    {
                        _logger.LogInformation("Console user is an administrator");
                        return hUserToken;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to query console user session");
        }

        try
        {
            IntPtr hServer = NativeMethods.WTSOpenServer(Environment.MachineName);
            IntPtr hSessionInfo = IntPtr.Zero;

            if (NativeMethods.WTSEnumerateSessions(hServer,
                                                   0,
                                                   1,
                                                   ref hSessionInfo,
                                                   out UInt32 sessionCount) == false)
            {
                _logger.LogError($"Failed to enumerate user sessions [WTSEnumerateSessions={Marshal.GetLastWin32Error()}]");
            }
            else
            {
                Int32 sessionSize = Marshal.SizeOf(typeof(NativeMethods.WTS_SESSION_INFO));
                IntPtr hCurSession = hSessionInfo;

                for (int i = 0; i < sessionCount; i++)
                {
                    NativeMethods.WTS_SESSION_INFO si = (NativeMethods.WTS_SESSION_INFO)Marshal.PtrToStructure(hCurSession,
                                                                                                               typeof(NativeMethods.WTS_SESSION_INFO));
                    _logger.LogInformation($"Found session: {si.SessionID}");

                    if (NativeMethods.WTSQueryUserToken(si.SessionID, out IntPtr hUserToken) == false)
                    {
                        _logger.LogError($"Failed to query terminal user token [WTSQueryUserToken={Marshal.GetLastWin32Error()}]");
                    }
                    else
                    {
                        WindowsIdentity userId = new(hUserToken);
                        _logger.LogInformation($"Terminal user: {userId.Name}");
                        userId.Dispose();

                        if (IsUserInAdminGroup(hUserToken) == false)
                        {
                            _logger.LogWarning("Terminal user is not an administrator");
                        }
                        else
                        {
                            _logger.LogInformation("Terminal user is an administrator");
                            return hUserToken;
                        }
                    }

                    hCurSession += sessionSize;
                }

                NativeMethods.WTSFreeMemory(hSessionInfo);
            }

            NativeMethods.WTSCloseServer(hServer);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to query terminal user sessions");
        }

        return IntPtr.Zero;
    }

    public IntPtr GetConsoleUserToken()
    {
        try
        {
            uint consoleSessionId = NativeMethods.WTSGetActiveConsoleSessionId();

            if (consoleSessionId != 0xFFFFFFFF)
            {
                _logger.LogInformation($"Found console session: {consoleSessionId}");

                if (NativeMethods.WTSQueryUserToken(consoleSessionId, out IntPtr hUserToken) == false)
                {
                    _logger.LogError($"Failed to query console user token [WTSQueryUserToken={Marshal.GetLastWin32Error()}]");
                }
                else
                {
                    WindowsIdentity userId = new(hUserToken);
                    _logger.LogInformation($"Console user: {userId.Name}");
                    userId.Dispose();
                    return hUserToken;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to query console user session");
        }

        return IntPtr.Zero;
    }

    public List<Tuple<uint, string>> GetParentProcesses()
    {
        List<Tuple<uint, string>> ParentProcessList = new();
        int currentPid = Process.GetCurrentProcess().Id;
        List<uint> loopSafetyList = new();
        loopSafetyList.Add((uint)currentPid);

        for (int i = 0; i <= Process.GetProcesses().Count() / 2; i++)
        {
            ManagementObjectSearcher wmiQuery = new($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId={currentPid}");
            ManagementObjectEnumerator wmiResult = wmiQuery.Get().GetEnumerator();
            wmiResult.MoveNext();
            ManagementBaseObject currentObject = wmiResult.Current;
            uint parentPID = (uint)currentObject["ParentProcessId"];
            wmiQuery.Dispose();
            currentObject.Dispose();

            if (int.TryParse(parentPID.ToString(), out int result) == false)
            {
                break; // Invalid PPID -- PPID has terminated already.
            }

            if (loopSafetyList.Contains(parentPID))
            {
                break; // Loop safety -- We've traversed this PPID already.
            }
            else
            {
                loopSafetyList.Add(parentPID);
            }

            try
            {
                string parentProcessName = Process.GetProcessById((int)parentPID).ProcessName;
                _logger.LogInformation($"Parent: {parentPID}/{parentProcessName}");
                ParentProcessList.Add(new Tuple<uint, string>(parentPID, parentProcessName.ToLower()));
                currentPid = (int)parentPID;
            }
            catch (Exception)
            {
                break; // Parent process has probably terminated.
            }
        }

        return ParentProcessList;
    }

    public string GetTimeStamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd--HH.mm.ss");
    }

    public string GetUninstallReg(string displayName)
    {
        if (Environment.Is64BitOperatingSystem)
        {
            RegistryKey localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            RegistryKey uninstallKey64 = localMachine64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);

            foreach (string subKeyName in uninstallKey64.GetSubKeyNames())
            {
                try
                {
                    RegistryKey productKey = uninstallKey64.OpenSubKey(subKeyName, false);
                    var displayNameValue = productKey.GetValue("DisplayName");

                    if (displayNameValue != null)
                    {
                        if (displayNameValue.ToString().ToLower().Equals(displayName.ToLower()))
                        {
                            uninstallKey64.Dispose();
                            localMachine64.Dispose();
                            return $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{subKeyName}";
                        }
                    }

                    productKey.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to open product key [{subKeyName}]");
                    continue;
                }
            }

            uninstallKey64.Dispose();
            localMachine64.Dispose();
        }

        RegistryKey localMachine32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        RegistryKey uninstallKey32 = localMachine32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);

        foreach (string subKeyName in uninstallKey32.GetSubKeyNames())
        {
            try
            {
                RegistryKey productKey = uninstallKey32.OpenSubKey(subKeyName, false);
                var displayNameValue = productKey.GetValue("DisplayName");

                if (displayNameValue != null)
                {
                    if (displayNameValue.ToString().ToLower().Equals(displayName.ToLower()))
                    {
                        uninstallKey32.Dispose();
                        localMachine32.Dispose();
                        return @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + subKeyName;
                    }
                }

                productKey.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to open product key [{subKeyName}]");
                continue;
            }
        }

        uninstallKey32.Dispose();
        localMachine32.Dispose();
        return null;
    }

    public string GetUninstallString(string displayName)
    {
        bool foundApp = false;
        string returnString = null;

        if (Environment.Is64BitOperatingSystem)
        {
            RegistryKey localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            RegistryKey uninstallKey64 = localMachine64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);

            foreach (string subKeyName in uninstallKey64.GetSubKeyNames())
            {
                try
                {
                    RegistryKey productKey = uninstallKey64.OpenSubKey(subKeyName, false);
                    var displayNameValue = productKey.GetValue("DisplayName");

                    if (displayNameValue != null)
                    {
                        if (displayNameValue.ToString().ToLower().Equals(displayName.ToLower()))
                        {
                            foundApp = true;
                            var uninstStringValue = productKey.GetValue("UninstallString");

                            if (uninstStringValue != null)
                            {
                                returnString = uninstStringValue.ToString();
                            }

                            break;
                        }
                    }

                    productKey.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to open product key [{subKeyName}]");
                    continue;
                }
            }

            uninstallKey64.Dispose();
            localMachine64.Dispose();

            if (foundApp)
            {
                return returnString;
            }
        }

        RegistryKey localMachine32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        RegistryKey uninstallKey32 = localMachine32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);

        foreach (string subKeyName in uninstallKey32.GetSubKeyNames())
        {
            try
            {
                RegistryKey productKey = uninstallKey32.OpenSubKey(subKeyName, false);
                var displayNameValue = productKey.GetValue("DisplayName");

                if (displayNameValue != null)
                {
                    if (displayNameValue.ToString().ToLower().Equals(displayName.ToLower()))
                    {
                        foundApp = true;
                        var uninstStringValue = productKey.GetValue("UninstallString");

                        if (uninstStringValue != null)
                        {
                            returnString = uninstStringValue.ToString();
                        }

                        break;
                    }
                }

                productKey.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to open product key [" + subKeyName + "]");
                continue;
            }
        }

        uninstallKey32.Dispose();
        localMachine32.Dispose();

        if (foundApp)
        {
            return returnString;
        }
        else
        {
            return returnString;
        }
    }

    private static void GrantAccess(string username, IntPtr handle, int accessMask)
    {
        SafeHandle safeHandle = new NativeMethods.NoopSafeHandle(handle);
        NativeMethods.GenericSecurity security = new NativeMethods.GenericSecurity(false,
                                                                                   ResourceType.WindowObject,
                                                                                   safeHandle,
                                                                                   AccessControlSections.Access);
        security.AddAccessRule(new NativeMethods.GenericAccessRule(new NTAccount(username),
                                                                   accessMask,
                                                                   AccessControlType.Allow));
        security.Persist(safeHandle, AccessControlSections.Access);
    }

    public void GrantAccessToWindowStationAndDesktop(string username)
    {
        IntPtr handle;
        const int WindowStationAllAccess = 0x000f037f;
        handle = NativeMethods.GetProcessWindowStation();
        GrantAccess(username, handle, WindowStationAllAccess);
        const int DesktopRightsAllAccess = 0x000f01ff;
        handle = NativeMethods.GetThreadDesktop(NativeMethods.GetCurrentThreadId());
        GrantAccess(username, handle, DesktopRightsAllAccess);
    }

    public bool ImportCertificate(string certFilename,
                                  string certPassword = "",
                                  StoreName certStore = StoreName.My,
                                  StoreLocation certLocation = StoreLocation.CurrentUser)
    {
        try
        {
            if (File.Exists(certFilename) == false)
            {
                _logger.LogError($"Specified certifcate file does not exist [{certFilename}]");
                return false;
            }

            X509Certificate2 importCert = null;

            if (certPassword != "")
            {
                importCert = new(certFilename, certPassword);
            }
            else
            {
                importCert = new(certFilename);
            }

            X509Store store = new(certStore, certLocation);
            store.Open(OpenFlags.ReadWrite);

            if (store.Certificates.Contains(importCert) == false)
            {
                _logger.LogInformation("Import certificate...");
                store.Add(importCert);
                _logger.LogInformation("Certifcate imported successfully");
            }
            else
            {
                _logger.LogInformation("Certificate already imported");
            }

            store.Dispose();
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to import certificate");
            return false;
        }
    }

    public bool IncreaseProcessPrivileges(Process targetProcess)
    {
        IntPtr hProcess = targetProcess.Handle;

        if (NativeMethods.OpenProcessToken(hProcess,
                                           NativeMethods.TOKEN_ALL_ACCESS,
                                           out IntPtr hToken) == false)
        {
            _logger.LogError($"Unable to open specified process token [OpenProcessToken={Marshal.GetLastWin32Error()}]");
            return false;
        }

        return IncreaseTokenPrivileges(hToken);
    }

    public bool IncreaseTokenPrivileges(IntPtr hToken)
    {
        if (EnablePrivilege(hToken, NativeMethods.SE_INCREASE_QUOTA_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeIncreaseQuotaPrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_ASSIGNPRIMARYTOKEN_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeAssignPrimaryTokenPrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_TCB_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeTcbPrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_DEBUG_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeDebugPrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_IMPERSONATE_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeImpersonatePrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_TIME_ZONE_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeTimeZonePrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_SYSTEMTIME_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeSystemtimePrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_SHUTDOWN_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeShutdownPrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_TAKE_OWNERSHIP_NAME) == false)
        {
            _logger.LogError("Failed to enable privilege [SeTakeOwnershipPrivilege]");
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        Marshal.FreeHGlobal(hToken);
        return true;
    }

    public bool IsAppInstalled(string displayName)
    {
        bool foundApp = false;

        if (Environment.Is64BitOperatingSystem)
        {
            RegistryKey localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            RegistryKey uninstallKey64 = localMachine64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);

            foreach (string subKeyName in uninstallKey64.GetSubKeyNames())
            {
                try
                {
                    RegistryKey productKey = uninstallKey64.OpenSubKey(subKeyName, false);
                    var displayNameValue = productKey.GetValue("DisplayName");

                    if (displayNameValue != null)
                    {
                        if (displayNameValue.ToString().ToLower().Equals(displayName.ToLower()))
                        {
                            foundApp = true;
                            break;
                        }
                    }

                    productKey.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to open product key [{subKeyName}]");
                    continue;
                }
            }

            uninstallKey64.Dispose();
            localMachine64.Dispose();

            if (foundApp)
            {
                return true;
            }
        }

        RegistryKey localMachine32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        RegistryKey uninstallKey32 = localMachine32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);

        foreach (string subKeyName in uninstallKey32.GetSubKeyNames())
        {
            try
            {
                RegistryKey productKey = uninstallKey32.OpenSubKey(subKeyName, false);
                var displayNameValue = productKey.GetValue("DisplayName");

                if (displayNameValue != null)
                {
                    if (displayNameValue.ToString().ToLower().Equals(displayName.ToLower()))
                    {
                        foundApp = true;
                        break;
                    }
                }

                productKey.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to open product key [{subKeyName}]");
                continue;
            }
        }

        uninstallKey32.Dispose();
        localMachine32.Dispose();

        if (foundApp)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool IsAutoLogonConfigred(out string logonUser, out string logonPwd)
    {
        int autoAdminLogon = -1;
        logonUser = null;
        logonPwd = null;

        try
        {
            _logger.LogInformation("Read logon configuration...");
            RegistryKey winLogonKey = _registryHelper
                .OpenKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true, RegistryHive.LocalMachine);
            var curAutoAdminLogon = winLogonKey.GetValue("AutoAdminLogon");
            var curAutoLogonCount = winLogonKey.GetValue("AutoLogonCount");
            var curDefaultUserName = winLogonKey.GetValue("DefaultUserName");
            var curDefaultPassword = winLogonKey.GetValue("DefaultPassword");
            var curDisableCAD = winLogonKey.GetValue("DisableCAD");

            if (curAutoAdminLogon != null)
            {
                if (int.TryParse(curAutoAdminLogon.ToString(), out autoAdminLogon) == false)
                {
                    autoAdminLogon = -1;
                }

                _logger.LogInformation($"  AutoAdminLogon: {autoAdminLogon}");
            }
            else
            {
                _logger.LogInformation("  AutoAdminLogon: <Not Available>");
            }

            if (curAutoLogonCount != null)
            {
                if (int.TryParse(curAutoLogonCount.ToString(), out int autoLogonCount) == false)
                {
                    autoLogonCount = -1;
                }

                _logger.LogInformation($"  AutoLogonCount: {autoLogonCount}");
            }
            else
            {
                _logger.LogInformation("  AutoLogonCount: <Not Available>");
            }

            if (curDefaultUserName != null)
            {
                logonUser = curDefaultUserName.ToString();
                _logger.LogInformation($"  DefaultUserName: {logonUser}");
            }
            else
            {
                _logger.LogInformation("  DefaultUserName: <Not Available>");
            }

            if (curDefaultPassword != null)
            {
                logonPwd = curDefaultPassword.ToString();
                _logger.LogInformation("  DefaultPassword: <Not Displayed>");
            }
            else
            {
                _logger.LogInformation("  DefaultPassword: <Not Available>");
            }

            if (curDisableCAD != null)
            {
                if (int.TryParse(curDisableCAD.ToString(), out int disableCAD) == false)
                {
                    disableCAD = -1;
                }

                _logger.LogInformation($"  DisableCAD: {disableCAD}");
            }
            else
            {
                _logger.LogInformation("  DisableCAD: <Not Available>");
            }

            if (autoAdminLogon == 1 && logonUser.Equals("") == false)
            {
                _logger.LogInformation("Automatic logon: CONFIGURED");
                return true;
            }
            else
            {
                _logger.LogInformation("Automatic logon: NOT CONFIGURED");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to inspect automatic logon configuration");
            return false;
        }
    }

    public bool IsDomainUser(string userName, string domainName)
    {
        bool userExists = false;

        try
        {
            using (PrincipalContext domainContext = new(ContextType.Domain, domainName))
            {
                using (var foundUser = UserPrincipal.FindByIdentity(domainContext, IdentityType.SamAccountName, userName))
                {
                    if (foundUser != null)
                    {
                        userExists = true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to validate domain user credentials");
        }

        return userExists;
    }

    public bool IsLocalUser(string userName)
    {
        bool userExists = false;

        try
        {
            using (PrincipalContext localContext = new(ContextType.Machine))
            {
                using (var foundUser = UserPrincipal.FindByIdentity(localContext, IdentityType.SamAccountName, userName))
                {
                    if (foundUser != null)
                    {
                        userExists = true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to validate local user credentials");
        }

        return userExists;
    }

    public bool IsTokenElevated(IntPtr hToken)
    {
        if (Environment.OSVersion.Version.Major >= 6)
        {
            int cbSize = sizeof(NativeMethods.TOKEN_ELEVATION_TYPE);
            IntPtr pElevationType = Marshal.AllocHGlobal(cbSize);

            if (pElevationType == IntPtr.Zero)
            {
                _logger.LogError("Failed to allocate memory for token elevation check");
                Marshal.FreeHGlobal(hToken);
                return false;
            }

            if (NativeMethods.GetTokenInformation(hToken,
                NativeMethods.TOKEN_INFORMATION_CLASS.TokenElevationType,
                pElevationType,
                cbSize,
                out cbSize) == false)
            {
                _logger.LogError($"Failed to query user-token elevation type [GetTokenInformation={Marshal.GetLastWin32Error()}]");

                Marshal.FreeHGlobal(hToken);
                Marshal.FreeHGlobal(pElevationType);
                return false;
            }

            NativeMethods.TOKEN_ELEVATION_TYPE elevType = (NativeMethods.TOKEN_ELEVATION_TYPE)Marshal.ReadInt32(pElevationType);

            if (elevType == NativeMethods.TOKEN_ELEVATION_TYPE.TokenElevationTypeLimited)
            {
                /* Type 3 is a limited token with administrative privileges removed
                 * and administrative groups disabled. The limited token is used when
                 * User Account Control is enabled, the application does not require
                 * administrative privilege, and the user does not choose to start
                 * the program using Run as administrator.*/

                _logger.LogInformation("Token elevation type: Limited");
                Marshal.FreeHGlobal(hToken);
                Marshal.FreeHGlobal(pElevationType);
                return false;
            }
            else if (elevType == NativeMethods.TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault)
            {
                /* Type 1 is a full token with no privileges removed or groups disabled.
                 * A full token is only used if User Account Control is disabled or if
                 * the user is the built -in Administrator account (for which UAC 
                 * disabled by default), service account or local system account.*/

                _logger.LogInformation("Token elevation type: Default");
                Marshal.FreeHGlobal(hToken);
                Marshal.FreeHGlobal(pElevationType);
                return true;
            }
            else if (elevType == NativeMethods.TOKEN_ELEVATION_TYPE.TokenElevationTypeFull)
            {
                /* Type 2 is an elevated token with no privileges removed or groups
                 * disabled. An elevated token is used when User Account Control is
                 * enabled and the user chooses to start the program using Run as
                 * administrator. An elevated token is also used when an application
                 * is configured to always require administrative privilege or to
                 * always require maximum privilege, and the user is a member of the
                 * Administrators group.*/

                _logger.LogInformation("Token elevation type: Full");
                Marshal.FreeHGlobal(hToken);
                Marshal.FreeHGlobal(pElevationType);
                return true;
            }
            else
            {
                _logger.LogInformation("Token elevation type: Unknown");
                Marshal.FreeHGlobal(hToken);
                Marshal.FreeHGlobal(pElevationType);
                return false;
            }
        }
        else
        {
            Marshal.FreeHGlobal(hToken);
            return true;
        }
    }

    public bool IsUACEnabled()
    {
        bool isUserAccountControlEnabled = false;

        try
        {
            if (Environment.Is64BitOperatingSystem)
            {
                RegistryKey localMachine64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                RegistryKey systemPolicies = localMachine64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");

                if (systemPolicies != null)
                {
                    int enableLua = int.Parse(systemPolicies.GetValue("EnableLUA").ToString());

                    if (enableLua == 1)
                    {
                        _logger.LogInformation("User account control (UAC): Enabled");
                        isUserAccountControlEnabled = true;
                    }
                    else
                    {
                        _logger.LogInformation("User account control (UAC): Disabled");
                        isUserAccountControlEnabled = false;
                    }
                }

                localMachine64.Dispose();
            }
            else
            {
                RegistryKey localMachine32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                RegistryKey systemPolicies = localMachine32.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");

                if (systemPolicies != null)
                {
                    int enableLua = int.Parse(systemPolicies.GetValue("EnableLUA").ToString());

                    if (enableLua == 1)
                    {
                        _logger.LogInformation("User account control (UAC): Enabled");
                        isUserAccountControlEnabled = true;
                    }
                    else
                    {
                        _logger.LogInformation("User account control (UAC): Disabled");
                        isUserAccountControlEnabled = false;
                    }
                }

                localMachine32.Dispose();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to determine if UAC is enabled");
            return isUserAccountControlEnabled;
        }

        return isUserAccountControlEnabled;
    }

    public bool IsUserInAdminGroup(IntPtr hToken)
    {
        // Is UAC enabled?
        /*
         * Note: In Windows Vista and newer, one cannot simply check if the user account is in
         *       the administrators group. It depends on whether or not the user possesses an
         *       elevated token. To do this, we must query the user's access token, and check
         *       for a linked token that indicates they have elevation privileges or not.
         *       Otherwise, you may get a false negative, e.g. the user is an admin, but
         *       UserPrincipal.IsInRole() returns false. Ohh the simpler times. We miss them.
         *       I feel like .NET needs a native library for this. I dislike having to user
         *       unmanaged code.
         */
        if (IsUACEnabled())
        {
            bool fInAdminGroup = false;
            IntPtr hTokenToCheck = IntPtr.Zero;
            IntPtr pElevationType = IntPtr.Zero;
            IntPtr pLinkedToken = IntPtr.Zero;
            int cbSize = 0;

            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    cbSize = sizeof(NativeMethods.TOKEN_ELEVATION_TYPE);
                    pElevationType = Marshal.AllocHGlobal(cbSize);

                    if (pElevationType == IntPtr.Zero)
                    {
                        _logger.LogError("Failed to allocate memory for token elevation check");
                        return false;
                    }

                    if (NativeMethods.GetTokenInformation(hToken,
                                                          NativeMethods.TOKEN_INFORMATION_CLASS.TokenElevationType,
                                                          pElevationType,
                                                          cbSize,
                                                          out cbSize) == false)
                    {
                        _logger.LogError($"Failed to query token elevation type [GetTokenInformation={Marshal.GetLastWin32Error()}]");
                        return false;
                    }

                    NativeMethods.TOKEN_ELEVATION_TYPE elevType = (NativeMethods.TOKEN_ELEVATION_TYPE)Marshal.ReadInt32(pElevationType);

                    if (elevType == NativeMethods.TOKEN_ELEVATION_TYPE.TokenElevationTypeLimited)
                    {
                        _logger.LogInformation("Token elevation type: Limited");
                        cbSize = IntPtr.Size;
                        pLinkedToken = Marshal.AllocHGlobal(cbSize);

                        if (pLinkedToken == IntPtr.Zero)
                        {
                            _logger.LogError("Failed to allocate memory for linked token check");
                            return false;
                        }

                        if (NativeMethods.GetTokenInformation(hToken,
                                                              NativeMethods.TOKEN_INFORMATION_CLASS.TokenLinkedToken,
                                                              pLinkedToken,
                                                              cbSize,
                                                              out cbSize) == false)
                        {
                            _logger.LogError($"Failed to query LINKED token [GetTokenInformation={Marshal.GetLastWin32Error()}]");
                            return false;
                        }
                        else
                        {
                            _logger.LogDebug("Token has a Linked token");
                        }

                        hTokenToCheck = Marshal.ReadIntPtr(pLinkedToken);
                    }
                    else if (elevType == NativeMethods.TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault)
                    {
                        _logger.LogDebug("Token elevation type: Default");
                    }
                    else if (elevType == NativeMethods.TOKEN_ELEVATION_TYPE.TokenElevationTypeFull)
                    {
                        _logger.LogDebug("Token elevation type: Full");
                    }
                    else
                    {
                        _logger.LogDebug("Token elevation type: Unknown");
                    }
                }

                if (hTokenToCheck == IntPtr.Zero)
                {
                    if (NativeMethods.DuplicateToken(hToken,
                                                     NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityIdentification,
                                                     out hTokenToCheck) == false)
                    {
                        _logger.LogError($"Failed to duplicate ORIGNAL access token [DuplicateToken={Marshal.GetLastWin32Error()}]");
                        return false;
                    }
                }

                WindowsIdentity id = new(hTokenToCheck);
                WindowsPrincipal principal = new(id);
                fInAdminGroup = principal.IsInRole(WindowsBuiltInRole.Administrator);
                id.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to verify if user token is in admin group");
                return false;
            }

            finally
            {
                if (hToken != IntPtr.Zero)
                {
                    hToken = IntPtr.Zero;
                }

                if (hTokenToCheck != IntPtr.Zero)
                {
                    hTokenToCheck = IntPtr.Zero;
                }

                if (pElevationType != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pElevationType);
                    pElevationType = IntPtr.Zero;
                }

                if (pLinkedToken != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pLinkedToken);
                    pLinkedToken = IntPtr.Zero;
                }
            }

            return fInAdminGroup;
        }
        else
        {
            WindowsIdentity userId = new(hToken);
            WindowsPrincipal userPrincipal = new(userId);

            if (userPrincipal.IsInRole("Administrators") || userPrincipal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                userId.Dispose();
                return true;
            }
            else
            {
                userId.Dispose();
                return false;
            }
        }
    }

    public bool IsUserInAdminGroup(WindowsIdentity userToCheck)
    {
        if (new WindowsPrincipal(userToCheck).IsInRole(WindowsBuiltInRole.Administrator))
        {
            return true;
        }

        try
        {
            uint entriesRead = 0, totalEntries = 0;

            unsafe
            {
                int LOCALGROUP_INFO_1_SIZE = sizeof(NativeMethods.LOCALGROUP_INFO_1);
                int LOCALGROUP_MEMBERS_INFO_1_SIZE = sizeof(NativeMethods.LOCALGROUP_MEMBERS_INFO_1);
                IntPtr groupInfoPtr, userInfoPtr;
                groupInfoPtr = IntPtr.Zero;
                userInfoPtr = IntPtr.Zero;

                NativeMethods.NetLocalGroupEnum(IntPtr.Zero,
                                                1,
                                                ref groupInfoPtr,
                                                0xFFFFFFFF,
                                                ref entriesRead,
                                                ref totalEntries,
                                                IntPtr.Zero);

                for (int i = 0; i < totalEntries; i++)
                {
                    int newOffset = 0;
                    long newOffset64 = 0;
                    NativeMethods.LOCALGROUP_INFO_1 groupInfo;

                    if (Environment.Is64BitOperatingSystem)
                    {
                        newOffset64 = groupInfoPtr.ToInt64() + LOCALGROUP_INFO_1_SIZE * i;
                        groupInfo = (NativeMethods.LOCALGROUP_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset64),
                                                                                            typeof(NativeMethods.LOCALGROUP_INFO_1));
                    }
                    else
                    {
                        newOffset = groupInfoPtr.ToInt32() + LOCALGROUP_INFO_1_SIZE * i;
                        groupInfo = (NativeMethods.LOCALGROUP_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset),
                                                                                            typeof(NativeMethods.LOCALGROUP_INFO_1));
                    }

                    string currentGroupName = Marshal.PtrToStringAuto(groupInfo.lpszGroupName);

                    _logger.LogDebug($"Group: {currentGroupName}");

                    if (currentGroupName.ToLower().Equals("administrators"))
                    {
                        uint entriesRead1 = 0, totalEntries1 = 0;
                        NativeMethods.NetLocalGroupGetMembers(IntPtr.Zero,
                                                              groupInfo.lpszGroupName,
                                                              1,
                                                              ref userInfoPtr,
                                                              0xFFFFFFFF,
                                                              ref entriesRead1,
                                                              ref totalEntries1,
                                                              IntPtr.Zero);

                        for (int j = 0; j < totalEntries1; j++)
                        {
                            NativeMethods.LOCALGROUP_MEMBERS_INFO_1 memberInfo;
                            int newOffset1 = 0;
                            long newOffset1_64 = 0;

                            if (Environment.Is64BitOperatingSystem)
                            {
                                newOffset1_64 = userInfoPtr.ToInt64() + LOCALGROUP_MEMBERS_INFO_1_SIZE * j;
                                memberInfo = (NativeMethods.LOCALGROUP_MEMBERS_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset1_64),
                                                                                                             typeof(NativeMethods.LOCALGROUP_MEMBERS_INFO_1));
                            }
                            else
                            {
                                newOffset1 = userInfoPtr.ToInt32() + LOCALGROUP_MEMBERS_INFO_1_SIZE * j;
                                memberInfo = (NativeMethods.LOCALGROUP_MEMBERS_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset1),
                                                                                                             typeof(NativeMethods.LOCALGROUP_MEMBERS_INFO_1));
                            }

                            string currentUserName = Marshal.PtrToStringAuto(memberInfo.lgrmi1_name);

                            _logger.LogDebug($"  Member: {currentUserName}");

                            if (currentUserName.ToLower().Equals(userToCheck.Name.ToLower()) 
                                || (userToCheck.Name.Contains("\\") 
                                    && currentUserName.ToLower().Equals(
                                        userToCheck.Name.ToLower().Substring(
                                            userToCheck.Name.IndexOf("\\") + 1))))
                            {
                                NativeMethods.NetApiBufferFree(userInfoPtr);
                                NativeMethods.NetApiBufferFree(groupInfoPtr);
                                return true;
                            }
                        }

                        NativeMethods.NetApiBufferFree(userInfoPtr);
                        break;
                    }
                }

                NativeMethods.NetApiBufferFree(groupInfoPtr);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to determine admin group membership [NativeMethods.method]");
        }

        return false;
    }

    public bool IsUserInAdminGroup(string userName)
    {
        try
        {
            uint entriesRead = 0, totalEntries = 0;

            unsafe
            {
                int LOCALGROUP_INFO_1_SIZE = sizeof(NativeMethods.LOCALGROUP_INFO_1);
                int LOCALGROUP_MEMBERS_INFO_1_SIZE = sizeof(NativeMethods.LOCALGROUP_MEMBERS_INFO_1);
                IntPtr groupInfoPtr, userInfoPtr;
                groupInfoPtr = IntPtr.Zero;
                userInfoPtr = IntPtr.Zero;

                NativeMethods.NetLocalGroupEnum(IntPtr.Zero,
                                                1,
                                                ref groupInfoPtr,
                                                0xFFFFFFFF,
                                                ref entriesRead,
                                                ref totalEntries,
                                                IntPtr.Zero);

                for (int i = 0; i < totalEntries; i++)
                {
                    int newOffset = 0;
                    long newOffset64 = 0;
                    NativeMethods.LOCALGROUP_INFO_1 groupInfo;

                    if (Environment.Is64BitOperatingSystem)
                    {
                        newOffset64 = groupInfoPtr.ToInt64() + LOCALGROUP_INFO_1_SIZE * i;
                        groupInfo = (NativeMethods.LOCALGROUP_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset64),
                                                                                            typeof(NativeMethods.LOCALGROUP_INFO_1));
                    }
                    else
                    {
                        newOffset = groupInfoPtr.ToInt32() + LOCALGROUP_INFO_1_SIZE * i;
                        groupInfo = (NativeMethods.LOCALGROUP_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset),
                                                                                            typeof(NativeMethods.LOCALGROUP_INFO_1));
                    }

                    string currentGroupName = Marshal.PtrToStringAuto(groupInfo.lpszGroupName);

                    _logger.LogDebug($"Group: {currentGroupName}");

                    if (currentGroupName.ToLower().Equals("administrators"))
                    {
                        uint entriesRead1 = 0, totalEntries1 = 0;
                        NativeMethods.NetLocalGroupGetMembers(IntPtr.Zero,
                                                              groupInfo.lpszGroupName,
                                                              1,
                                                              ref userInfoPtr,
                                                              0xFFFFFFFF,
                                                              ref entriesRead1,
                                                              ref totalEntries1,
                                                              IntPtr.Zero);

                        for (int j = 0; j < totalEntries1; j++)
                        {
                            NativeMethods.LOCALGROUP_MEMBERS_INFO_1 memberInfo;
                            int newOffset1 = 0;
                            long newOffset1_64 = 0;

                            if (Environment.Is64BitOperatingSystem)
                            {
                                newOffset1_64 = userInfoPtr.ToInt64() + LOCALGROUP_MEMBERS_INFO_1_SIZE * j;
                                memberInfo = (NativeMethods.LOCALGROUP_MEMBERS_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset1_64),
                                                                                                             typeof(NativeMethods.LOCALGROUP_MEMBERS_INFO_1));
                            }
                            else
                            {
                                newOffset1 = userInfoPtr.ToInt32() + LOCALGROUP_MEMBERS_INFO_1_SIZE * j;
                                memberInfo = (NativeMethods.LOCALGROUP_MEMBERS_INFO_1)Marshal.PtrToStructure(new IntPtr(newOffset1),
                                                                                                             typeof(NativeMethods.LOCALGROUP_MEMBERS_INFO_1));
                            }

                            string currentUserName = Marshal.PtrToStringAuto(memberInfo.lgrmi1_name);

                            _logger.LogDebug($"  Member: {currentUserName}");

                            if (currentUserName.ToLower().Equals(userName.ToLower()) 
                                || (userName.Contains("\\") 
                                    && currentUserName.ToLower().Equals(
                                        userName.ToLower().Substring(
                                            userName.IndexOf("\\") + 1))))
                            {
                                NativeMethods.NetApiBufferFree(userInfoPtr);
                                NativeMethods.NetApiBufferFree(groupInfoPtr);
                                return true;
                            }
                        }

                        NativeMethods.NetApiBufferFree(userInfoPtr);
                        break;
                    }
                }

                NativeMethods.NetApiBufferFree(groupInfoPtr);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to determine admin group membership [NativeMethods.method]");
        }

        return false;
    }

    public bool RemoveFromSystemPath(string folder)
    {
        try
        {
            RegistryKey pathKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment\", true);
            string cleanPath = "";

            foreach (string strPath in pathKey.GetValue("PATH",
                                                        null,
                                                        RegistryValueOptions.DoNotExpandEnvironmentNames).ToString().Split(';'))
            {
                if (strPath.ToLower().Equals(folder.ToLower()))
                {
                    _logger.LogInformation($"Remove from PATH: {folder}");
                }
                else
                {
                    cleanPath += strPath + ";";
                }
            }

            pathKey.SetValue("Path", cleanPath, RegistryValueKind.ExpandString);
            pathKey.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to remove {folder} to system PATH variable");
            return false;
        }

        return true;
    }

    public bool RebootSystem(uint delaySeconds = 10,
                             string comment = null,
                             NativeMethods.ShutdownReason shutdownReason =
                                NativeMethods.ShutdownReason.MajorOther 
                                | NativeMethods.ShutdownReason.MinorOther)
    {
        IntPtr hProcess = Process.GetCurrentProcess().Handle;

        if (NativeMethods.OpenProcessToken(hProcess,
                                           NativeMethods.TOKEN_ALL_ACCESS,
                                           out IntPtr hToken) == false)
        {
            _logger.LogError($"Unable to open specified process token [OpenProcessToken={Marshal.GetLastWin32Error()}]");
            Marshal.FreeHGlobal(hProcess);
            return false;
        }

        if (EnablePrivilege(hToken, NativeMethods.SE_SHUTDOWN_NAME) == false)
        {
            _logger.LogWarning("Failed to enable privilege [SeShutdownPrivilege]");
            Marshal.FreeHGlobal(hProcess);
            Marshal.FreeHGlobal(hToken);
            return false;
        }

        Marshal.FreeHGlobal(hProcess);
        Marshal.FreeHGlobal(hToken);

        if (comment == null || comment == "")
        {
            string processName = Process.GetCurrentProcess().MainModule.FileName;
            string shortName = processName.Substring(processName.LastIndexOf("\\") + 1);
            string friendlyName = shortName.Substring(0, shortName.LastIndexOf("."));
            comment = friendlyName + " initiated a reboot of the system";
        }

        _logger.LogInformation($"Windows reboot [{comment}]");

        if (NativeMethods.InitiateSystemShutdownEx(null,
                                                   comment,
                                                   delaySeconds,
                                                   true,
                                                   true,
                                                   shutdownReason) == false)
        {
            int lastError = Marshal.GetLastWin32Error();

            /* Is this an unexpected error code? 
                 1115/0x45B --> A system shutdown is in progress.     
                 1190/0x4A6 --> A system shutdown has already been scheduled.
            */
            if (lastError != 1115 && lastError != 1190)
            {
                _logger.LogError($"Failed to initiate reboot [InitiateSystemShutdownEx={Marshal.GetLastWin32Error()}]");
                return false;
            }
            else if (lastError == 1115)
            {
                _logger.LogInformation("REBOOT: A system shutdown is in progress");
            }
            else if (lastError == 1190)
            {
                _logger.LogInformation("REBOOT: A system shutdown has already been scheduled");
            }
        }
        else
        {
            _logger.LogInformation($"REBOOT: System will restart in ({delaySeconds}) seconds");
        }

        return true;
    }

    public string[] SynthesizeCommandLineArgs()
    {
        StringBuilder argCrawler = new(Environment.CommandLine);
        char nextChar = '\0';
        char currentChar = '\0';
        char previousChar = '\0';
        int quoteLevel = 0;
        bool inQuote = false;

        for (int i = 0; i < argCrawler.Length; i++)
        {
            // Ternary character scope
            previousChar = currentChar;
            currentChar = argCrawler[i];

            // Are we near end of string?
            if (i < argCrawler.Length - 1)
            {
                // Scope next character
                nextChar = argCrawler[i + 1];
            }
            else
            {
                // Stub null char
                nextChar = '\0';
            }

            // Is this a START QUOTE?
            if ((previousChar == '\0' && currentChar == '\"' && nextChar != '\0') || (previousChar == ' ' && currentChar == '\"' && nextChar != '\0'))
            {
                inQuote = true;
                quoteLevel += 1;
            }

            // Is this an END QUOTE?
            if (inQuote && ((currentChar == '\"' && nextChar == ' ') || (currentChar == '\"' && nextChar == '\0')))
            {
                quoteLevel -= 1;

                if (quoteLevel == 0)
                {
                    inQuote = false;
                }
            }

            // Is this a space character, outside of quoted text?
            if (argCrawler[i].Equals(' ') && inQuote == false)
            {
                argCrawler[i] = '\n';
            }
        }

        string[] synthArgs = argCrawler.ToString().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (synthArgs.Length > 1)
        {
            for (int i = 1; i < synthArgs.Length; i++)
            {
                // If quoted, unquote the argument 
                // Note: Quotes were needed to distinctly identify the argument from other arguments, but otherwise serve no purpose.
                // Note: Carful not to trim quotes-- we only want to trim a single/outer mathcing pair.

                if (synthArgs[i].StartsWith("\"") && synthArgs[i].EndsWith("\""))
                {
                    synthArgs[i] = synthArgs[i].Substring(1, synthArgs[i].Length - 2);
                }

                _logger.LogInformation($"Argument [{i}]: {synthArgs[i]}");
            }
        }
        else
        {
            _logger.LogInformation("Arguments: <None>");
        }

        return synthArgs;
    }
}
