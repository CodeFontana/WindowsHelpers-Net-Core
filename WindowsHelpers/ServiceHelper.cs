using LoggerLibrary;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace WindowsLibrary
{
    [SupportedOSPlatform("windows")]
    public class ServiceHelper
    {
        private ISimpleLogger _logger;

        public ServiceHelper(ISimpleLogger logger)
        {
            _logger = logger;
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
                    _logger.Log("Unable to open service control manager.", SimpleLogger.MsgType.ERROR);
                    return false;
                }

                serviceHandle = NativeMethods.OpenService(
                    scManagerHandle,
                    serviceName,
                    NativeMethods.SERVICE_QUERY_CONFIG | NativeMethods.SERVICE_CHANGE_CONFIG);

                if (serviceHandle == IntPtr.Zero)
                {
                    _logger.Log("Unable to open specified service [" + serviceName + "].", SimpleLogger.MsgType.ERROR);
                    return false;
                }

                var configSuccess = NativeMethods.ChangeServiceConfig(
                    serviceHandle,
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

                if (!configSuccess)
                {
                    _logger.Log("Unable to configure service logon user [ChangeServiceConfig=" +
                        Marshal.GetLastWin32Error().ToString() + "].", SimpleLogger.MsgType.ERROR);
                    return false;
                }
            }
            catch (Exception e)
            {
                _logger.Log(e, "Failed to change service logon user.");
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
                    _logger.Log("Unable to open service control manager.", SimpleLogger.MsgType.ERROR);
                    return false;
                }

                serviceHandle = NativeMethods.OpenService(
                    scManagerHandle,
                    serviceName,
                    NativeMethods.SERVICE_QUERY_CONFIG | NativeMethods.SERVICE_CHANGE_CONFIG);

                if (serviceHandle == IntPtr.Zero)
                {
                    _logger.Log("Unable to open specified service [" + serviceName + "].", SimpleLogger.MsgType.ERROR);
                    return false;
                }

                var configSuccess = NativeMethods.ChangeServiceConfig(
                    serviceHandle,
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

                if (!configSuccess)
                {
                    _logger.Log("Unable to configure service startup mode [ChangeServiceConfig=" +
                        Marshal.GetLastWin32Error().ToString() + "].", SimpleLogger.MsgType.ERROR);
                    return false;
                }
            }
            catch (Exception e)
            {
                _logger.Log(e, "Failed to change service startup mode.");
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
                    _logger.Log($"ERROR: Service does not exist [{serviceName}].");
                    return false;
                }

                scManagerHandle = NativeMethods.OpenSCManagerA(
                    null, null,
                    NativeMethods.ServiceControlManagerType.SC_MANAGER_ALL_ACCESS);

                if (scManagerHandle == IntPtr.Zero)
                {
                    _logger.Log("ERROR: Unable to open service control manager.");
                    return false;
                }

                scManagerLockHandle = NativeMethods.LockServiceDatabase(scManagerHandle);

                if (scManagerLockHandle == IntPtr.Zero)
                {
                    _logger.Log("ERROR: Unable to lock service control manager database.");
                    return false;
                }

                serviceHandle = NativeMethods.OpenServiceA(
                    scManagerHandle,
                    serviceName,
                    NativeMethods.ACCESS_TYPE.SERVICE_ALL_ACCESS);

                if (serviceHandle == IntPtr.Zero)
                {
                    _logger.Log("ERROR: Unable to open specified service [" + serviceName + "].");
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

                bool configSuccess = NativeMethods.ChangeServiceConfig2A(
                    serviceHandle,
                    NativeMethods.InfoLevel.SERVICE_CONFIG_FAILURE_ACTIONS,
                    ref serviceFailureActions);

                if (!configSuccess)
                {
                    _logger.Log("ERROR: Unable to configure service failure actions [ChangeServiceConfig2A=" +
                        Marshal.GetLastWin32Error().ToString() + "].");
                    return false;
                }
            }
            catch (Exception e)
            {
                _logger.Log(e, "Failed to configure service failure actions.");
            }
            finally
            {
                if (actionsBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(actionsBuffer);
                }

                if (serviceHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseServiceHandle(serviceHandle);
                }

                if (scManagerLockHandle != IntPtr.Zero)
                {
                    NativeMethods.UnlockServiceDatabase(scManagerLockHandle);
                }

                if (scManagerHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseServiceHandle(scManagerHandle);
                }
            }

            return true;
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
                UInt32 dwBytesNeeded;
                
                // Call once to figure the size of the output buffer.
                NativeMethods.QueryServiceStatusEx(
                    sc.ServiceHandle, 
                    NativeMethods.SC_STATUS_PROCESS_INFO, 
                    buffer, 
                    0, 
                    out dwBytesNeeded);

                if (Marshal.GetLastWin32Error() == NativeMethods.ERROR_INSUFFICIENT_BUFFER)
                {
                    // Allocate required buffer and call again.
                    buffer = Marshal.AllocHGlobal((int)dwBytesNeeded);

                    if (NativeMethods.QueryServiceStatusEx(
                        sc.ServiceHandle, 
                        NativeMethods.SC_STATUS_PROCESS_INFO, 
                        buffer, 
                        dwBytesNeeded, 
                        out dwBytesNeeded))
                    {
                        var ssp = new NativeMethods.SERVICE_STATUS_PROCESS();
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

        public bool ServiceExists(string serviceName)
        {
            ServiceController[] sc = ServiceController.GetServices();
            var service = sc.FirstOrDefault(s => s.ServiceName.ToLower() == serviceName.ToLower());
            return service != null;
        }
    }
}