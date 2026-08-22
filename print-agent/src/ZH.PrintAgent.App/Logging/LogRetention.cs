namespace ZH.PrintAgent.App.Logging;

public static class LogRetention
{
    public static void PruneOldLogs(string logDirectory, int retentionDays)
    {
        if (!Directory.Exists(logDirectory) || retentionDays <= 0)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        foreach (var file in Directory.EnumerateFiles(logDirectory, "printagent-*.log"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // Skip files that are locked or otherwise inaccessible; retry on next startup.
            }
        }
    }
}
