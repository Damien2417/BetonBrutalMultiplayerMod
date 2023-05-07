using System;
using System.IO;

public class Logger
{
    private readonly string _prefix;
    private readonly string _logFilePath;
    private readonly object _logFileLock = new object();

    public Logger(string prefix, string logFilePath)
    {
        _prefix = prefix;
        _logFilePath = logFilePath;
    }

    public void Log(string message)
    {
        string formattedMessage = $"{_prefix} {DateTime.Now}: {message}";

        lock (_logFileLock)
        {
            using (StreamWriter streamWriter = new StreamWriter(_logFilePath, true))
            {
                streamWriter.WriteLine(formattedMessage);
            }
        }
    }
}
