using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.Core;

public static class PrintJobValidator
{
    public static IReadOnlyList<string> Validate(SubmitPrintJobRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            errors.Add("jobId is required.");
        }

        if (request.JobId.Length > 128)
        {
            errors.Add("jobId cannot exceed 128 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            errors.Add("printerName is required.");
        }

        if (request.PrinterName.Length > 128)
        {
            errors.Add("printerName cannot exceed 128 characters.");
        }

        if (request.Copies < 1 || request.Copies > 5)
        {
            errors.Add("copies must be between 1 and 5.");
        }

        if (request.Receipt is null)
        {
            errors.Add("receipt is required.");
            return errors;
        }

        var hasPrintableContent =
            !string.IsNullOrWhiteSpace(request.Receipt.MerchantName) ||
            request.Receipt.HeaderLines.Count > 0 ||
            request.Receipt.Items.Count > 0 ||
            request.Receipt.Totals.Count > 0 ||
            request.Receipt.RawLines.Count > 0 ||
            request.Receipt.FooterLines.Count > 0;

        if (!hasPrintableContent)
        {
            errors.Add("receipt must contain printable content.");
        }

        return errors;
    }
}
