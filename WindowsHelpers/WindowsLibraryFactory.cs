using LoggerLibrary.Interfaces;

namespace WindowsLibrary;

public class WindowsLibraryFactory : IWindowsLibraryFactory
{
    public FileSystemHelper GetFilesystemHelper(ISimpleLogger logFile) => new FileSystemHelper(logFile);
    public HostsFileHelper GetHostsFileHelper() => new HostsFileHelper();
    public NetworkAdapterHelper GetNetworkAdapterHelper(ISimpleLogger logFile) => new NetworkAdapterHelper(logFile);
    public NetworkHelper GetNetworkHelper(ISimpleLogger logFile) => new NetworkHelper(logFile);
    public PageFileHelper GetPageFileHelper(ISimpleLogger logFile) => new PageFileHelper(logFile);
    public ProcessHelper GetProcessHelper(ISimpleLogger logFile) => new ProcessHelper(logFile);
    public RegistryHelper GetRegistryHelper(ISimpleLogger logFile) => new RegistryHelper(logFile);
    public ServiceHelper GetServiceHelper(ISimpleLogger logFile) => new ServiceHelper(logFile);
    public WindowsHelper GetWindowsHelper(ISimpleLogger logFile) => new WindowsHelper(logFile);
    public WmiHelper GetWmiHelper(ISimpleLogger logFile) => new WmiHelper(logFile);
}
