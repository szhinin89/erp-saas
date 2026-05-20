namespace ERP.Domain.Common;

public enum WithholdingDirection
{
    Issued,
    Received
}

public static class WithholdingDirectionExtensions
{
    public static string ToDbValue(this WithholdingDirection direction) =>
        direction == WithholdingDirection.Issued ? "ISSUED" : "RECEIVED";

    public static WithholdingDirection FromDbValue(string value) =>
        value.Trim().ToUpperInvariant() == "ISSUED"
            ? WithholdingDirection.Issued
            : WithholdingDirection.Received;
}
