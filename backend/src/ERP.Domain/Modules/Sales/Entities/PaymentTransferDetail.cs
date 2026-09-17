namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>
/// SALES-TRANSFER-BANK-ACCOUNT-01: detalle de un pago por Transferencia Bancaria. La cuenta
/// bancaria destino (<see cref="CompanyBankAccountId"/>) es ahora la fuente de verdad de "qué
/// banco/cuenta recibió la transferencia" — reemplaza el texto libre <see cref="BankName"/>
/// (conservado solo por compatibilidad histórica de facturas ya autorizadas antes de este
/// ticket; ningún código nuevo lo escribe). Comprobante y fecha de operación son obligatorios.
/// </summary>
public sealed class PaymentTransferDetail
{
    public const int BankMaxLen = 100;
    public const int ReceiptMaxLen = 50;

    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Nullable a nivel de tipo únicamente para permitir que EF materialice filas históricas
    /// (autorizadas antes de este ticket, sin cuenta bancaria seleccionada). <see cref="Create"/>
    /// exige siempre un valor real — ningún código nuevo produce una fila con este campo null.
    /// </summary>
    public Guid? CompanyBankAccountId { get; private set; }

    /// <summary>Legado — texto libre de facturas anteriores a SALES-TRANSFER-BANK-ACCOUNT-01. No se escribe desde código nuevo.</summary>
    public string? BankName { get; private set; }

    /// <summary>Nullable a nivel de tipo solo por compatibilidad con filas históricas — <see cref="Create"/> siempre exige un valor.</summary>
    public string? ReceiptNumber { get; private set; }

    /// <summary>Nullable a nivel de tipo solo por compatibilidad con filas históricas — <see cref="Create"/> siempre exige un valor.</summary>
    public DateOnly? TransferDate { get; private set; }

    private PaymentTransferDetail() { }

    public static PaymentTransferDetail Create(
        Guid paymentId,
        Guid companyBankAccountId,
        string receiptNumber,
        DateOnly transferDate
    )
    {
        if (companyBankAccountId == Guid.Empty)
            throw new ArgumentException(
                "La cuenta bancaria destino es obligatoria.",
                nameof(companyBankAccountId)
            );
        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new ArgumentException(
                "El comprobante/referencia de la transferencia es obligatorio.",
                nameof(receiptNumber)
            );
        var normalizedReceipt = receiptNumber.Trim();
        if (normalizedReceipt.Length > ReceiptMaxLen)
            throw new ArgumentException(
                $"El comprobante no puede superar {ReceiptMaxLen} caracteres.",
                nameof(receiptNumber)
            );

        return new PaymentTransferDetail
        {
            PaymentId = paymentId,
            CompanyBankAccountId = companyBankAccountId,
            ReceiptNumber = normalizedReceipt,
            TransferDate = transferDate,
        };
    }
}
