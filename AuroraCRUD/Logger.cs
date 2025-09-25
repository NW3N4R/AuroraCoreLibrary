public class Logger
{
    public static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "AuroraLibrary.log");
    private static readonly Queue<string> logQueue = new Queue<string>();
    private static readonly object lockObj = new object();
    private static bool isProcessing = false;

    public static void Log(string message, logStatus log = logStatus.ongoing)
    {
        lock (lockObj)
        {
            string ul = "";
            for (int i = 0; i < (message.Length + log.ToString().Length) + 3; i++)
            {
                ul += "_";
            }
            string loggerString = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{log} | {message}\n{ul}\n\n";
            logQueue.Enqueue(loggerString);

            if (!isProcessing)
            {
                isProcessing = true;
                _ = Process();
            }
        }
    }

    private static async Task Process()
    {
        while (true)
        {
            string? entry = null;
            lock (lockObj)
            {
                if (logQueue.Count > 0)
                {
                    entry = logQueue.Dequeue();
                } else
                {
                    isProcessing = false;
                    return;
                }
            }

            if (entry != null)
            {
                await File.AppendAllTextAsync(LogFilePath, entry + Environment.NewLine);
            }
        }
    }

}
public enum logStatus
{
    error,
    warning,
    success,
    ongoing
}
