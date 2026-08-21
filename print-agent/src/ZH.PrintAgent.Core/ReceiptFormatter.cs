using System.Globalization;
using System.Text;
using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.Core;

public sealed class ReceiptFormatter
{
    public const int Columns80Mm = 48;

    public string Format80Mm(ReceiptDocument receipt)
    {
        var builder = new StringBuilder();

        AppendCentered(builder, receipt.MerchantName);
        AppendLines(builder, receipt.HeaderLines);

        if (receipt.Items.Count > 0)
        {
            AppendSeparator(builder);
            foreach (var item in receipt.Items)
            {
                AppendWrapped(builder, item.Name);
                var quantity = item.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
                var detail = $"{quantity} x {Money(item.UnitPrice)}";
                AppendLeftRight(builder, detail, Money(item.Total));
            }
        }

        if (receipt.Totals.Count > 0)
        {
            AppendSeparator(builder);
            foreach (var total in receipt.Totals)
            {
                AppendLeftRight(builder, total.Label, Money(total.Amount));
            }
        }

        AppendLines(builder, receipt.RawLines);
        AppendLines(builder, receipt.FooterLines);
        builder.AppendLine();
        return builder.ToString();
    }

    private static void AppendLines(StringBuilder builder, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            AppendWrapped(builder, line);
        }
    }

    private static void AppendCentered(StringBuilder builder, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var line in Wrap(value.Trim()))
        {
            var padding = Math.Max(0, (Columns80Mm - line.Length) / 2);
            builder.AppendLine(new string(' ', padding) + line);
        }
    }

    private static void AppendWrapped(StringBuilder builder, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var line in Wrap(value.Trim()))
        {
            builder.AppendLine(line);
        }
    }

    private static void AppendLeftRight(StringBuilder builder, string left, string right)
    {
        left = Normalize(left);
        right = Normalize(right);
        var space = Columns80Mm - left.Length - right.Length;
        if (space < 1)
        {
            builder.AppendLine(left[..Math.Min(left.Length, Columns80Mm)]);
            builder.AppendLine(right.PadLeft(Columns80Mm));
            return;
        }

        builder.AppendLine(left + new string(' ', space) + right);
    }

    private static void AppendSeparator(StringBuilder builder)
    {
        builder.AppendLine(new string('-', Columns80Mm));
    }

    private static IEnumerable<string> Wrap(string value)
    {
        value = Normalize(value);
        while (value.Length > Columns80Mm)
        {
            var split = value.LastIndexOf(' ', Columns80Mm - 1, Columns80Mm);
            if (split <= 0)
            {
                split = Columns80Mm;
            }

            yield return value[..split].TrimEnd();
            value = value[split..].TrimStart();
        }

        if (value.Length > 0)
        {
            yield return value;
        }
    }

    private static string Normalize(string value)
    {
        return value.ReplaceLineEndings(" ").Trim();
    }

    private static string Money(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
