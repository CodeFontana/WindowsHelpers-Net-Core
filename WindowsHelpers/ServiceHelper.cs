using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace WindowsLibrary;

public class ServiceHelper
{
    private readonly ILogger<ServiceHelper> _logger;
    private readonly ProcessHelper _psHelper;

    public ServiceHelper(ILogger<ServiceHelper> logger,
                         ProcessHelper psHelper)
    {
        _logger = logger;
        _psHelper = psHelper;
    }

    public enum ServiceStart // For reference.
    {
        Boot = 0,
        System = 1,
        Automatic = 2,
        Manual = 3,
        Disabled = 4
    }

    public bool ChangeLogonUser(string serviceName, string logonUser, string logonPassword)
    {
        /* Built-in logonUsers:
         *   Local Service: logonUser="nt authority\\localservice"  logonPassword=""
         *   Local System:  logonUser=".\\localsystem"              logonPassword=""
         */

        IntPtr scManagerHandle = IntPtr.Zero;
        IntPtr serviceHandle = IntPtr.Zero;

        try
        {
            scManagerHandle = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);

            if (scManagerHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to open service control manager");
                return false;
            }

            serviceHandle = NativeMethods.OpenService(
                scManagerHandle,
                serviceName,
                NativeMethods.SERVICE_QUERY_CONFIG | NativeMethods.SERVICE_CHANGE_CONFIG);

            if (serviceHandle == IntPtr.Zero)
            {
                _logger.LogError($"Unable to open specified service [{serviceName}]");
                return false;
            }

            var configSuccess = NativeMethods.ChangeServiceConfig(serviceHandle,
                                                                  NativeMethods.SERVICE_NO_CHANGE,
                                                                  NativeMethods.SERVICE_NO_CHANGE,
                                                                  NativeMethods.SERVICE_NO_CHANGE,
                                                                  null,
                                                                  null,
                                                                  IntPtr.Zero,
                                                                  null,
                                                                  logonUser,
                                                                  logonPassword,
                                                                  null);

            if (configSuccess == false)
            {
                _logger.LogError($"Unable to configure service logon user [ChangeServiceConfig={Marshal.GetLastWin32Error()}]");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to change service logon user");
        }
        finally
        {
            if (serviceHandle != IntPtr.Zero)
                NativeMethods.CloseServiceHandle(serviceHandle);
            if (scManagerHandle != IntPtr.Zero)
                NativeMethods.CloseServiceHandle(scManagerHandle);
        }

        return true;
    }

    public bool ChangeStartMode(string serviceName, ServiceStartMode startMode)
    {
        IntPtr scManagerHandle = IntPtr.Zero;
        IntPtr serviceHandle = IntPtr.Zero;

        try
        {
            scManagerHandle = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);

            if (scManagerHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to open service control manager");
                return false;
            }

            serviceHandle = NativeMethods.OpenService(
                scManagerHandle,
                serviceName,
                NativeMethods.SERVICE_QUERY_CONFIG | NativeMethods.SERVICE_CHANGE_CONFIG);

            if (serviceHandle == IntPtr.Zero)
            {
                _logger.LogError($"Unable to open specified service [{serviceName}]");
                return false;
            }

            var configSuccess = NativeMethods.ChangeServiceConfig(serviceHandle,
                                                                  NativeMethods.SERVICE_NO_CHANGE,
                                                                  (uint)startMode,
                                                                  NativeMethods.SERVICE_NO_CHANGE,
                                                                  null,
                                                                  null,
                                                                  IntPtr.Zero,
                                                                  null,
                                                                  null,
                                                                  null,
                                                                  null);

            if (configSuccess == false)
            {
                _logger.LogError($"Unable to configure service startup mode [ChangeServiceConfig={Marshal.GetLastWin32Error().ToString()}]");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to change service startup mode");
        }
        finally
        {
            if (serviceHandle != IntPtr.Zero)
            {
                NativeMethods.CloseServiceHandle(serviceHandle);
            }

            if (scManagerHandle != IntPtr.Zero)
            {
                NativeMethods.CloseServiceHandle(scManagerHandle);
            }
        }

        return true;
    }

