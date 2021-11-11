using Microsoft.Extensions.DependencyInjection;

namespace WindowsLibrary;

public static class WindowsLibraryInjection
{
    public static IServiceCollection AddWindowsHelpers(this IServiceCollection services)
    {
        services.AddSingleton<FileSystemHelper>();
        services.AddSingleton<HostsFileHelper>();
        services.AddSingleton<NetworkAdapterHelper>();
        services.AddSingleton<NetworkHelper>();
        services.AddSingleton<NumericComparer>();
        services.AddSingleton<PageFileHelper>();
        services.AddSingleton<ProcessHelper>();
        services.AddSingleton<RegistryHelper>();
        services.AddSingleton<ServiceHelper>();
        services.AddSingleton<WindowsHelper>();
        services.AddSingleton<WmiHelper>();
        return services;
    }
}
