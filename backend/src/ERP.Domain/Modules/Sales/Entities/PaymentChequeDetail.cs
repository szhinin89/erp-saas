namespace ERP.Domain.Modules.Sales.Entities;

public sealed class PaymentChequeDetail
{
    public const int BankMaxLen    = 100;
    public const int NumberMaxLen  = 50;
    public const int HolderMaxLen  = 100;

    public Guid     PaymentId     { get; private set; }
    public string?  BankName      { get; private set; }
    public string?  ChequeNumber  { get; private set; }
    public string?  HolderName    { get; private set; }
    public DateOnly? CashDate     { get; private set; }

    private PaymentChequeDetail() { }

    public static PaymentChequeDetail Create(
        Guid paymentId, string? bankName, string? chequeNumber,
        string? holderName, DateOnly? cashDate)
    {
        return new PaymentChequeDetail
        {
            PaymentId    = paymentId,
            BankName     = bankName?.Trim(),
            ChequeNumber = chequeNumber?.Trim(),
            HolderName   = holderName?.Trim(),
            CashDate     = cashDate,
        };
    }
}