    public bool ConfigureDescription(string serviceName, string description)
    {
        IntPtr scManagerHandle = IntPtr.Zero;
        IntPtr scManagerLockHandle = IntPtr.Zero;
        IntPtr serviceHandle = IntPtr.Zero;

        try
        {
            if (ServiceExists(serviceName) == false)
            {
                _logger.LogError($"Service does not exist [{serviceName}]");
                return false;
            }

            scManagerHandle = NativeMethods.OpenSCManagerA(
                null, null,
                NativeMethods.ServiceControlManagerType.SC_MANAGER_ALL_ACCESS);

            if (scManagerHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to open service control manager");
                return false;
            }

            scManagerLockHandle = NativeMethods.LockServiceDatabase(scManagerHandle);

            if (scManagerLockHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to lock service control manager database");
                return false;
            }

            serviceHandle = NativeMethods.OpenServiceA(scManagerHandle,
                                                       serviceName,
                                                       NativeMethods.ACCESS_TYPE.SERVICE_ALL_ACCESS);

            if (serviceHandle == IntPtr.Zero)
            {
                _logger.LogError($"Unable to open specified service [{serviceName}]");
                return false;
            }

            NativeMethods.SERVICE_DESCRIPTION serviceDesc;
            serviceDesc.lpDescription = description;

            bool configSuccess = NativeMethods.ChangeServiceConfig2A(serviceHandle,
                                                                     NativeMethods.InfoLevel.SERVICE_CONFIG_DESCRIPTION,
                                                                     ref serviceDesc);

            if (configSuccess == false)
            {
                _logger.LogError($"Unable to configure service failure actions [ChangeServiceConfig2A={Marshal.GetLastWin32Error()}]");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to configure service failure actions");
        }
        finally
        {
            if (serviceHandle != IntPtr.Zero) { NativeMethods.CloseServiceHandle(serviceHandle); }
            if (scManagerLockHandle != IntPtr.Zero) { NativeMethods.UnlockServiceDatabase(scManagerLockHandle); }
            if (scManagerHandle != IntPtr.Zero) { NativeMethods.CloseServiceHandle(scManagerHandle); }
        }

        return true;
    }

    public bool ConfigureRestartActions(string serviceName)
    {
        /* Note: For now, this function is hard-coded to set recovery actions
         *       that will restart the service, waiting 60s between restart
         *       attempts. It could be enhanced in the future, to take these
         *       actions as parameters, and configure accordingly.
         */

        IntPtr scManagerHandle = IntPtr.Zero;
        IntPtr actionsBuffer = IntPtr.Zero;
        IntPtr scManagerLockHandle = IntPtr.Zero;
        IntPtr serviceHandle = IntPtr.Zero;

        try
        {
            if (ServiceExists(serviceName) == false)
            {
                _logger.LogError($"Service does not exist [{serviceName}]");
                return false;
            }

            scManagerHandle = NativeMethods.OpenSCManagerA(null,
                                                           null,
                                                           NativeMethods.ServiceControlManagerType.SC_MANAGER_ALL_ACCESS);

            if (scManagerHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to open service control manager");
                return false;
            }

            scManagerLockHandle = NativeMethods.LockServiceDatabase(scManagerHandle);

            if (scManagerLockHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to lock service control manager database");
                return false;
            }

            serviceHandle = NativeMethods.OpenServiceA(scManagerHandle,
                                                       serviceName,
                                                       NativeMethods.ACCESS_TYPE.SERVICE_ALL_ACCESS);

            if (serviceHandle == IntPtr.Zero)
            {
                _logger.LogError($"Unable to open specified service [{serviceName}]");
                return false;
            }

            NativeMethods.SC_ACTION[] scActions = new NativeMethods.SC_ACTION[3];
            NativeMethods.SERVICE_FAILURE_ACTIONS serviceFailureActions; // Reference: https://docs.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_failure_actionsa
            serviceFailureActions.dwResetPeriod = 24 * 3600; // The time after which to reset the failure count to zero if there are no failures, in seconds.
            serviceFailureActions.lpRebootMsg = ""; // No broadcast message.
            serviceFailureActions.lpCommand = null; // If this value is NULL, the command is unchanged.
            serviceFailureActions.cActions = scActions.Length; // (3) failure actions.
            scActions[0].Delay = 60000;
            scActions[0].SCActionType = NativeMethods.SC_ACTION_TYPE.SC_ACTION_RESTART;
            scActions[1].Delay = 60000;
            scActions[1].SCActionType = NativeMethods.SC_ACTION_TYPE.SC_ACTION_RESTART;
            scActions[2].Delay = 60000;
            scActions[2].SCActionType = NativeMethods.SC_ACTION_TYPE.SC_ACTION_RESTART;

            actionsBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(new NativeMethods.SC_ACTION()) * 3);
            NativeMethods.CopyMemory(actionsBuffer, scActions, Marshal.SizeOf(new NativeMethods.SC_ACTION()) * 3);
            serviceFailureActions.lpsaActions = actionsBuffer;

            IntPtr lpInfo = Marshal.AllocHGlobal(Marshal.SizeOf(serviceFailureActions));

            bool configSuccess = NativeMethods.ChangeServiceConfig2A(serviceHandle,
                                                                     NativeMethods.InfoLevel.SERVICE_CONFIG_FAILURE_ACTIONS,
                                                                     ref serviceFailureActions);

            if (configSuccess == false)
            {
                _logger.LogError($"Unable to configure service failure actions [ChangeServiceConfig2A={Marshal.GetLastWin32Error()}]");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to configure service failure actions");
        }
        finally
        {
            if (actionsBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(actionsBuffer); }
            if (serviceHandle != IntPtr.Zero) { NativeMethods.CloseServiceHandle(serviceHandle); }
            if (scManagerLockHandle != IntPtr.Zero) { NativeMethods.UnlockServiceDatabase(scManagerLockHandle); }
            if (scManagerHandle != IntPtr.Zero) { NativeMethods.CloseServiceHandle(scManagerHandle); }
        }

        return true;
    }

    public string GetServiceFolder(string serviceName)
    {
        ManagementClass mc = new("Win32_Service");

        foreach (ManagementObject mo in mc.GetInstances())
        {
            if (mo.GetPropertyValue("Name").ToString().ToLower() == serviceName.ToLower())
            {
                return Path.GetDirectoryName(mo.GetPropertyValue("PathName").ToString().Trim('"'));
            }
        }

        return null;
    }

    public int GetServiceProcessId(ServiceController sc)
    {
        if (sc == null)
        {
            throw new ArgumentNullException("sc");
        }

        IntPtr buffer = IntPtr.Zero;

        try
        {
            uint dwBytesNeeded;

            // Call once to figure the size of the output buffer.
            NativeMethods.QueryServiceStatusEx(sc.ServiceHandle,
                                               NativeMethods.SC_STATUS_PROCESS_INFO,
                                               buffer,
                                               0,
                                               out dwBytesNeeded);

            if (Marshal.GetLastWin32Error() == NativeMethods.ERROR_INSUFFICIENT_BUFFER)
            {
                // Allocate required buffer and call again.
                buffer = Marshal.AllocHGlobal((int)dwBytesNeeded);

                if (NativeMethods.QueryServiceStatusEx(sc.ServiceHandle,
                                                       NativeMethods.SC_STATUS_PROCESS_INFO,
                                                       buffer,
                                                       dwBytesNeeded,
                                                       out dwBytesNeeded))
                {
                    NativeMethods.SERVICE_STATUS_PROCESS ssp = new();
                    Marshal.PtrToStructure(buffer, ssp);
                    return (int)ssp.dwProcessId;
                }
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return -1;
    }

    public bool InstallService(string serviceName,
                               string serviceFileName,
                               string displayName,
                               NativeMethods.SERVICE_START_TYPES startType)
    {
        IntPtr hServiceManager = IntPtr.Zero;
        IntPtr scManagerLockHandle = IntPtr.Zero;
        IntPtr hNewService = IntPtr.Zero;

        try
        {
            hServiceManager = NativeMethods.OpenSCManager(null,
                                                          null,
                                                          NativeMethods.SC_MANAGER_ALL_ACCESS);

            if (hServiceManager == IntPtr.Zero)
            {
                _logger.LogError("Unable to open service control manager");
                return false;
            }

            scManagerLockHandle = NativeMethods.LockServiceDatabase(hServiceManager);

            if (scManagerLockHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to lock service control manager database");
                return false;
            }

            hNewService = NativeMethods.CreateService(hServiceManager,
                                                      serviceName,
                                                      displayName,
                                                      NativeMethods.SC_MANAGER_ALL_ACCESS,
                                                      (uint)NativeMethods.SERVICE_TYPES.SERVICE_WIN32_OWN_PROCESS 
                                                        | (uint)NativeMethods.SERVICE_TYPES.SERVICE_INTERACTIVE_PROCESS,
                                                      (uint)startType,
                                                      (uint)NativeMethods.SERVICE_ERROR_CONTROL.SERVICE_ERROR_NORMAL,
                                                      serviceFileName,
                                                      null,
                                                      IntPtr.Zero,
                                                      null,
                                                      null,
                                                      null);

            if (hNewService == IntPtr.Zero)
            {
                _logger.LogError("Failed to create new Windows service");
                return false;
            }
            else
            {
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create new Windows service");
            return false;
        }
        finally
        {
            if (hNewService != IntPtr.Zero) { NativeMethods.CloseServiceHandle(hNewService); }
            if (scManagerLockHandle != IntPtr.Zero) { NativeMethods.UnlockServiceDatabase(scManagerLockHandle); }
            if (hServiceManager != IntPtr.Zero) { NativeMethods.CloseServiceHandle(hServiceManager); }
        }
    }

    public bool StartService(string serviceName)
    {
        try
        {
            if (ServiceExists(serviceName))
            {
                _logger.LogInformation($"Start service [{serviceName}]...");
                ServiceController sc = new(serviceName);

                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    _logger.LogInformation("Service is already running");
                }
                else
                {
                    sc.Start();
                    _logger.LogInformation("Service started");
                }

                sc.Dispose();
                return true;
            }
            else
            {
                _logger.LogError($"Service does not exist [{serviceName}]");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to start requested service [{serviceName}]");
            return false;
        }
    }

    public bool StopService(string serviceName)
    {
        try
        {
            if (ServiceExists(serviceName))
            {
                ServiceController svcCtrl = new(serviceName);

                if (svcCtrl.Status != ServiceControllerStatus.Stopped)
                {
                    int pid = GetServiceProcessId(svcCtrl);

                    try
                    {
                        _logger.LogInformation($"Service running [{serviceName}], send stop request...");
                        svcCtrl.Stop();
                        svcCtrl.WaitForStatus(ServiceControllerStatus.Stopped,
                            new TimeSpan(0, 2, 0));
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to gracefully stop service");
                    }
                    finally
                    {
                        if (Process.GetProcesses().Any(p => p.Id == pid))
                        {
                            _logger.LogInformation("Killing service...");
                            _psHelper.KillProcess(pid);
                        }

                        _logger.LogInformation("Service stopped");
                    }
                }
                else
                {
                    _logger.LogInformation($"Service not running [{serviceName}]");
                }

                svcCtrl.Dispose();
                return true;
            }
            else
            {
                _logger.LogError($"Service does not exist [{serviceName}]");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to stop requested service [{serviceName}]");
            return false;
        }
    }

    public bool UninstallService(string serviceName)
    {
        IntPtr hServiceManager = IntPtr.Zero;
        IntPtr scManagerLockHandle = IntPtr.Zero;
        IntPtr hServiceHandle = IntPtr.Zero;

        try
        {
            StopService(serviceName);

            hServiceManager = NativeMethods.OpenSCManager(null,
                                                          null,
                                                          NativeMethods.SC_MANAGER_ALL_ACCESS);

            if (hServiceManager == IntPtr.Zero)
            {
                _logger.LogError("Unable to open service control manager");
                return false;
            }

            scManagerLockHandle = NativeMethods.LockServiceDatabase(hServiceManager);

            if (scManagerLockHandle == IntPtr.Zero)
            {
                _logger.LogError("Unable to lock service control manager database");
                return false;
            }

            hServiceHandle = NativeMethods.OpenService(hServiceManager,
                                                       serviceName,
                                                       NativeMethods.DELETE);

            if (hServiceHandle == IntPtr.Zero)
            {
                _logger.LogError($"Failed to open requested service [{serviceName}]");
                return false;
            }

            if (NativeMethods.DeleteService(hServiceHandle))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to delete Windows service [{serviceName}]");
            return false;
        }
        finally
        {
            if (hServiceHandle != IntPtr.Zero) { NativeMethods.CloseServiceHandle(hServiceHandle); }
            if (scManagerLockHandle != IntPtr.Zero) { NativeMethods.UnlockServiceDatabase(scManagerLockHandle); }
            if (hServiceManager != IntPtr.Zero) { NativeMethods.CloseServiceHandle(hServiceManager); }
        }
    }

    public bool IsService(uint pid)
    {
        using (ManagementObjectSearcher Searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE ProcessId = '{pid}'"))
        {
            foreach (ManagementObject service in Searcher.Get())
            {
                return true;
            }
        }

        return false;
    }

    public bool ServiceExists(string serviceName)
    {
        ServiceController[] sc = ServiceController.GetServices();
        var service = sc.FirstOrDefault(s => s.ServiceName.ToLower() == serviceName.ToLower());
        return service != null;
    }
}
