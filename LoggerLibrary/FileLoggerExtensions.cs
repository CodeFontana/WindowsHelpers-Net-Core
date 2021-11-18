using LoggerLibrary;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Logging
{
    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName));
            builder.Services.AddSingleton<IFileLoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddSingleton<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder));
            builder.Services.AddSingleton<IFileLoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddSingleton<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder, long logMaxBytes)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder, logMaxBytes));
            builder.Services.AddSingleton<IFileLoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddSingleton<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder, long logMaxBytes, uint logMaxCount)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder, logMaxBytes, logMaxCount));
            builder.Services.AddSingleton<IFileLoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddSingleton<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            return builder;
        }
    }
}
