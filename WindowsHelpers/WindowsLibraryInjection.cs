using Microsoft.Extensions.DependencyInjection;

namespace WindowsLibrary;

public static class WindowsLibraryInjection
{
    public static IServiceCollection AddWindowsHelpers(this IServiceCollection services)
    {
        services.AddTransient<FileSystemHelper>();
        services.AddTransient<HostsFileHelper>();
        services.AddTransient<NetworkAdapterHelper>();
        services.AddTransient<NetworkHelper>();
        services.AddTransient<NumericComparer>();
        services.AddTransient<PageFileHelper>();
        services.AddTransient<ProcessHelper>();
        services.AddTransient<RegistryHelper>();
        services.AddTransient<ServiceHelper>();
        services.AddTransient<WindowsHelper>();
        services.AddTransient<WmiHelper>();
        return services;
    }
}
