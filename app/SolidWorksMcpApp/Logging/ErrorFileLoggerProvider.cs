using Microsoft.Extensions.Logging;

namespace SolidWorksMcpApp.Logging;

internal sealed class ErrorFileLoggerProvider(string filePath) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new ErrorFileLogger(categoryName, filePath);

    public void Dispose() { }
}
