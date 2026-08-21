using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class SimulatedReceiptPrinter : IReceiptPrinter
{
    private readonly string outputDirectory;
    private readonly ISet<string> failingPrinters;

    public SimulatedReceiptPrinter(string outputDirectory, IEnumerable<string> failingPrinters)
    {
        this.outputDirectory = outputDirectory;
        this.failingPrinters = new HashSet<string>(failingPrinters, StringComparer.OrdinalIgnoreCase);
    }

    public async Task PrintAsync(PrintJob job, string formattedReceipt, CancellationToken cancellationToken)
    {
        if (failingPrinters.Contains(job.PrinterName) ||
            job.PrinterName.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Printer '{job.PrinterName}' is configured to fail.");
        }

        var printerDirectory = Path.Combine(outputDirectory, Sanitize(job.PrinterName));
        Directory.CreateDirectory(printerDirectory);

        var fileName = $"{Sanitize(job.JobId)}-attempt-{job.Attempts:000}-{Guid.NewGuid():N}.txt";
        var path = Path.Combine(printerDirectory, fileName);
        await File.WriteAllTextAsync(path, formattedReceipt, cancellationToken);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }
}
