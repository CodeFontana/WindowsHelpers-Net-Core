using LoggerLibrary;

namespace WindowsLibrary;

public interface IWindowsLibraryFactory
{
    FileSystemHelper GetFilesystemHelper(IFileLogger logger);
    HostsFileHelper GetHostsFileHelper();
    NetworkAdapterHelper GetNetworkAdapterHelper(IFileLogger logger);
    NetworkHelper GetNetworkHelper(IFileLogger logger);
    PageFileHelper GetPageFileHelper(IFileLogger logger);
    ProcessHelper GetProcessHelper(IFileLogger logger);
    RegistryHelper GetRegistryHelper(IFileLogger logger);
    ServiceHelper GetServiceHelper(IFileLogger logger);
    WindowsHelper GetWindowsHelper(IFileLogger logger);
    WmiHelper GetWmiHelper(IFileLogger logger);
}
