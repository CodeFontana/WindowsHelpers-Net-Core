using LoggerLibrary;

namespace WindowsLibrary;

public class WindowsLibraryFactory : IWindowsLibraryFactory
{
    public FileSystemHelper GetFilesystemHelper(IFileLogger logger) => new FileSystemHelper(logger);
    public HostsFileHelper GetHostsFileHelper() => new HostsFileHelper();
    public NetworkAdapterHelper GetNetworkAdapterHelper(IFileLogger logger) => new NetworkAdapterHelper(logger);
    public NetworkHelper GetNetworkHelper(IFileLogger logger) => new NetworkHelper(logger);
    public PageFileHelper GetPageFileHelper(IFileLogger logger) => new PageFileHelper(logger);
    public ProcessHelper GetProcessHelper(IFileLogger logger) => new ProcessHelper(logger);
    public RegistryHelper GetRegistryHelper(IFileLogger logger) => new RegistryHelper(logger);
    public ServiceHelper GetServiceHelper(IFileLogger logger) => new ServiceHelper(logger);
    public WindowsHelper GetWindowsHelper(IFileLogger logger) => new WindowsHelper(logger);
    public WmiHelper GetWmiHelper(IFileLogger logger) => new WmiHelper(logger);
}
