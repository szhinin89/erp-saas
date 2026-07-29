namespace ERP.Domain.Modules.Sales.Entities;

public sealed class PaymentTransferDetail
{
    public const int BankMaxLen = 100;
    public const int ReceiptMaxLen = 50;

    public Guid PaymentId { get; private set; }
    public string? BankName { get; private set; }
    public string? ReceiptNumber { get; private set; }
    public DateOnly? TransferDate { get; private set; }

    private PaymentTransferDetail() { }

    public static PaymentTransferDetail Create(
        Guid paymentId,
        string? bankName,
        string? receiptNumber,
        DateOnly? transferDate
    )
    {
        return new PaymentTransferDetail
        {
            PaymentId = paymentId,
            BankName = bankName?.Trim(),
            ReceiptNumber = receiptNumber?.Trim(),
            TransferDate = transferDate,
        };
    }
}
