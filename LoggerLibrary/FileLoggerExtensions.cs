using LoggerLibrary;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Logging
{
    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName));
            builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder));
            builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder, long logMaxBytes)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder, logMaxBytes));
            builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logName, string logFolder, long logMaxBytes, uint logMaxCount)
        {
            builder.Services.AddSingleton(new FileLoggerProvider(logName, logFolder, logMaxBytes, logMaxCount));
            builder.Services.AddTransient<ILoggerProvider>(x => x.GetRequiredService<FileLoggerProvider>());
            builder.Services.AddTransient<IFileLogger>(x => x.GetRequiredService<FileLoggerProvider>().CreateFileLogger(logName));
            return builder;
        }
    }
}
