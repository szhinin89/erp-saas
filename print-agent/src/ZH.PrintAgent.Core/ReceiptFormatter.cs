using System.Globalization;
using System.Text;
using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.Core;

public sealed class ReceiptFormatter
{
    public const int Columns80Mm = 48;
    public const int Columns58Mm = 32;

    public static int ColumnsForWidth(int paperWidthMm)
    {
        return paperWidthMm <= 58 ? Columns58Mm : Columns80Mm;
    }

    public string Format80Mm(ReceiptDocument receipt)
    {
        return Format(receipt, Columns80Mm);
    }

    public string Format(ReceiptDocument receipt, int columns)
    {
        var builder = new StringBuilder();

        AppendCentered(builder, receipt.MerchantName, columns);
        AppendLines(builder, receipt.HeaderLines, columns);

        if (receipt.Items.Count > 0)
        {
            AppendSeparator(builder, columns);
            foreach (var item in receipt.Items)
            {
                AppendWrapped(builder, item.Name, columns);
                var quantity = item.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
                var detail = $"{quantity} x {Money(item.UnitPrice)}";
                AppendLeftRight(builder, detail, Money(item.Total), columns);
            }
        }

        if (receipt.Totals.Count > 0)
        {
            AppendSeparator(builder, columns);
            foreach (var total in receipt.Totals)
            {
                AppendLeftRight(builder, total.Label, Money(total.Amount), columns);
            }
        }

        AppendLines(builder, receipt.RawLines, columns);
        AppendLines(builder, receipt.FooterLines, columns);
        builder.AppendLine();
        return builder.ToString();
    }

    private static void AppendLines(StringBuilder builder, IEnumerable<string> lines, int columns)
    {
        foreach (var line in lines)
        {
            AppendWrapped(builder, line, columns);
        }
    }

    private static void AppendCentered(StringBuilder builder, string value, int columns)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var line in Wrap(value.Trim(), columns))
        {
            var padding = Math.Max(0, (columns - line.Length) / 2);
            builder.AppendLine(new string(' ', padding) + line);
        }
    }

    private static void AppendWrapped(StringBuilder builder, string value, int columns)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var line in Wrap(value.Trim(), columns))
        {
            builder.AppendLine(line);
        }
    }

    private static void AppendLeftRight(StringBuilder builder, string left, string right, int columns)
    {
        left = Normalize(left);
        right = Normalize(right);
        var space = columns - left.Length - right.Length;
        if (space < 1)
        {
            builder.AppendLine(left[..Math.Min(left.Length, columns)]);
            builder.AppendLine(right.PadLeft(columns));
            return;
        }

        builder.AppendLine(left + new string(' ', space) + right);
    }

    private static void AppendSeparator(StringBuilder builder, int columns)
    {
        builder.AppendLine(new string('-', columns));
    }

    private static IEnumerable<string> Wrap(string value, int columns)
    {
        value = Normalize(value);
        while (value.Length > columns)
        {
            var split = value.LastIndexOf(' ', columns - 1, columns);
            if (split <= 0)
            {
                split = columns;
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
