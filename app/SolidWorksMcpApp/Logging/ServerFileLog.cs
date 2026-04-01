namespace SolidWorksMcpApp.Logging;

internal static class ServerFileLog
{
    private static readonly object s_lock = new();

    public static void Append(string level, string source, string message, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(ServerState.LogFilePath))
        {
            return;
        }

        var client = ServerState.ConnectedClientName is { } name ? $" [{name}]" : string.Empty;
        var line = $"[{level}]{client} {DateTime.Now:yyyy-MM-dd HH:mm:ss} {source}: {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + Indent(exception.ToString());
        }

        try
        {
            lock (s_lock)
            {
                File.AppendAllText(ServerState.LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never let logging failures take down the hub.
        }
    }

    private static string Indent(string text)
    {
        return string.Join(
            Environment.NewLine,
            text.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => $"  {line}"));
    }
}