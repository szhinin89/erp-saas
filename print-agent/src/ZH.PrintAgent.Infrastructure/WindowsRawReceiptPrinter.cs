using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class WindowsRawReceiptPrinter : IReceiptPrinter
{
    public Task PrintAsync(PrintJob job, string formattedReceipt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = BuildEscPosPayload(formattedReceipt);
        WindowsRawPrinterInterop.WriteRaw(
            job.PrinterName,
            $"ZH Print Agent {job.JobId} attempt {job.Attempts}",
            bytes);

        return Task.CompletedTask;
    }

    private static byte[] BuildEscPosPayload(string formattedReceipt)
    {
        using var stream = new MemoryStream();

        // ESC/POS basics: initialize, align left, bold off, select common Windows code page.
        stream.Write(new byte[] { 0x1B, 0x40 });
        stream.Write(new byte[] { 0x1B, 0x61, 0x00 });
        stream.Write(new byte[] { 0x1B, 0x45, 0x00 });
        stream.Write(new byte[] { 0x1B, 0x74, 0x10 });

        var normalized = formattedReceipt.ReplaceLineEndings("\n");
        var textBytes = System.Text.Encoding.Latin1.GetBytes(normalized);
        stream.Write(textBytes);

        stream.Write(new byte[] { 0x0A, 0x0A, 0x0A });
        stream.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 });

        return stream.ToArray();
    }
}
