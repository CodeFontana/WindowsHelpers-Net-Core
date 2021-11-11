using LoggerLibrary.Interfaces;

namespace WindowsLibrary;

public interface IWindowsLibraryFactory
{
    FileSystemHelper GetFilesystemHelper(ISimpleLogger logFile);
    HostsFileHelper GetHostsFileHelper();
    NetworkAdapterHelper GetNetworkAdapterHelper(ISimpleLogger logFile);
    NetworkHelper GetNetworkHelper(ISimpleLogger logFile);
    PageFileHelper GetPageFileHelper(ISimpleLogger logFile);
    ProcessHelper GetProcessHelper(ISimpleLogger logFile);
    RegistryHelper GetRegistryHelper(ISimpleLogger logFile);
    ServiceHelper GetServiceHelper(ISimpleLogger logFile);
    WindowsHelper GetWindowsHelper(ISimpleLogger logFile);
    WmiHelper GetWmiHelper(ISimpleLogger logFile);
}
